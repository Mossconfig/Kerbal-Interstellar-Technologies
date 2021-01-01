﻿using System;
using KSP.Localization;
using UnityEngine;

namespace KIT.Science 
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class FNImpactorModule : MonoBehaviour 
    {
        protected double lastImpactTime;

        public void Awake() 
        {
            lastImpactTime = 0d;

            // Register collision handler with GameEvents.onCollision
            GameEvents.onCollision.Add(onVesselAboutToBeDestroyed);
            GameEvents.onCrash.Add(onVesselAboutToBeDestroyed);
            Debug.Log("[KSPI]: FNImpactorModule listening for collisions.");
        }

        public void onVesselAboutToBeDestroyed(EventReport report) 
        {
            Debug.Log("[KSPI]: Handling Impactor");

            // Do nothing if we don't have a origin.  This seems improbable, but who knows.
            if (report == null)
            {
                Debug.Log("[KSPI]: Impactor: Ignored because the report is undefined.");
                return;
            }

            // Do nothing if we don't have a origin.  This seems improbable, but who knows.
            if (report.origin == null)
            {
                Debug.Log("[KSPI]: Impactor: Ignored because the origin is undefined.");
                return;
            }

            // Do nothing if we don't have a vessel.  This seems improbable, but who knows.
            if (report.origin.vessel == null)
            {
                Debug.Log("[KSPI]: Impactor: Ignored because the vessel is undefined.");
                return;
            }

            Vessel vessel = report.origin.vessel;
            int science_experiment_number = 0;
            string vessel_impact_node_string = string.Concat("IMPACT_", vessel.id.ToString());
            string vessel_seismic_node_string = string.Concat("SEISMIC_SCIENCE_", vessel.mainBody.name.ToUpper());

            // Do nothing if we have recorded an impact less than 10 physics updates ago.  This probably means this call
            // is a duplicate of a previous call.
            if (Planetarium.GetUniversalTime() - lastImpactTime < TimeWarp.fixedDeltaTime * 10f) 
            {
                Debug.Log("[KSPI]: Impactor: Ignored because we've just recorded an impact.");
                return;
            }

            // Do nothing if we are a debris item less than ten physics-updates old.  That probably means we were
            // generated by a recently-recorded impact.
            if (vessel.vesselType == VesselType.Debris && vessel.missionTime < Time.fixedDeltaTime * 10f) 
            {
                Debug.Log("[KSPI]: Impactor: Ignored due to vessel being brand-new debris.");
                return;
            }

            // Do nothing if we aren't very near the terrain.  Note that using heightFromTerrain probably allows
            // impactors against the ocean floor... good luck.
            double vesselDimension = vessel.MOI.magnitude / vessel.GetTotalMass();
            if (vessel.heightFromSurface > Math.Max(vesselDimension, 0.75f)) 
            {
                Debug.Log("[KSPI]: Impactor: Ignored due to vessel altitude being too high.");
                return;
            }

            // Do nothing if we aren't impacting the surface.
            if (!(
                report.other.ToLower().Contains(string.Intern("surface")) ||
                report.other.ToLower().Contains(string.Intern("terrain")) ||
                report.other.ToLower().Contains(vessel.mainBody.name.ToLower())
            )) 
            {
                Debug.Log("[KSPI]: Impactor: Ignored due to not impacting the surface.");
                return;
            }

            /* 
             * NOTE: This is a deviation from current KSPI behavior.  KSPI currently registers an impact over 40 m/s
             * regardless of its mass; this means that trivially light impactors (single instruments, even) could
             * trigger the experiment.
             * 
             * The guard below requires that the impactor have at least as much vertical impact energy as a 1 Mg
             * object traveling at 40 m/s.  This means that nearly-tangential impacts or very light impactors will need
             * to be much faster, but that heavier impactors may be slower.
             * 
             * */
            if ((Math.Pow(vessel.verticalSpeed, 2d) * vessel.GetTotalMass() / 2d < 800d) && vessel.verticalSpeed > 20d) 
            {
                Debug.Log("[KSPI]: Impactor: Ignored due to vessel imparting too little impact energy.");
                return;
            }

            ConfigNode scienceNode;
            ConfigNode config = PluginHelper.GetPluginSaveFile();
            if (config.HasNode(vessel_seismic_node_string)) 
            {
                scienceNode = config.GetNode(vessel_seismic_node_string);
                science_experiment_number = scienceNode.nodes.Count;

                if (scienceNode.HasNode(vessel_impact_node_string)) 
                {
                    Debug.Log("[KSPI]: Impactor: Ignored because this vessel's impact has already been recorded.");
                    return;
                }
            } 
            else 
            {
                Debug.Log("[KSPI]: Impactor: Created sysmic impact data node");
                scienceNode = config.AddNode(vessel_seismic_node_string);
                scienceNode.AddValue("name", "interstellarseismicarchive");
            }

            int body = vessel.mainBody.flightGlobalsIndex;
            Vector3d netVector = Vector3d.zero;
            bool first = true;
            double distributionFactor = 0;

            foreach (Vessel confVess in FlightGlobals.Vessels) 
            {
                string vesselProbeNodeString = string.Concat("VESSEL_SEISMIC_PROBE_", confVess.id.ToString());

                if (config.HasNode(vesselProbeNodeString)) 
                {
                    ConfigNode probeNode = config.GetNode(vesselProbeNodeString);

                    // If the seismometer is inactive, skip it.
                    if (probeNode.HasValue("is_active")) 
                    {
                        bool.TryParse(probeNode.GetValue("is_active"), out var is_active);
                        if (!is_active) continue;
                    }

                    // If the seismometer is on another planet, skip it.
                    if (probeNode.HasValue("celestial_body")) 
                    {
                        int.TryParse(probeNode.GetValue("celestial_body"), out int planet);
                        if (planet != body) continue;
                    }

                    // do sciency stuff
                    Vector3d surfaceVector = (confVess.transform.position - FlightGlobals.Bodies[body].transform.position);
                    surfaceVector = surfaceVector.normalized;
                    if (first) 
                    {
                        first = false;
                        netVector = surfaceVector;
                        distributionFactor = 1;
                    } 
                    else 
                    {
                        distributionFactor += 1.0 - Vector3d.Dot(surfaceVector, netVector.normalized);
                        netVector = netVector + surfaceVector;
                    }
                }
            }

            distributionFactor = Math.Min(distributionFactor, 3.5); // no more than 3.5x boost to science by using multiple detectors
            if (distributionFactor > 0 && !double.IsInfinity(distributionFactor) && !double.IsNaN(distributionFactor)) 
            {
                ScreenMessages.PostScreenMessage(Localizer.Format("#LOC_KSPIE_ImpactorModule_Postmsg"), 5f, ScreenMessageStyle.UPPER_CENTER);//"Impact Recorded, science report can now be accessed from one of your accelerometers deployed on this body."
                lastImpactTime = Planetarium.GetUniversalTime();
                Debug.Log("[KSPI]: Impactor: Impact registered!");

                ConfigNode impact_node = new ConfigNode(vessel_impact_node_string);
                impact_node.AddValue(string.Intern("transmitted"), bool.FalseString);
                impact_node.AddValue(string.Intern("vesselname"), vessel.vesselName);
                impact_node.AddValue(string.Intern("distribution_factor"), distributionFactor);
                scienceNode.AddNode(impact_node);

                config.Save(PluginHelper.PluginSaveFilePath);
            }
        }
    }
}
