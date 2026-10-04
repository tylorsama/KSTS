using System;
using UnityEngine;
using ClickThroughFix;

namespace KSTS
{
    // Helper-Class to draw the window in the flight scene:
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class GUIFlight : MonoBehaviour
    {
        static int winId;

        public void Start()
        {
            Mission.InitKAC();
            winId = SpaceTuxUtility.WindowHelper.NextWindowId("KSPS.GUIFlight");
        }
        public void OnGUI()
        {
            if (GUI.showGui)
            {
                GUI.FitWindowToContent();
                var previousMatrix = GUI.BeginScale();
                GUI.windowPosition = ClickThruBlocker.GUILayoutWindow(winId, GUI.windowPosition, OnWindow, ""); //, GUI.windowStyle);
                GUI.EndScale(previousMatrix);
            }
        }

        private void OnWindow(int windowId)
        {
            try
            {
                GUI.DrawWindow();
            }
            catch (Exception e)
            {
                Debug.LogError("KSTSGUIFlight.OnWindow(): " + e.ToString());
            }
        }
    }
}
