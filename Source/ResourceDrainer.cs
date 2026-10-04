using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KSTS
{
    public class ResourceDrainer
    {
        private const int MaxRetries = 200; // ~200 сек ожидания, пока Kerbalism обработает vessel
        private static double MaxAgeUT => KSPUtil.dateTimeFormatter.Year * KSTSSettings.PendingDrainMaxAgeYears;

        public class PendingDrain : Saveable
        {
            public Guid vesselId;
            public double duration;
            public int crewCount;
            public int retries = 0;
            public double scheduledAtUT = -1.0; // UT at schedule time; -1 = legacy save without this field
        }

        private static List<PendingDrain> pendingDrains = new List<PendingDrain>();
        
        public static void Timer()
        {
            if (!KerbalismWrapper.Instance.Present) return;
            if (pendingDrains.Count == 0) return;

            var done = new List<PendingDrain>();

            foreach (var drain in pendingDrains)
            {
                var vessel = FlightGlobals.Vessels.FirstOrDefault(v => v.id == drain.vesselId);
                if (vessel == null)
                {
                    Debug.LogWarning($"[KSTS] ResourceDrainer: vessel {drain.vesselId} not found, dropping drain (duration={drain.duration:F0}s, crew={drain.crewCount})");
                    done.Add(drain);
                    continue;
                }

                if (drain.scheduledAtUT >= 0 && Planetarium.GetUniversalTime() - drain.scheduledAtUT > MaxAgeUT)
                {
                    Debug.LogWarning($"[KSTS] ResourceDrainer: vessel {vessel.vesselName} ({vessel.id}) — drain older than {MaxAgeUT:F0}s of game time, dropping (player never approached the ship)");
                    done.Add(drain);
                    continue;
                }

                // Kerbalism populates ResourceAverageRate only on the loaded path (vd.Evaluate → Profile.Execute → resources.Sync).
                // For unloaded vessels rates stay at 0, so waiting/retrying is pointless until the player brings the vessel into physics range.
                // Skip without spending a retry — the drain stays in queue and is retried after the vessel becomes loaded.
                if (!vessel.loaded) continue;

                var resourceNames = vessel.protoVessel.protoPartSnapshots
                    .SelectMany(ps => ps.resources)
                    .Select(r => r.resourceName)
                    .Distinct()
                    .ToList();

                var rates = resourceNames
                    .Select(n => new { name = n, rate = KerbalismWrapper.Instance.ResourceAverageRate(vessel, n) ?? 0.0 })
                    .ToList();

                // drain was created when crew count > 0, so some drain rates always >0 if ship is initialised
                var activeRates = rates.Where(x => x.rate != 0.0).ToList();

                if (activeRates.Count == 0)
                {
                    if (drain.retries < MaxRetries)
                    {
                        drain.retries++;
                        continue;
                    }
                    Debug.LogWarning($"[KSTS] ResourceDrainer: vessel {vessel.vesselName} ({vessel.id}) — no non-zero rates after {MaxRetries} ticks, drain skipped. Ship keeps full resources. Resources checked: [{string.Join(", ", resourceNames.ToArray())}]");
                    done.Add(drain);
                    continue;
                }

                Debug.Log($"[KSTS] ResourceDrainer: draining vessel {vessel.vesselName} ({vessel.id}), duration={drain.duration:F0}s, crew={drain.crewCount}, retries used={drain.retries}");
                // TODO: NET rate ignores recycler/feedback dynamics — closed-loop LS gets under-drained. Needs API getters on kerbalism side 
                foreach (var activeRate in activeRates)
                {
                    var amount = -activeRate.rate * drain.duration;
                    Debug.Log($"[KSTS] ResourceDrainer:   {activeRate.name}: rate={activeRate.rate:G4}/s → consuming {amount:G4}");
                    KerbalismWrapper.Instance.ConsumeResource(vessel, activeRate.name, amount, "KSTS transit");
                }
                
                done.Add(drain);
            }

            foreach (var d in done) pendingDrains.Remove(d);
        }
        
        public static void ScheduleResourceDrain(Vessel vessel, double duration, int crewCount)
        {
            if (!KerbalismWrapper.Instance.Present)
            {
                Debug.Log($"[KSTS] ResourceDrainer: Kerbalism not present, skipping drain scheduling for vessel {vessel.vesselName} ({vessel.id})");
                return;
            }
            pendingDrains.Add(new PendingDrain
            {
                vesselId = vessel.id,
                duration = duration,
                crewCount = crewCount,
                scheduledAtUT = Planetarium.GetUniversalTime(),
            });
            Debug.Log($"[KSTS] ResourceDrainer: scheduled drain for vessel {vessel.vesselName} ({vessel.id}), duration={duration:F0}s, crew={crewCount}, queue size={pendingDrains.Count}");
        }

        public static void Save(ConfigNode node)
        {
            var root = node.AddNode("PendingDrains");
            foreach (var d in pendingDrains)
                root.AddNode(d.CreateConfigNode("PendingDrain"));
            if (pendingDrains.Count > 0)
                Debug.Log($"[KSTS] ResourceDrainer: saved {pendingDrains.Count} pending drain(s)");
        }

        public static void Load(ConfigNode node)
        {
            pendingDrains.Clear();
            var root = node.GetNode("PendingDrains");
            if (root == null) return;
            foreach (var n in root.GetNodes("PendingDrain"))
            {
                var d = new PendingDrain();
                Saveable.CreateFromConfigNode(n, d);
                pendingDrains.Add(d);
            }
            if (pendingDrains.Count > 0)
                Debug.Log($"[KSTS] ResourceDrainer: loaded {pendingDrains.Count} pending drain(s) from save");
        }
    }
}