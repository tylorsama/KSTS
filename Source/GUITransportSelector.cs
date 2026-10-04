using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KSTS
{
    public class CrewTransferOrder
    {
        public enum CrewTransferDirection { DELIVER = 1, COLLECT = 2 };
        public string kerbalName;
        public CrewTransferDirection direction;
    }

    public class GUITransportSelector
    {
        public List<PayloadResource> selectedResources = null;
        public List<CrewTransferOrder> selectedCrewTransfers = null;

        private Vector2 scrollPos = Vector2.zero;
        private List<GUIRichValueSelector> resourceSelectors = null;
        private List<PayloadResource> availableResources = null;
        private MissionProfile missionProfile = null;
        private Vessel targetVessel = null;
        private int selectedTransportType = 0;
        private GUICrewTransferSelector crewTransferSelector = null;

        public GUITransportSelector(Vessel targetVessel, MissionProfile missionProfile)
        {
            this.targetVessel = targetVessel;
            this.missionProfile = missionProfile;
            resourceSelectors = new List<GUIRichValueSelector>();
            availableResources = TargetVessel.GetFreeResourcesCapacities(targetVessel);
            foreach (var availablePayload in availableResources)
            {
                var selector = new GUIRichValueSelector(availablePayload.name, 0, "", 0, Math.Round(availablePayload.amount,2), true, "#,##0.00");
                resourceSelectors.Add(selector);
            }
            crewTransferSelector = new GUICrewTransferSelector(targetVessel, missionProfile);
        }

        enum TransportTypes { Resources = 0, Crew = 1 };
        static string[] transportTypeStrings = new string[] { "Resources", "Crew" };
        // Shows a list of all available payload-resources the player can choose from:
        public bool DisplayList()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("<size=14><b>Cargo:</b></size>");
           
            selectedTransportType = GUILayout.Toolbar(selectedTransportType, transportTypeStrings);
            GUILayout.EndHorizontal();

            scrollPos = GUI.BeginAutoScroll("GUITransportSelector:54", scrollPos);
            if (selectedTransportType == (int)TransportTypes.Resources)
            {
                // Transport Resources:
                selectedCrewTransfers = null;
                if (resourceSelectors.Count == 0)
                {
                    GUILayout.Label("The selected target has no free capacity to receive resources.");
                }
                else
                {
                    // Show list with all possible payloads:
                    var index = 0;
                    double selectedMass = 0;
                    var currentlySelectedPayloads = new List<PayloadResource>();
                    foreach (var selector in resourceSelectors)
                    {
                        selector.Display();
                        var resource = availableResources[index];
                        var selected = resource.Clone();
                        selected.amount = selector.Value;
                        currentlySelectedPayloads.Add(selected);
                        selectedMass += selected.amount * selected.mass;
                        index++;
                    }

                    // Show total selected amount:
                    var textColor = "#00FF00";
                    if (selectedMass > missionProfile.payloadMass) textColor = "#FF0000";
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("<b>Selected Payload:</b>");
                    GUILayout.Label("<color=" + textColor + ">" + selectedMass.ToString("#,##0.00 t") + " / " + missionProfile.payloadMass.ToString("#,##0.00 t") + "</color>  ", new GUIStyle(GUI.labelStyle) { alignment = TextAnchor.MiddleRight });
                    GUILayout.EndHorizontal();

                    // If the selected mass falls in the range of the transport capacity, we can use the current selection for the mission:
                    if (selectedMass > 0 && selectedMass <= missionProfile.payloadMass) selectedResources = currentlySelectedPayloads;
                    else selectedResources = null;
                }
            }
            else
            {
                // Transport Crew:
                selectedResources = null;
                var validCrewSelection = crewTransferSelector.DisplayList();

                // If there is a valid selection, copy the selection:
                if (validCrewSelection && ( crewTransferSelector.crewToDeliver.Count > 0 || crewTransferSelector.crewToCollect.Count > 0))
                {
                    selectedCrewTransfers = new List<CrewTransferOrder>();
                    foreach (var name in crewTransferSelector.crewToDeliver) selectedCrewTransfers.Add(new CrewTransferOrder() { kerbalName = name, direction = CrewTransferOrder.CrewTransferDirection.DELIVER });
                    foreach (var name in crewTransferSelector.crewToCollect) selectedCrewTransfers.Add(new CrewTransferOrder() { kerbalName = name, direction = CrewTransferOrder.CrewTransferDirection.COLLECT });
                }
                else
                {
                    selectedCrewTransfers = null;
                }
            }

            GUI.EndAutoScroll("GUITransportSelector:54");
            return selectedResources != null || selectedCrewTransfers != null;
        }
    }
}
