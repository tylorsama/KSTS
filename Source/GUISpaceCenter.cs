using System;
using UnityEngine;
using ClickThroughFix;

namespace KSTS
{



    // Helper-Class to draw the window in the space-center scene:
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class GUISpaceCenter : MonoBehaviour
    {
        static int winId;

        public void Start()
        {
            Mission.InitKAC();
            winId = SpaceTuxUtility.WindowHelper.NextWindowId("KSPS.GUISpaceCenter");
        }

        public void OnGUI()
        {
            if (GUI.showGui)
            {
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
                Debug.LogError("KSTSGUISpaceCenter.OnWindow(): " + e.ToString());
            }
        }
    }
}
