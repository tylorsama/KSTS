using System;
using System.Collections.Generic;

namespace KSTS
{
    // Closed-form Hohmann transfer-window helper for interplanetary deliveries (no Lambert/porkchop solver):
    // the departure phase angle has a closed form and the current phase comes from stock body positions.
    // Angles are in degrees, times in seconds of universal time.
    public static class TransferWindow
    {
        // Resolves the two bodies whose alignment defines the window by walking up referenceBody from both the
        // launch body and the target to their common parent. Returns false when the target is a moon of the launch
        // body (no real window - reachable almost any time); a/b are the two children under the common parent.
        public static bool IsInterplanetary(CelestialBody launch, CelestialBody target, out CelestialBody a, out CelestialBody b)
        {
            a = launch; b = target;
            if (launch == null || target == null || launch == target) return false;

            var launchChain = new List<CelestialBody>();
            for (var c = launch; c != null; c = c.referenceBody) { launchChain.Add(c); if (c.referenceBody == c) break; }

            for (var c = target; c != null && c.referenceBody != c; c = c.referenceBody)
            {
                int idx = launchChain.IndexOf(c.referenceBody);
                if (idx < 0) continue;      // common parent not reached yet, keep climbing
                if (idx == 0) return false;  // common parent IS the launch body -> target is its moon
                a = launchChain[idx - 1];    // launch-side child under the common parent
                b = c;                       // target-side child under the common parent
                return a != b;
            }
            return false;
        }

        public static bool IsInterplanetary(CelestialBody launch, CelestialBody target)
            => IsInterplanetary(launch, target, out _, out _);
        
        // Required phase of b ahead of a at departure for a Hohmann transfer:
        public static double RequiredPhaseAngle(CelestialBody a, CelestialBody b)
        {
            double r1 = a.orbit.semiMajorAxis, r2 = b.orbit.semiMajorAxis;
            return Normalize360(180.0 * (1.0 - Math.Pow((r1 + r2) / (2.0 * r2), 1.5)));
        }

        // Current phase of b relative to a in the common parent's reference plane (Zup: X-Y plane, +Z north):
        public static double CurrentPhaseAngle(CelestialBody a, CelestialBody b, double ut)
        {
            var pa = a.orbit.getRelativePositionAtUT(ut);
            var pb = b.orbit.getRelativePositionAtUT(ut);
            double la = Math.Atan2(pa.y, pa.x) * (180.0 / Math.PI);
            double lb = Math.Atan2(pb.y, pb.x) * (180.0 / Math.PI);
            return Normalize360(lb - la);
        }

        public static double SynodicPeriod(CelestialBody a, CelestialBody b)
        {
            return 1.0 / Math.Abs(1.0 / a.orbit.period - 1.0 / b.orbit.period);
        }

        // Seconds until the phase angle next matches the required one (small value = alignment is near):
        public static double TimeToNextWindow(CelestialBody a, CelestialBody b, double now)
        {
            double phi = CurrentPhaseAngle(a, b, now), theta = RequiredPhaseAngle(a, b);
            double ratePerSec = 360.0 / SynodicPeriod(a, b);
            // The inner body (shorter period) moves faster, so its lead (phi = Lb - La) decreases over time:
            double delta = a.orbit.period < b.orbit.period ? Normalize360(phi - theta) : Normalize360(theta - phi);
            return delta / ratePerSec;
        }

        public static bool IsWindowOpen(CelestialBody a, CelestialBody b, double now, double toleranceDeg)
        {
            return Math.Abs(NormalizeSigned(CurrentPhaseAngle(a, b, now) - RequiredPhaseAngle(a, b))) <= toleranceDeg;
        }

        private static double Normalize360(double a) { a %= 360.0; return a < 0 ? a + 360.0 : a; }
        private static double NormalizeSigned(double a) { a = Normalize360(a); return a > 180.0 ? a - 360.0 : a; }
    }
}
