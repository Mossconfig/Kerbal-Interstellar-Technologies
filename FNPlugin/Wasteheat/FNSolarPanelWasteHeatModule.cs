using OpenResourceSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FNPlugin 
{
    enum resourceType
    {
        electricCharge, megajoule, other
    }

	class FNSolarPanelWasteHeatModule : FNResourceSuppliableModule 
    {
        [KSPField(isPersistant = false, guiActiveEditor = false, guiActive = true,  guiName = "Solar Power", guiUnits = " MW", guiFormat="F5")]
        public double megaJouleSolarPowerSupply;

		public string heatProductionStr = ":";

        protected ModuleDeployableSolarPanel solarPanel;
        private bool active = false;
        private float previousDeltaTime;

        resourceType outputType = 0;

        PartResource megajoulePartResource;
        PartResource electricChargePartResource;

        private double fixedMegajouleBufferSize;
        private double fixedElectricChargeBufferSize;

        public override void OnStart(PartModule.StartState state)
        {
            String[] resources_to_supply = { FNResourceManager.FNRESOURCE_MEGAJOULES };
            this.resources_to_supply = resources_to_supply;
            base.OnStart(state);

            if (state == StartState.Editor)
            {
                return;
            }

            previousDeltaTime = TimeWarp.fixedDeltaTime;

            solarPanel = (ModuleDeployableSolarPanel)this.part.FindModuleImplementing<ModuleDeployableSolarPanel>();

            if (solarPanel == null) return;

            if (solarPanel.resourceName == FNResourceManager.FNRESOURCE_MEGAJOULES)
            {
                outputType = resourceType.megajoule;

                megajoulePartResource = part.Resources[FNResourceManager.FNRESOURCE_MEGAJOULES];
                if (megajoulePartResource != null)
                {
                    fixedMegajouleBufferSize = megajoulePartResource.maxAmount * 50;
                }
            }
            else if (solarPanel.resourceName == FNResourceManager.STOCK_RESOURCE_ELECTRICCHARGE)
            {
                outputType = resourceType.electricCharge;

                electricChargePartResource = part.Resources[FNResourceManager.STOCK_RESOURCE_ELECTRICCHARGE];
                if (electricChargePartResource != null)
                {
                    fixedElectricChargeBufferSize = electricChargePartResource.maxAmount * 50;
                }
            }
            else
                outputType = resourceType.other;
        }

        public override void OnFixedUpdate() 
        {
            active = true;
            base.OnFixedUpdate();
        }


        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight) return;

            if (!active)
                base.OnFixedUpdate();

            if (solarPanel == null) return;

            if (outputType == resourceType.other) return;

            if (megajoulePartResource != null && fixedMegajouleBufferSize > 0 && TimeWarp.fixedDeltaTime != previousDeltaTime)
            {
                double requiredMegawattCapacity = fixedMegajouleBufferSize * TimeWarp.fixedDeltaTime;
                double previousMegawattCapacity = fixedMegajouleBufferSize * previousDeltaTime;
                double ratio = megajoulePartResource.amount / megajoulePartResource.maxAmount;

                megajoulePartResource.maxAmount = requiredMegawattCapacity;
                megajoulePartResource.amount = TimeWarp.fixedDeltaTime > previousDeltaTime
                    ? Math.Max(0, Math.Min(requiredMegawattCapacity, megajoulePartResource.amount + requiredMegawattCapacity - previousMegawattCapacity))
                    : Math.Max(0, Math.Min(requiredMegawattCapacity, ratio * requiredMegawattCapacity));
            }

            if (electricChargePartResource != null && fixedElectricChargeBufferSize > 0 && TimeWarp.fixedDeltaTime != previousDeltaTime)
            {
                double requiredElectricChargeCapacity = fixedElectricChargeBufferSize * TimeWarp.fixedDeltaTime;
                double previousPreviousElectricCapacity = fixedElectricChargeBufferSize * previousDeltaTime;
                double ratio = electricChargePartResource.amount / electricChargePartResource.maxAmount;

                electricChargePartResource.maxAmount = requiredElectricChargeCapacity;
                electricChargePartResource.amount = TimeWarp.fixedDeltaTime > previousDeltaTime
                    ? Math.Max(0, Math.Min(requiredElectricChargeCapacity, electricChargePartResource.amount + requiredElectricChargeCapacity - previousPreviousElectricCapacity))
                    : Math.Max(0, Math.Min(requiredElectricChargeCapacity, ratio * requiredElectricChargeCapacity));
            }

            previousDeltaTime = TimeWarp.fixedDeltaTime;

            double solar_rate = solarPanel.flowRate * TimeWarp.fixedDeltaTime;
            double maxSupply = solarPanel.chargeRate * solarPanel._distMult * solarPanel._efficMult * TimeWarp.fixedDeltaTime;

            double solar_supply = outputType == resourceType.megajoule ? solar_rate : solar_rate / 1000;
            double solar_maxSupply = outputType == resourceType.megajoule ? maxSupply : maxSupply / 1000;

            megaJouleSolarPowerSupply = supplyFNResourceFixedMax(solar_supply, solar_maxSupply, FNResourceManager.FNRESOURCE_MEGAJOULES) / TimeWarp.fixedDeltaTime;
        }

        public override string getResourceManagerDisplayName()
        {
            return part.partInfo.title;
        }
	}
}

