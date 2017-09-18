﻿using System;


namespace FNPlugin
{
    class ModuleStorageCryostat: FNModuleCryostat {}

    [KSPModule("Cryostat")]
    class FNModuleCryostat : FNResourceSuppliableModule
    {
        // Persistant
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Cooling"), UI_Toggle(disabledText = "On", enabledText = "Off")]
        public bool isDisabled = true;
        [KSPField(isPersistant = true)]
        public double storedTemp = 0;

        // Confiration
        [KSPField(isPersistant = false)]
        public string resourceName = "";
        [KSPField(isPersistant = false)]
        public string resourceGUIName = "";
        [KSPField(isPersistant = false, guiActive = false)]
        public double boilOffRate = 0;
        [KSPField(isPersistant = false, guiActive = false)]
        public double powerReqKW = 0;
        [KSPField(isPersistant = false, guiActive = false)]
        public double powerReqMult = 1;
        [KSPField(isPersistant = false)]
        public double boilOffMultiplier = 0;
        [KSPField(isPersistant = false)]
        public double boilOffBase = 10000;
        [KSPField(isPersistant = false)]
        public double boilOffAddition = 0;
        [KSPField(isPersistant = false)]
        public double boilOffTemp = 20.271;
        [KSPField(isPersistant = false)]
        public double convectionMod = 1;
        [KSPField(isPersistant = false)]
        public bool showPower = true;
        [KSPField(isPersistant = false)]
        public bool showBoiloff = true;
        [KSPField(isPersistant = false)]
        public bool showTemp = true;
        [KSPField(isPersistant = false)]
        public bool warningShown;

        //GUI
        [KSPField(isPersistant = false, guiActive = false, guiName = "Power")]
        public string powerStatusStr = String.Empty;
        [KSPField(isPersistant = false, guiActive = false, guiName = "Boiloff")]
        public string boiloffStr;
        [KSPField(isPersistant = false, guiActive = false, guiName = "Temperature", guiFormat = "F3", guiUnits = " K")]
        public double externalTemperature;
        [KSPField(isPersistant = false, guiActive = false, guiName = "internal boiloff")]
        public double boiloff;

        private PartResource _electricCharge_resource;
        private PartResource _cryostat_resource;

        private BaseField isDisabledField;
        private BaseField boiloffStrField;
        private BaseField powerStatusStrField;
        private BaseField externalTemperatureField;
            
        private double environmentBoiloff;
        private double environmentFactor;
        private double recievedPowerKW;
        private double previousRecievedPowerKW;
        private double currentPowerReq;
        private double previousPowerReq;
        private int initializationCountdown;
        private bool requiresPower;

        private float previousDeltaTime;
        private double previousPowerUsage;

        public override void OnStart(PartModule.StartState state)
        {
            enabled = true;

            // compensate for stock solar initialisation heating issies
            part.temperature = storedTemp;
            initializationCountdown = 100;
            requiresPower = powerReqKW > 0;

            if (state == StartState.Editor)
                return;

            // if electricCharge buffer is missing, add it.
            if (!part.Resources.Contains(InterstellarResourcesConfiguration.Instance.ElectricCharge))
            {
                ConfigNode node = new ConfigNode("RESOURCE");
                node.AddValue("name", InterstellarResourcesConfiguration.Instance.ElectricCharge);
                node.AddValue("maxAmount", powerReqKW > 0 ? powerReqKW / 50 : 1);
                node.AddValue("amount", powerReqKW > 0  ? powerReqKW / 50 : 1);
                part.AddResource(node);
            }

            // store reference to local electric charge buffer
            _electricCharge_resource = part.Resources[InterstellarResourcesConfiguration.Instance.ElectricCharge];

            isDisabledField = Fields["isDisabled"];
            boiloffStrField = Fields["boiloffStr"];
            powerStatusStrField = Fields["powerStatusStr"];
            externalTemperatureField = Fields["externalTemperature"];
        }

        private void UpdateElectricChargeBuffer(double currentPowerUsage)
        {
            if (_electricCharge_resource != null && (TimeWarp.fixedDeltaTime != previousDeltaTime || previousPowerUsage != currentPowerUsage))
            {
                var requiredCapacity = 2 * currentPowerUsage * TimeWarp.fixedDeltaTime;
                var bufferRatio = _electricCharge_resource.maxAmount > 0 ? _electricCharge_resource.amount / _electricCharge_resource.maxAmount : 0;

                _electricCharge_resource.maxAmount = requiredCapacity;
                _electricCharge_resource.amount =  bufferRatio * requiredCapacity;
            }

            previousPowerUsage = currentPowerUsage;
            previousDeltaTime = TimeWarp.fixedDeltaTime;
        }

