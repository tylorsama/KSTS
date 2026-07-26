using System.Linq;

namespace KSTS
{
    // Mod-wide tuning values, read once at startup from the "KSTS_SETTINGS" node (any .cfg under GameData).
    // Defaults below are used if the node or a key is missing.
    public static class KSTSSettings
    {
        // Tolerance bands for delivery orbits around a body other than the launch body:
        public static double ToleranceInclination = 2.0;    // degrees around the recorded inclination
        public static double ToleranceEccentricity = 0.02;  // around the recorded eccentricity
        public static double ToleranceLAN = 2.0;             // degrees around the recorded ascending node

        // Deploy-altitude ceiling is capped this many meters below the destination body's SoI edge:
        public static double CeilingSoiMargin = 1000;        // meters

        // A payload part counts as "used" only if a massed resource drained more than this fraction of its
        // capacity during the recording (keeps slow life-support drain of empty crew pods from disqualifying them):
        public static double UsedPartResourceThreshold = 0.01;  // fraction of capacity (0.01 = 1%)

        // Global scale of the KSTS window (1.0 = default KSP size), clamped to a usable range on load:
        public static double UiScale = 1.0;

        // Maximum age of a queued Kerbalism-drain (in-game years) before it is dropped without draining
        // — reached when the player never brings the deployed ship into physics range for that long:
        public static double PendingDrainMaxAgeYears = 2.0;

        public static void Load()
        {
            var node = GameDatabase.Instance.GetConfigNodes("KSTS_SETTINGS").FirstOrDefault();
            if (node == null) return;
            if (node.HasValue("toleranceInclination")) ToleranceInclination = double.Parse(node.GetValue("toleranceInclination"));
            if (node.HasValue("toleranceEccentricity")) ToleranceEccentricity = double.Parse(node.GetValue("toleranceEccentricity"));
            if (node.HasValue("toleranceLAN")) ToleranceLAN = double.Parse(node.GetValue("toleranceLAN"));
            if (node.HasValue("ceilingSoiMargin")) CeilingSoiMargin = double.Parse(node.GetValue("ceilingSoiMargin"));
            if (node.HasValue("usedPartResourceThreshold")) UsedPartResourceThreshold = double.Parse(node.GetValue("usedPartResourceThreshold"));
            if (node.HasValue("uiScale")) UiScale = double.Parse(node.GetValue("uiScale"));
            if (node.HasValue("pendingDrainMaxAgeYears")) PendingDrainMaxAgeYears = double.Parse(node.GetValue("pendingDrainMaxAgeYears"));
            if (UiScale < 0.5) UiScale = 0.5;
            else if (UiScale > 3.0) UiScale = 3.0;
            if (PendingDrainMaxAgeYears < 0) PendingDrainMaxAgeYears = 0;
        }
    }
}
