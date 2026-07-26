using System.Linq;

namespace KSTS
{
    // Loads mod-wide tuning values from GameData/KSTS/settings.cfg (node "KSTS_SETTINGS").
    // Values are read once at startup; the defaults below are used if the file or a key is missing.
    public static class KSTSSettings
    {
        // Tolerance bands for delivery orbits around a body other than the launch body:
        public static double ToleranceInclination = 2.0;    // degrees around the recorded inclination
        public static double ToleranceEccentricity = 0.02;  // around the recorded eccentricity

        // Deploy-altitude ceiling is capped this many meters below the destination body's SoI edge:
        public static double CeilingSoiMargin = 1000;        // meters

        public static void Load()
        {
            var node = GameDatabase.Instance.GetConfigNodes("KSTS_SETTINGS").FirstOrDefault();
            if (node == null) return;
            if (node.HasValue("toleranceInclination")) ToleranceInclination = double.Parse(node.GetValue("toleranceInclination"));
            if (node.HasValue("toleranceEccentricity")) ToleranceEccentricity = double.Parse(node.GetValue("toleranceEccentricity"));
            if (node.HasValue("ceilingSoiMargin")) CeilingSoiMargin = double.Parse(node.GetValue("ceilingSoiMargin"));
        }
    }
}
