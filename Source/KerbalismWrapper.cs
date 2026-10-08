using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace KSTS
{
    // TODO: propose Kerbalism API getters — GetRules() / GetProcesses() / GetPartProcesses(AvailablePart)
    // that expose rule + process config so it wiil be possible to pre-flight-compute per-resource L balance for a ship template.
    // When available: replace the DEPLOY warning in GUICrewTransferSelector with a real preview + Start gate.
    public class KerbalismWrapper
    {
        static KerbalismWrapper instance = null;
        static readonly object padlock = new object();


        private Type apiType; // KERBALISM.API
        private MethodInfo miConsumeResource;
        private MethodInfo miResourceAmount;
        private MethodInfo miResourceAverageRate;
        private MethodInfo miKill;

        public bool Present { get; private set; }


        private KerbalismWrapper()
        {
            try
            {
                var loaded = AssemblyLoader.loadedAssemblies
                    .FirstOrDefault(a => a.name == "Kerbalism");
                if (loaded == null)
                {
                    Debug.Log("[KSTS] KerbalismWrapper: Kerbalism assembly not found — LS-drain disabled");
                    return;
                }

                apiType = loaded.assembly.GetType("KERBALISM.API");
                if (apiType == null)
                {
                    Debug.LogWarning("[KSTS] KerbalismWrapper: Kerbalism assembly present, but KERBALISM.API type missing — LS-drain disabled");
                    return;
                }

                const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
                miConsumeResource = apiType.GetMethod("ConsumeResource", flags, null,
                    new[] { typeof(Vessel), typeof(string), typeof(double), typeof(string) }, null);
                miResourceAmount = apiType.GetMethod("ResourceAmount", flags, null,
                    new[] { typeof(Vessel), typeof(string) }, null);
                miResourceAverageRate = apiType.GetMethod("ResourceAverageRate", flags, null,
                    new[] { typeof(Vessel), typeof(string) }, null);
                miKill = apiType.GetMethod("Kill", flags, null,
                    new[] { typeof(Vessel), typeof(ProtoCrewMember) }, null);

                Present = miConsumeResource != null
                          && miResourceAmount != null
                          && miResourceAverageRate != null
                          && miKill != null;

                if (Present)
                    Debug.Log($"[KSTS] KerbalismWrapper: initialised, API resolved from {loaded.assembly.GetName().Name} {loaded.assembly.GetName().Version}");
                else
                    Debug.LogWarning(
                        $"[KSTS] KerbalismWrapper: Kerbalism assembly found, but one of the expected API methods is missing " +
                        $"(ConsumeResource={miConsumeResource != null}, ResourceAmount={miResourceAmount != null}, " +
                        $"ResourceAverageRate={miResourceAverageRate != null}, Kill={miKill != null})");
            }
            catch (Exception e)
            {
                Debug.LogError("[KSTS] KerbalismWrapper init failed: " + e);
                Present = false;
            }
        }


        public static KerbalismWrapper Instance
        {
            get
            {
                lock (padlock)
                {
                    if (instance == null)
                    {
                        instance = new KerbalismWrapper();
                    }

                    return instance;
                }
            }
        }

        public void ConsumeResource(Vessel v, string resource, double amount, string title)
        {
            if (!Present) return;
            try
            {
                miConsumeResource.Invoke(null, new object[] { v, resource, amount, title });
            }
            catch (Exception e)
            {
                Debug.LogError("[KSTS] KerbalismWrapper.ConsumeResource failed: " + e);
            }
        }

        public double? ResourceAmount(Vessel v, string resource)
        {
            if (!Present) return null;
            try
            {
                return (double)miResourceAmount.Invoke(null, new object[] { v, resource });
            }
            catch (Exception e)
            {
                Debug.LogError("[KSTS] KerbalismWrapper.ResourceAmount failed: " + e);
                return 0.0;
            }
        }

        public double? ResourceAverageRate(Vessel v, string resource)
        {
            if (!Present) return null;
            try
            {
                return (double)miResourceAverageRate.Invoke(null, new object[] { v, resource });
            }
            catch (Exception e)
            {
                Debug.LogError("[KSTS] KerbalismWrapper.ResourceAverageRate failed: " + e);
                return 0.0;
            }
        }

        public void Kill(Vessel v, ProtoCrewMember crew)
        {
            if (!Present) return;
            try
            {
                miKill.Invoke(null, new object[] { v, crew });
            }
            catch (Exception e)
            {
                Debug.LogError("[KSTS] KerbalismWrapper.Kill failed: " + e);
            }
        }
    }
}