using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.Localization;

namespace KSTS
{
    class GUITargetVesselSelector
    {
        private Vector2 scrollPos = Vector2.zero;
        private int selectedIndex = -1;
        private string nameSearch = "";
        public Vessel targetVessel = null;

        public VesselType? filterVesselType = null;
        public string filterHasCrewTrait = null;
        public int? filterHasCrewTraitCount = null;

        // Displays the currently selected target-vessel and returns true, if the player has deselected it:
        public bool DisplaySelected()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("<size=14><b>Target:</b></size>", new GUIStyle(GUI.labelStyle) { stretchWidth = true });
            if (GUILayout.Button("<size=14><color=#F9FA86><b>" + Localizer.Format(targetVessel.vesselName) + "</b></color> (Apoapsis: " + GUI.FormatAltitude(targetVessel.orbit.ApA) + ")</size>", new GUIStyle(GUI.buttonStyle) { alignment = TextAnchor.MiddleRight, stretchWidth = false, fixedWidth = 320 }))
            {
                selectedIndex = -1;
                targetVessel = null;
            }
            GUILayout.EndHorizontal();
            return targetVessel == null;
        }

        // Shows a list of all available target-vessels and returns true, if the player has selected one:
        public bool DisplayList()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("<size=14><b>Target:</b></size>");
            GUILayout.FlexibleSpace();
            GUILayout.Label("Filter:");
            nameSearch = GUILayout.TextField(nameSearch, GUILayout.Width(200));
            GUILayout.EndHorizontal();
            scrollPos = GUILayout.BeginScrollView(scrollPos, GUI.scrollStyle);
            var green = "#00FF00";
            var red = "#FF0000";

            // Build a list with all valid vessels:
            var validTargets = new List<Vessel>();
            foreach (var vessel in FlightGlobals.Vessels)
            {
                if (!TargetVessel.IsValidTarget(vessel)) continue;
                validTargets.Add(vessel);
            }

            // Filter by the vessel-name search box (covers both target flavours: transport and construction).
            // Matches vessel name AND the name of the body it orbits, so "duna" surfaces every ship around Duna.
            if (this.nameSearch != "")
            {
                string f = nameSearch.ToLower();
                validTargets = validTargets.Where(v =>
                    Localizer.Format(v.vesselName).ToLower().Contains(f)
                    || (v.mainBody != null && Localizer.Format(v.mainBody.bodyName).ToLower().Contains(f))
                ).ToList();
            }

            if (selectedIndex >= validTargets.Count)
            {
                selectedIndex = -1;
                targetVessel = null;
            }

            if (validTargets.Count == 0)
            {
                // No valid targest available:
                GUILayout.Label("No valid targets in orbit.");
            }
            else
            {
                // Show list with all possible vessels:
                var contents = new List<GUIContent>();
                var filteredIndices = new List<int>(); // Target-vessels which fall out of the defined filters will get noted here.
                var index = 0;
                for (int i = 0; i < validTargets.Count; i++)
                {
                    var vessel = validTargets[i];
                
                    var filterThisTarget = false;
                    if (!TargetVessel.IsValidTarget(vessel)) continue;
                    var descriptions = new List<string>();
                    descriptions.Add("<color=#F9FA86><b>" + Localizer.Format(vessel.vesselName) + "</b></color><color=#FFFFFF>");

                    // Orbital-Parameters:
                    var bodyName = vessel.mainBody != null ? Localizer.Format(vessel.mainBody.bodyName) : "?";
                    descriptions.Add("<b>Body:</b> " + bodyName + ", <b>Apoapsis:</b> " + GUI.FormatAltitude(vessel.orbit.ApA) + ", <b>Periapsis:</b> " + GUI.FormatAltitude(vessel.orbit.PeA) + ", <b>MET:</b> " + GUI.FormatDuration(vessel.missionTime));

                    // Docking-Port Types:
                    var dockingPortsTranslated = new List<string>();
                    foreach (var dockingPortType in TargetVessel.GetVesselDockingPortTypes(vessel)) dockingPortsTranslated.Add(TargetVessel.TranslateDockingPortName(dockingPortType));
                    if (dockingPortsTranslated.Count > 0) descriptions.Add("<b>Docking-Ports:</b> " + string.Join(", ", dockingPortsTranslated.ToArray()));

                    // Resources:
                    double capacity = 0;
                    foreach(var availableResource in TargetVessel.GetFreeResourcesCapacities(vessel)) capacity += availableResource.amount * availableResource.mass;
                    if (capacity > 0) descriptions.Add("<b>Resource Capacity:</b> " + capacity.ToString("#,##0.00t"));

                    // Crew:
                    var seats = TargetVessel.GetCrewCapacity(vessel);
                    var crew = TargetVessel.GetCrew(vessel).Count;
                    if (seats > 0) descriptions.Add("<b>Crew:</b> " + crew.ToString() + "/" + seats.ToString());

                    // Maybe apply additional filters and show their attributes:
                    var filterAttributes = new List<string>(); ;
                    if (filterVesselType != null)
                    {
                        var isValidType = vessel.vesselType == (VesselType)filterVesselType;
                        var color = isValidType ? green : red;
                        filterAttributes.Add("<b>Type:</b> <color=" + color + ">" + vessel.vesselType.ToString() + "</color>");
                        if (!isValidType) filterThisTarget = true;
                    }
                    if (filterHasCrewTrait != null)
                    {
                        var traitCount = TargetVessel.GetCrewCountWithTrait(vessel, filterHasCrewTrait);
                        var requiredCount = 1;
                        if (filterHasCrewTraitCount != null) requiredCount = (int) filterHasCrewTraitCount;
                        var color = traitCount >= requiredCount ? green : red;
                        filterAttributes.Add("<b>" + filterHasCrewTrait + "s:</b> <color=" + color + ">" + traitCount.ToString() + "/"+ requiredCount.ToString() + "</color>");
                        if (traitCount < requiredCount) filterThisTarget = true;
                    }
                    if (filterAttributes.Count > 0) descriptions.Add(String.Join(" ", filterAttributes.ToArray()));

                    contents.Add(new GUIContent(String.Join("\n", descriptions.ToArray()) + "</color>"));
                    if (filterThisTarget) filteredIndices.Add(index); // If there were filters, which did not match, we still show the target, but don't allow to select it.
                    index++;
                }

                var newSelection = GUILayout.SelectionGrid(selectedIndex, contents.ToArray(), 1, GUI.selectionGridStyle);
                if (newSelection >= 0 && !filteredIndices.Contains(newSelection))
                {
                    // The player has selected a payload:
                    selectedIndex = newSelection;
                    targetVessel = validTargets[selectedIndex];
                }
            }
            GUILayout.EndScrollView();
            return targetVessel != null;
        }
    }
}
