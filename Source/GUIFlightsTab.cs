using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSP.Localization;


namespace KSTS
{
    class GUIFlightsTab
    {
        private static Vector2 scrollPos = Vector2.zero;

        public static void Display()
        {
            scrollPos = GUI.BeginAutoScroll("GUIFlightsTab:15", scrollPos);
            if (MissionController.missions.Count == 0)
            {
                GUILayout.Label("<b>No active missions.</b>");
            }
            else
            {
                var contents = new List<GUIContent>();
                MissionController.missions.Sort((x, y) => x.eta.CompareTo(y.eta)); // Sort list by ETA
                foreach (var mission in MissionController.missions)
                {
                    var missionVesselName = "";
                    if (mission.GetProfile() != null) missionVesselName = mission.GetProfile().vesselName;
                    contents.Add(new GUIContent(mission.GetDescription(), GUI.GetVesselThumbnail(missionVesselName)));
                }
                
                GUILayout.SelectionGrid(-1, contents.ToArray(), 1, GUI.selectionGridStyle);
            }
            GUI.EndAutoScroll("GUIFlightsTab:15");
        }
    }
}
