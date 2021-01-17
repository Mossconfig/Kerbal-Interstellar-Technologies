﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KIT.Resources
{
    class MagneticFieldDefinitionsHandler
    {
        protected static Dictionary<string, MagneticFieldDefinition> MagneticFieldDefinitionsByName;

        public static MagneticFieldDefinition GetMagneticFieldDefinitionForBody(CelestialBody celestialBodyName)
        {
            return GetMagneticFieldDefinitionForBody(celestialBodyName.name);
        }

        public static MagneticFieldDefinition GetMagneticFieldDefinitionForBody(string celestialBodyName) // function for getting or creating Crustal composition
        {
            MagneticFieldDefinition magneticFieldDefinition;
            try
            {
                LoadMagneticFieldDefinition();

                // check if there's a composition for this body
                if (!MagneticFieldDefinitionsByName.TryGetValue(celestialBodyName, out magneticFieldDefinition))
                {
                    Debug.LogWarning("[KSPI]: Failed to find magneticFieldDefinition for: " + celestialBodyName);
                    magneticFieldDefinition = new MagneticFieldDefinition(celestialBodyName, 1); // create an object list for holding default multiplier
                    MagneticFieldDefinitionsByName.Add(celestialBodyName, magneticFieldDefinition);
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[KSPI]: Exception while retrieving MagneticFieldDefinition : " + ex);
                magneticFieldDefinition = new MagneticFieldDefinition(celestialBodyName, 1); // create an object list for holding default multiplier
                MagneticFieldDefinitionsByName.Add(celestialBodyName, magneticFieldDefinition);
            }
            return magneticFieldDefinition;
        }

        private static void LoadMagneticFieldDefinition()
        {
            try
            {
                if (MagneticFieldDefinitionsByName != null)
                    return;

                Debug.Log("[KSPI]: Start Loading Magnetic Field Definitions");

                ConfigNode magneticFieldDefinitionsRoot = GameDatabase.Instance.GetConfigNodes("MAGNETIC_FIELD_DEFINITION_KSPI").FirstOrDefault();

                if (magneticFieldDefinitionsRoot == null)
                {
                    Debug.LogWarning("[KSPI]: failed to find ConfigNodes MAGNETIC_FIELD_DEFINITION_KSPI");

                    // create empty dictionary
                    MagneticFieldDefinitionsByName = new Dictionary<string, MagneticFieldDefinition>();
                }
                else
                {
                    var magneticFieldDefinitionModels = magneticFieldDefinitionsRoot.nodes.Cast<ConfigNode>()
                        .Select(m => new MagneticFieldDefinition(m.GetValue("celestialBodyName"), double.Parse(m.GetValue("strengthMult")))).ToList();

                    Debug.Log("[KSPI]: found " + magneticFieldDefinitionModels.Count + " Magnetic Field Definitions");

                    MagneticFieldDefinitionsByName = magneticFieldDefinitionModels.ToDictionary(m => m.CelestialBodyName);
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[KSPI]: Exception while loading MagneticFieldDefinitions : " + ex);
            }

        }
    }
}