        public override void OnUpdate()
        {
            _cryostat_resource = part.Resources[resourceName];

            if (_cryostat_resource != null)
            {
                bool coolingIsRelevant = _cryostat_resource.amount > 0.0000001 && (boilOffRate > 0 || requiresPower);

                powerStatusStrField.guiActive = showPower && requiresPower;
                boiloffStrField.guiActive = showBoiloff && boiloff > 0.00001;
                externalTemperatureField.guiActive = showTemp && coolingIsRelevant;

                if (!coolingIsRelevant)
                    return;

                var atmosphereModifier = convectionMod == -1 ? 0 : convectionMod + part.atmDensity / (convectionMod + 1);

                externalTemperature = part.temperature;
                if (Double.IsNaN(externalTemperature))
                {
                    part.temperature = part.skinTemperature;
                    externalTemperature = part.skinTemperature;
                }

                var temperatureModifier = Math.Max(0, externalTemperature - boilOffTemp) / 300; //273.15;

                environmentFactor = atmosphereModifier * temperatureModifier;

                if (powerReqKW > 0)
                {
                    currentPowerReq = powerReqKW * 0.2 * environmentFactor * powerReqMult;

                    UpdatePowerStatusSting();
                }
                else
                    currentPowerReq = 0;

                environmentBoiloff = environmentFactor * boilOffMultiplier * boilOffBase;
            }
            else
            {
                isDisabledField.guiActive = true;
                powerStatusStrField.guiActive = false;
                boiloffStrField.guiActive = false;
            }
        }

        private void UpdatePowerStatusSting()
        {
            powerStatusStr = currentPowerReq < 1.0e+3
                ? recievedPowerKW.ToString("0.00") + " KW / " + currentPowerReq.ToString("0.00") + " KW"
                : currentPowerReq < 1.0e+6
                    ? (recievedPowerKW / 1.0e+3).ToString("0.000") + " MW / " + (currentPowerReq / 1.0e+3).ToString("0.000") + " MW"
                    : (recievedPowerKW / 1.0e+6).ToString("0.000") + " GW / " + (currentPowerReq / 1.0e+6).ToString("0.000") + " GW";
        }

        // FixedUpdate is also called while not staged
        public void FixedUpdate()
        {
            if (_cryostat_resource == null)
            {
                boiloff = 0;
                return;
            }

            if (initializationCountdown > 0)
            {
                part.temperature = storedTemp;
                part.skinTemperature = storedTemp;
                initializationCountdown--;
            }

            if (!isDisabled && currentPowerReq > 0)
            {
                UpdateElectricChargeBuffer(Math.Max(currentPowerReq, 0.1 * powerReqKW));

                var fixedPowerReqKW = currentPowerReq * TimeWarp.fixedDeltaTime;

                var fixedRecievedChargeKW = CheatOptions.InfiniteElectricity 
                    ? fixedPowerReqKW
                    : consumeFNResource(fixedPowerReqKW / 1000, FNResourceManager.FNRESOURCE_MEGAJOULES) * 1000;

                if (fixedRecievedChargeKW <= fixedPowerReqKW)
                    fixedRecievedChargeKW += part.RequestResource(FNResourceManager.FNRESOURCE_MEGAJOULES, (fixedPowerReqKW - fixedRecievedChargeKW) / 1000) * 1000;

                if (currentPowerReq < 1000 && fixedRecievedChargeKW <= fixedPowerReqKW)
                    fixedRecievedChargeKW += part.RequestResource(FNResourceManager.STOCK_RESOURCE_ELECTRICCHARGE, fixedPowerReqKW - fixedRecievedChargeKW);

                recievedPowerKW = fixedRecievedChargeKW / TimeWarp.fixedDeltaTime;
            }
            else
                recievedPowerKW = 0;

            bool hasExtraBoiloff = initializationCountdown == 0 && powerReqKW > 0 && recievedPowerKW < currentPowerReq && previousRecievedPowerKW < previousPowerReq;

            var boiloffReducuction = !hasExtraBoiloff
                    ? boilOffRate
                    : (boilOffRate + (boilOffAddition * (1 - recievedPowerKW / currentPowerReq)));

            boiloff = CheatOptions.IgnoreMaxTemperature ||  boiloffReducuction <= 0 
                ? 0
                : boiloffReducuction * environmentBoiloff;

            if (boiloff > 0.0000000001)
            {
                _cryostat_resource.amount = Math.Max(0, _cryostat_resource.amount - boiloff * TimeWarp.fixedDeltaTime);
                boiloffStr = boiloff.ToString("0.0000000") + " L/s " + _cryostat_resource.resourceName;

                if (hasExtraBoiloff && part.vessel.isActiveVessel && !warningShown)
                {
                    warningShown = true;
                    ScreenMessages.PostScreenMessage("Warning: " + boiloffStr + " Boiloff", 5, ScreenMessageStyle.UPPER_CENTER);
                }
            }
            else
            {
                warningShown = false;
                boiloffStr = "0.0000000 L/s " + _cryostat_resource.resourceName;
            }

            previousPowerReq = currentPowerReq;
            previousRecievedPowerKW = recievedPowerKW;
        }

        public override string getResourceManagerDisplayName()
        {
            return resourceGUIName + " Cryostat";
        }

        public override int getPowerPriority()
        {
            return 2;
        }

        public override string GetInfo()
        {
            return "Power Requirements: " + (powerReqKW * 0.1).ToString("0.0") + " KW\n Powered Boil Off Fraction: " 
                + boilOffRate * PluginHelper.SecondsInDay + " /day\n Unpowered Boil Off Fraction: " + (boilOffRate + boilOffAddition) * boilOffMultiplier * PluginHelper.SecondsInDay + " /day";
        }
    }
}

