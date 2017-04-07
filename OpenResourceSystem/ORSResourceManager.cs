﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace OpenResourceSystem 
{
    public class PowerConsumption
    {
        public double Power_draw { get; set; }
        public double Power_consume { get; set; }
    }

    public class ORSResourceManager 
    {
        public const string STOCK_RESOURCE_ELECTRICCHARGE = "ElectricCharge";
        public const string FNRESOURCE_MEGAJOULES = "Megajoules";
        public const string FNRESOURCE_CHARGED_PARTICLES = "ChargedParticles";
        public const string FNRESOURCE_THERMALPOWER = "ThermalPower";
		public const string FNRESOURCE_WASTEHEAT = "WasteHeat";

		public const int FNRESOURCE_FLOWTYPE_SMALLEST_FIRST = 0;
		public const int FNRESOURCE_FLOWTYPE_EVEN = 1;
               
        protected Vessel my_vessel;
        protected Part my_part;
        protected PartModule my_partmodule;

        protected PartResourceDefinition resourceDefinition;
        protected PartResourceDefinition electricResourceDefinition;
        protected PartResourceDefinition thermalpowerResourceDefinition;
        protected PartResourceDefinition chargedpowerResourceDefinition;

        protected Dictionary<ORSResourceSuppliable, PowerConsumption> power_consumption;

        protected Dictionary<ORSResourceSupplier, double> power_max_supplies;   // maximum supplied power by power producers 
        protected Dictionary<ORSResourceSupplier, double> power_min_supplies;   // minimum supplied power by power producers 

        protected string resource_name;
        protected double currentPowerSupply = 0;
		protected double stable_supply = 0;

		protected double stored_stable_supply = 0;
        protected double stored_resource_demand = 0;
        protected double stored_current_hp_demand = 0;
        protected double stored_current_demand = 0;
        protected double stored_current_charge_demand = 0;
        protected double stored_supply = 0;
        protected double stored_charge_demand = 0;
        protected double stored_total_power_supplied = 0;

		protected double current_resource_demand = 0;
		protected double high_priority_resource_demand = 0;
		protected double charge_resource_demand = 0;
        protected double total_power_distributed = 0;

		protected int flow_type = 0;
        protected List<KeyValuePair<ORSResourceSuppliable, PowerConsumption>> power_draw_list_archive;
        protected bool render_window = false;
        protected Rect windowPosition = new Rect(50, 50, 300, 100);
        protected int windowID = 36549835;
		protected double resource_bar_ratio = 0;

        protected GUIStyle bold_label;
        protected GUIStyle green_label;
        protected GUIStyle red_label;
        protected GUIStyle right_align;

        protected int updateCounter;
        protected double internl_power_extract = 0;

        public Rect WindowPosition 
        { 
            get { return windowPosition; }
            set { windowPosition = value; }
        }

        public int WindowID
        {
            get { return windowID; }
            set { windowID = value; }
        }

        public ORSResourceManager(PartModule pm,String resource_name) 
        {
            my_vessel = pm.vessel;
            my_part = pm.part;
            my_partmodule = pm;

            windowID = new System.Random(resource_name.GetHashCode()).Next(int.MinValue, int.MaxValue);

            power_consumption = new Dictionary<ORSResourceSuppliable, PowerConsumption>();

            power_max_supplies = new Dictionary<ORSResourceSupplier, double>();
            power_min_supplies = new Dictionary<ORSResourceSupplier, double>();

            this.resource_name = resource_name;

            resourceDefinition = PartResourceLibrary.Instance.GetDefinition(resource_name);
            electricResourceDefinition = PartResourceLibrary.Instance.GetDefinition(ORSResourceManager.STOCK_RESOURCE_ELECTRICCHARGE);
            thermalpowerResourceDefinition = PartResourceLibrary.Instance.GetDefinition(ORSResourceManager.FNRESOURCE_THERMALPOWER);
            chargedpowerResourceDefinition = PartResourceLibrary.Instance.GetDefinition(ORSResourceManager.FNRESOURCE_CHARGED_PARTICLES);

            if (resource_name == FNRESOURCE_WASTEHEAT || resource_name == FNRESOURCE_THERMALPOWER || resource_name == FNRESOURCE_CHARGED_PARTICLES) 
				flow_type = FNRESOURCE_FLOWTYPE_EVEN;
			else 
				flow_type = FNRESOURCE_FLOWTYPE_SMALLEST_FIRST;
        }

        public void powerDraw(ORSResourceSuppliable pm, double power_draw, double power_cosumtion) 
        {
            var power_draw_per_second = power_draw / TimeWarp.fixedDeltaTime;
            var power_cosumtion_per_second = power_cosumtion / TimeWarp.fixedDeltaTime;
            
            PowerConsumption powerConsumption;
            if (power_consumption.TryGetValue(pm, out powerConsumption))
            {
                powerConsumption.Power_draw += power_draw_per_second;
                powerConsumption.Power_consume += power_cosumtion_per_second;
            }
            else
            {
                power_consumption.Add(pm, new PowerConsumption() 
                { 
                    Power_draw = power_draw_per_second, 
                    Power_consume = power_cosumtion_per_second
                });
            }           
        }

        public double powerSupply(ORSResourceSupplier pm, double power) 
        {
            currentPowerSupply += (power / TimeWarp.fixedDeltaTime);
			stable_supply += (power / TimeWarp.fixedDeltaTime);

            if (power_max_supplies.ContainsKey(pm)) 
                power_max_supplies[pm] += (power / TimeWarp.fixedDeltaTime);
            else 
                power_max_supplies.Add(pm, (power / TimeWarp.fixedDeltaTime));
            
            return power;
        }

        public double powerSupplyFixedMax(ORSResourceSupplier pm, double power, double maxpower) 
        {
			currentPowerSupply += (power / TimeWarp.fixedDeltaTime);
			stable_supply += (maxpower / TimeWarp.fixedDeltaTime);

            if (power_max_supplies.ContainsKey(pm)) 
                power_max_supplies[pm] += (power / TimeWarp.fixedDeltaTime);
            else 
                power_max_supplies.Add(pm, (power / TimeWarp.fixedDeltaTime));
			return power;
		}

        public double managedPowerSupply(ORSResourceSupplier pm, double power) 
        {
			return managedPowerSupplyWithMinimumRatio (pm, power, 0);
		}

        public double getResourceAvailability()
        {
            var resourceDefinition = PartResourceLibrary.Instance.GetDefinition(resource_name);

            double amount;
            double maxAmount;
            my_part.GetConnectedResourceTotals(resourceDefinition.id, out amount, out maxAmount);

            return amount;
        }

		public double getSpareResourceCapacity() 
        {
            var resourceDefinition = PartResourceLibrary.Instance.GetDefinition(resource_name);

            double amount;
            double maxAmount;
            my_part.GetConnectedResourceTotals(resourceDefinition.id, out amount, out maxAmount);

            return maxAmount - amount;
		}

        public double getTotalResourceCapacity()
        {
            var resourceDefinition = PartResourceLibrary.Instance.GetDefinition(resource_name);

            double amount;
            double maxAmount;
            my_part.GetConnectedResourceTotals(resourceDefinition.id, out amount, out maxAmount);

            return maxAmount;
		}

        public double managedPowerSupplyWithMinimumRatio(ORSResourceSupplier pm, double power, double rat_min) 
        {
			var maximum_available_power_per_second = power / TimeWarp.fixedDeltaTime;
            var minimum_power_per_second = maximum_available_power_per_second * rat_min;
            var required_power_per_second = Math.Max(GetRequiredResourceDemand(), minimum_power_per_second);
            var managed_supply_per_second = Math.Min(maximum_available_power_per_second, required_power_per_second);

			currentPowerSupply += managed_supply_per_second;
			stable_supply += maximum_available_power_per_second;

            if (power_max_supplies.ContainsKey(pm))
                power_max_supplies[pm] += maximum_available_power_per_second;
            else
                power_max_supplies.Add(pm, maximum_available_power_per_second);

            if (power_min_supplies.ContainsKey(pm))
                power_min_supplies[pm] += minimum_power_per_second;
            else
                power_min_supplies.Add(pm, minimum_power_per_second);

			return managed_supply_per_second * TimeWarp.fixedDeltaTime;
		}

        public double getStableResourceSupply() 
        {
            return  stored_stable_supply;
        }

        public double getResourceSupply()
        {
            return stored_supply;
        }

        public double getDemandSupply()
        {
            return stored_supply - stored_resource_demand;
        }

        public double getDemandStableSupply()
        {
            return stored_resource_demand / stored_stable_supply;
        }

        public double getResourceDemand()
        {
            return stored_resource_demand;
        }

		public double getCurrentResourceDemand() 
        {
			return current_resource_demand;
		}

        public double getCurrentHighPriorityResourceDemand() 
        {
            return stored_current_hp_demand;
		}

		public double getCurrentUnfilledResourceDemand() 
        {
			return current_resource_demand - currentPowerSupply;
		}

        public double GetRequiredResourceDemand()
        {
            return getCurrentUnfilledResourceDemand() + getSpareResourceCapacity() / TimeWarp.fixedDeltaTime;
        }

        public double GetPowerSupply()
        {
            return currentPowerSupply;
        }

        public double GetCurrentRresourceDemand()
        {
            return current_resource_demand;
        }

		public double getResourceBarRatio() 
        {
			return resource_bar_ratio;
		}

        public Vessel getVessel() 
        {
            return my_vessel;
        }

		public void updatePartModule(PartModule pm) 
        {
            if (pm != null)
            {
                my_vessel = pm.vessel;
                my_part = pm.part;
                my_partmodule = pm;
            }
            else
            {
                my_partmodule = null;
            }
		}

		public PartModule getPartModule() 
        {
			return my_partmodule;
		}

        public bool IsUpdatedAtLeastOnce { get; set; }

        public void update() 
        {
            updateCounter++;

            IsUpdatedAtLeastOnce = true;

            stored_supply = currentPowerSupply;
			stored_stable_supply = stable_supply;
            stored_resource_demand = current_resource_demand;
			stored_current_demand = current_resource_demand;
			stored_current_hp_demand = high_priority_resource_demand;
			stored_current_charge_demand = charge_resource_demand;
            stored_charge_demand = charge_resource_demand;
            stored_total_power_supplied = total_power_distributed;

			current_resource_demand = 0;
			high_priority_resource_demand = 0;
            charge_resource_demand = 0;
            total_power_distributed = 0;

            double availableResourceAmount;
            double maxResouceAmount;
            my_part.GetConnectedResourceTotals(resourceDefinition.id, out availableResourceAmount, out maxResouceAmount);

			if (maxResouceAmount > 0) 
				resource_bar_ratio = availableResourceAmount / maxResouceAmount;
            else 
				resource_bar_ratio = 0;

			double missingResourceAmount = maxResouceAmount - availableResourceAmount;
            currentPowerSupply += availableResourceAmount;

            double high_priority_demand_supply_ratio = high_priority_resource_demand > 0
                ? Math.Min((currentPowerSupply - stored_current_charge_demand) / stored_current_hp_demand, 1.0)
                : 1.0;

            double demand_supply_ratio = stored_current_demand > 0
                ? Math.Min((currentPowerSupply - stored_current_charge_demand - stored_current_hp_demand) / stored_current_demand, 1.0)
                : 1.0;        

			//Prioritise supplying stock ElectricCharge resource
			if (String.Equals(this.resource_name, ORSResourceManager.FNRESOURCE_MEGAJOULES) && stored_stable_supply > 0) 
            {
                double amount;
                double maxAmount;

                my_part.GetConnectedResourceTotals(electricResourceDefinition.id, out amount, out maxAmount);
                double stock_electric_charge_needed = maxAmount - amount;

				double power_supplied = Math.Min(currentPowerSupply * 1000 * TimeWarp.fixedDeltaTime, stock_electric_charge_needed);
                if (stock_electric_charge_needed > 0) 
                {
                    var deltaResourceDemand = stock_electric_charge_needed / 1000.0 / TimeWarp.fixedDeltaTime;
                    current_resource_demand += deltaResourceDemand;
                    charge_resource_demand += deltaResourceDemand;
                }

                if (power_supplied > 0)
                {
                    double fixed_provided_electric_charge_in_MW = my_part.RequestResource(ORSResourceManager.STOCK_RESOURCE_ELECTRICCHARGE, -power_supplied) / 1000;
                    var provided_electric_charge_per_second = fixed_provided_electric_charge_in_MW / TimeWarp.fixedDeltaTime;
                    total_power_distributed += -provided_electric_charge_per_second;
                    currentPowerSupply += provided_electric_charge_per_second;
                }
			}

			//sort by power draw
            List<KeyValuePair<ORSResourceSuppliable, PowerConsumption>> power_draw_items = power_consumption.OrderBy(m => m.Value.Power_draw).ToList();

            power_draw_list_archive = power_draw_items.ToList();
            power_draw_list_archive.Reverse();
            
            // check priority 1 parts like reactors
            foreach (KeyValuePair<ORSResourceSuppliable, PowerConsumption> power_kvp in power_draw_items) 
            {
                ORSResourceSuppliable resourceSuppliable = power_kvp.Key;

                if (resourceSuppliable.getPowerPriority() == 1) 
                {
                    double power = power_kvp.Value.Power_draw;
					current_resource_demand += power;
					high_priority_resource_demand += power;

					if (flow_type == FNRESOURCE_FLOWTYPE_EVEN) 
						power = power * high_priority_demand_supply_ratio;
					
                    double power_supplied = Math.Max(Math.Min(currentPowerSupply, power), 0.0);

                    currentPowerSupply -= power_supplied;
                    total_power_distributed += power_supplied;

					//notify of supply
                    resourceSuppliable.receiveFNResource(power_supplied, this.resource_name);
                }
            }

            // check priority 2 parts like engines
            foreach (KeyValuePair<ORSResourceSuppliable, PowerConsumption> power_kvp in power_draw_items) 
            {
                ORSResourceSuppliable resourceSuppliable = power_kvp.Key;
                
                if (resourceSuppliable.getPowerPriority() == 2) 
                {
                    double power = power_kvp.Value.Power_draw;
					current_resource_demand += power;

					if (flow_type == FNRESOURCE_FLOWTYPE_EVEN) 
						power = power * demand_supply_ratio;
					
					double power_supplied = Math.Max(Math.Min(currentPowerSupply, power), 0.0);

                    currentPowerSupply -= power_supplied;
                    total_power_distributed += power_supplied;

					//notify of supply
					resourceSuppliable.receiveFNResource(power_supplied, this.resource_name);
                }
            }

            // check priority 3 parts
            foreach (KeyValuePair<ORSResourceSuppliable, PowerConsumption> power_kvp in power_draw_items) 
            {
				ORSResourceSuppliable resourceSuppliable = power_kvp.Key;

				if (resourceSuppliable.getPowerPriority() == 3) 
                {
					double power = power_kvp.Value.Power_draw;
					current_resource_demand += power;

					if (flow_type == FNRESOURCE_FLOWTYPE_EVEN) 
						power = power * demand_supply_ratio;

					double power_supplied = Math.Max(Math.Min(currentPowerSupply, power), 0.0);

					currentPowerSupply -= power_supplied;
                    total_power_distributed += power_supplied;

					//notify of supply
                    resourceSuppliable.receiveFNResource(power_supplied, this.resource_name);
				}
			}

            // substract avaialble resource amount to get delta resource change
            currentPowerSupply -= Math.Max(availableResourceAmount, 0.0);
            internl_power_extract = -currentPowerSupply * TimeWarp.fixedDeltaTime;

            pluginSpecificImpl();

            if (internl_power_extract > 0) 
                internl_power_extract = Math.Min(internl_power_extract, availableResourceAmount);
            else
                internl_power_extract = Math.Max(internl_power_extract, -missingResourceAmount);

            var actual_stored_power = my_part.RequestResource(this.resource_name, internl_power_extract);

            //calculate total input and output
            var total_power_consumed = power_consumption.Sum(m => m.Value.Power_consume);
            var total_power_max_supplied = power_max_supplies.Sum(m => m.Value);
            var total_power_min_supplied = power_min_supplies.Sum(m => m.Value);

            //generate wasteheat from used thermal power + thermal store
            if (!CheatOptions.IgnoreMaxTemperature && total_power_max_supplied > 0 && 
                (resourceDefinition.id == thermalpowerResourceDefinition.id || resourceDefinition.id == chargedpowerResourceDefinition.id))
            {
                // calculate Wasteheat
                var min_supplied_per_second = TimeWarp.fixedDeltaTime * total_power_min_supplied;
                var max_supplied_pers_second = TimeWarp.fixedDeltaTime * Math.Min(total_power_consumed, total_power_max_supplied) + Math.Max(-actual_stored_power, 0);
                var wasteheatProduction = Math.Max(min_supplied_per_second, max_supplied_pers_second);

                // generate Wasteheat
                var effective_wasteheat_ratio = wasteheatProduction / total_power_max_supplied;

                var overmanager = ORSResourceOvermanager.getResourceOvermanagerForResource(ORSResourceManager.FNRESOURCE_WASTEHEAT);
                ORSResourceManager manager = overmanager.getManagerForVessel(my_vessel);

                foreach (var supplier_key_value in power_max_supplies)
                {
                    manager.powerSupply(supplier_key_value.Key, supplier_key_value.Value * effective_wasteheat_ratio);
                }
                
            }

            currentPowerSupply = 0;
			stable_supply = 0;

            power_max_supplies.Clear();
            power_min_supplies.Clear();
            power_consumption.Clear();
        }

        protected virtual void pluginSpecificImpl() 
        {

        }

        public void showWindow() 
        {
            render_window = true;
        }

        public void hideWindow() 
        {
            render_window = false;
        }

        public void OnGUI() 
        {
            if (my_vessel == FlightGlobals.ActiveVessel && render_window) 
            {
                string title = resource_name + " Management Display";
                windowPosition = GUILayout.Window(windowID, windowPosition, doWindow, title);
            }
        }

        // overriden by FNResourceManager
        protected virtual void doWindow(int windowID) 
        {
           
        }

        protected string getPowerFormatString(double power) 
        {
            if (Math.Abs(power) >= 1000) 
            {
                if (Math.Abs(power) > 20000) 
                    return (power / 1000).ToString("0.0") + " GW";
                else 
                    return (power / 1000).ToString("0.00") + " GW";
            } 
            else 
            {
                if (Math.Abs(power) > 20) 
                    return power.ToString("0.0") + " MW";
                else 
                {
                    if (Math.Abs(power) >= 1) 
                        return power.ToString("0.00") + " MW";
                    
                    else 
                        return (power * 1000).ToString("0.0") + " KW";
                }
            }
        }
    }
}
