using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSP.Localization;

namespace KSTS
{
    class GUIMissionProfileSelector
    {
        public const string SELECTED_DETAILS_ALTITUDE = "altitude";
        public const string SELECTED_DETAILS_PAYLOAD = "payload";

        private Vector2 scrollPos = Vector2.zero;
        private string nameSearch = "";
        private bool hideInvalid = true;
        public MissionProfile selectedProfile = null;

        public double? filterMass = null;
        public double? filterAltitude = null;
        public int? filterCrewCapacity = null;
        public bool? filterRoundTrip = null;
        public List<string> filterDockingPortTypes = null;
        public CelestialBody filterBody = null;
        public MissionProfileType? filterMissionType = null;
        public double? filterPeriapsis = null;    // target periapsis, for off-origin "recorded orbit or higher" check
        public double? filterInclination = null;  // target inclination, for the off-origin tolerance band
        public double? filterEccentricity = null; // target eccentricity, for the off-origin tolerance band
        public double? filterLAN = null;          // target longitude of ascending node, for the off-origin tolerance band

        // Makes sure that the cached settings are still valid (eg if the player has deleted the selected profile):
        private void CheckInternals()
        {
            if (!MissionController.missionProfiles.ContainsValue(selectedProfile))
            {
                selectedProfile = null;
            }
        }

        // Whether this selector has any filters on valid profiles
        private bool HasFilter()
        {
            return filterMass != null
                || filterAltitude != null
                || filterCrewCapacity != null
                || filterRoundTrip != null
                || filterDockingPortTypes != null
                || filterBody != null
                || filterMissionType != null
                || filterPeriapsis != null
                || filterInclination != null
                || filterEccentricity != null
                || filterLAN != null;
        }

        // Displays the currently selected mission-profile and returns true, if the player has deselected the profile:
        public bool DisplaySelected(string showDetails=SELECTED_DETAILS_ALTITUDE)
        {
            CheckInternals();
            if (this.selectedProfile == null) return true;
            GUILayout.BeginHorizontal();
            GUILayout.Label("<size=14><b>Mission Profile:</b></size>", new GUIStyle(GUI.labelStyle) { stretchWidth = true });

            var details = "N/A";
            if (showDetails == SELECTED_DETAILS_ALTITUDE) details = "Max Altitude: " + GUI.FormatAltitude(selectedProfile.maxAltitude);
            else if (showDetails == SELECTED_DETAILS_PAYLOAD) details = "Max Payload: " + this.selectedProfile.payloadMass.ToString("0.00t");
            if (GUILayout.Button("<size=14><color=#F9FA86><b>" + this.selectedProfile.profileName + "</b></color> ("+details+")</size>", new GUIStyle(GUI.buttonStyle) { alignment = TextAnchor.MiddleRight, stretchWidth = false, fixedWidth = 320 }))
            {
                this.selectedProfile = null; // Back to the previous selection
            }
            GUILayout.EndHorizontal();
            return this.selectedProfile == null;
        }

        // Shows a list of all available mission-profiles and returns true, if the player has selected one:
        public bool DisplayList()
        {
            CheckInternals();

            GUILayout.BeginHorizontal();
            GUILayout.Label("<size=14><b>Mission Profile:</b></size>");
            GUILayout.FlexibleSpace();
            GUILayout.Label("Filter:");
            nameSearch = GUILayout.TextField(nameSearch, GUILayout.Width(200));
            GUILayout.EndHorizontal();

            // Only show the "Hide Invalid" checkbox if there are actually filters that make profiles invalid
            if (this.HasFilter())
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                hideInvalid = GUILayout.Toggle(hideInvalid, "Hide Invalid");
                GUILayout.EndHorizontal();
            }

            scrollPos = GUILayout.BeginScrollView(scrollPos, GUI.scrollStyle);
            var green = "#00FF00";
            var red = "#FF0000";

            // Show a list with all possible mission-profiles:
            if (MissionController.missionProfiles.Count == 0)
            {
                GUILayout.Label("No recordings found, switch to a new vessel to start recording a mission.");
            }
            else
            {
                var contents = new List<GUIContent>();
                var displayedProfiles = new List<MissionProfile>(); // The list of profiles which are actually displayed
                var invalidIndices = new List<int>(); // Profiles which are not valid for the defined filters will get noted here.

                // Filter out the profiles that should be hidden
                IEnumerable<MissionProfile> profiles = MissionController.missionProfiles.Values;
                if (this.filterMissionType != null)
                {
                    // Always hide profiles that are not for the correct mission type, since there's no point in showing them
                    profiles = profiles.Where(prof => prof.missionType == this.filterMissionType);
                }
                if (this.nameSearch != "")
                {
                    // Filter out profiles that don't match the search box
                    string f = nameSearch.ToLower();
                    profiles = profiles.Where(prof => prof.vesselName.ToLower().Contains(f) || prof.profileName.ToLower().Contains(f));
                }

                var index = 0;
                foreach (var missionProfile in profiles)
                {
                    var isValidProfile = true;
                    var color = "";

                    // Build the descriptive text with highlighting:
                    var description = "<color=#F9FA86><b>" + missionProfile.profileName + "</b></color> <color=#FFFFFF>(" + missionProfile.vesselName + ")\n";
                    description += "<b>Mass:</b> " + missionProfile.launchMass.ToString("0.0t") + ", <b>Cost:</b> <color=#B3D355>" + missionProfile.launchCost.ToString("#,##0√")
                            + "</color> (<color=#B3D355>" + (missionProfile.launchCost / missionProfile.payloadMass).ToString("#,##0√") + "</color>/t), ";

                    // One-Way or Round-Trip:
                    var missionRouteDetails = "";
                    if (missionProfile.oneWayMission) missionRouteDetails = "one-way";
                    else missionRouteDetails = "round-trip";
                    if (this.filterRoundTrip != null)
                    {
                        if (this.filterRoundTrip != missionProfile.oneWayMission) { isValidProfile = false; color = red; }
                        else color = green;
                        missionRouteDetails = "<color=" + color + ">" + missionRouteDetails + "</color>";
                    }
                    description += missionRouteDetails + "\n";

                    // Mission-Type:
                    var missionType = MissionProfile.GetMissionProfileTypeName(missionProfile.missionType);
                    if (this.filterMissionType != null)
                    {
                        if (this.filterMissionType != missionProfile.missionType) { isValidProfile = false; color = red; }
                        else color = green;
                        missionType = "<color=" + color + ">" + missionType + "</color>";
                    }
                    description += "<b>Type:</b> " + missionType + ", ";

                    description += "<b>Duration:</b> " + GUI.FormatDuration(missionProfile.missionDuration) + "\n";

                    // Docking-Ports:
                    var dockingPorts = "";
                    if (missionProfile.missionType == MissionProfileType.TRANSPORT || this.filterDockingPortTypes != null)
                    {
                        var hasFittingPort = false;
                        var portNumber = 0;
                        if (missionProfile.dockingPortTypes != null)
                        {
                            foreach (var portType in missionProfile.dockingPortTypes)
                            {
                                if (portNumber > 0) dockingPorts += ", ";
                                if (this.filterDockingPortTypes != null && this.filterDockingPortTypes.Contains(portType))
                                {
                                    hasFittingPort = true;
                                    dockingPorts += "<color=" + green + ">" + TargetVessel.TranslateDockingPortName(portType) + "</color>";
                                }
                                else dockingPorts += TargetVessel.TranslateDockingPortName(portType);
                                portNumber++;
                            }
                        }
                        if (portNumber == 0) dockingPorts = "N/A";
                        if (this.filterDockingPortTypes != null && !hasFittingPort)
                        {
                            dockingPorts = "<color=" + red + ">" + dockingPorts + "</color>";
                            isValidProfile = false;
                        }
                    }
                    if (dockingPorts != "") description += "<b>Docking-Ports:</b> " + dockingPorts + "\n";

                    // Payload:
                    var payloadMass = missionProfile.payloadMass.ToString("0.0t");
                    if (this.filterMass != null)
                    {
                        // We only display one digit after the pount, so we should round here to avoid confustion:
                        if (Math.Round((double)this.filterMass, 1) > Math.Round(missionProfile.payloadMass, 1)) { isValidProfile = false; color = red; }
                        else color = green;
                        payloadMass = "<color=" + color + ">" + payloadMass + "</color>";
                    }
                    description += "<b>Payload:</b> " + payloadMass;

                    // Body:
                    var bodyName = missionProfile.destinationBodyName;
                    if (this.filterBody != null)
                    {
                        if (this.filterBody.bodyName != missionProfile.destinationBodyName) { isValidProfile = false; color = red; }
                        else color = green;
                        bodyName = "<color=" + color + ">" + bodyName + "</color>";
                    }
                    description += " to " + bodyName;

                    // Altitude:
                    var maxAltitude = GUI.FormatAltitude(missionProfile.maxAltitude);
                    if (this.filterAltitude != null)
                    {
                        if (missionProfile.IsForeignBodyDelivery())
                        {
                            // Off-origin delivery: the target must be on the recorded orbit or higher (by periapsis).
                            if (this.filterPeriapsis != null && this.filterPeriapsis < missionProfile.maxAltitude) { isValidProfile = false; color = red; }
                            else color = green;
                        }
                        else
                        {
                            if (this.filterAltitude > missionProfile.maxAltitude) { isValidProfile = false; color = red; }
                            else color = green;
                        }
                        maxAltitude = "<color=" + color + ">" + maxAltitude + "</color>";
                    }
                    description += " @ " + maxAltitude + "\n";

                    // Off-origin orbit tolerance: inclination, eccentricity and ascending node must stay near the
                    // recorded values, because a plane- or shape-change at the destination was not demonstrated.
                    // The values are rendered and coloured so the player sees WHY an out-of-plane target is rejected.
                    if (missionProfile.IsForeignBodyDelivery())
                    {
                        var inclination = missionProfile.deployInclination.ToString("0.0") + "°";
                        if (this.filterInclination != null)
                        {
                            if (Math.Abs((double)this.filterInclination - missionProfile.deployInclination) > KSTSSettings.ToleranceInclination) { isValidProfile = false; color = red; }
                            else color = green;
                            inclination = "<color=" + color + ">" + inclination + "</color>";
                        }

                        var eccentricity = missionProfile.deployEccentricity.ToString("0.000");
                        if (this.filterEccentricity != null)
                        {
                            if (Math.Abs((double)this.filterEccentricity - missionProfile.deployEccentricity) > KSTSSettings.ToleranceEccentricity) { isValidProfile = false; color = red; }
                            else color = green;
                            eccentricity = "<color=" + color + ">" + eccentricity + "</color>";
                        }

                        var lan = missionProfile.deployLAN.ToString("0.0") + "°";
                        if (this.filterLAN != null)
                        {
                            if (Math.Abs((double)this.filterLAN - missionProfile.deployLAN) > KSTSSettings.ToleranceInclination) { isValidProfile = false; color = red; }
                            else color = green;
                            lan = "<color=" + color + ">" + lan + "</color>";
                        }

                        description += "<b>Orbit:</b> incl " + inclination + ", ecc " + eccentricity + ", LAN " + lan + "\n";
                    }

                    // Crew-Capacity:
                    var crewCapacity = missionProfile.crewCapacity.ToString("0");
                    if (this.filterCrewCapacity != null)
                    {
                        if (this.filterCrewCapacity > missionProfile.crewCapacity) { isValidProfile = false; color = red; }
                        else color = green;
                        crewCapacity = "<color=" + color + ">" + crewCapacity + "</color>";
                    }
                    description += "<b>Crew-Capacity:</b> " + crewCapacity;

                    description += "</color>";

                    // Don't render invalid profiles if the "Hide Invalid" box is checked
                    // Has to be done down here to avoid duplicating any logic
                    if (isValidProfile || !hideInvalid)
                    {
                        displayedProfiles.Add(missionProfile);
                        contents.Add(new GUIContent(description, GUI.GetVesselThumbnail(missionProfile.vesselName)));

                        if (!isValidProfile) invalidIndices.Add(index);
                        index++;
                    }
                }

                var prevSelection = displayedProfiles.IndexOf(this.selectedProfile);
                var newSelection = GUILayout.SelectionGrid(prevSelection, contents.ToArray(), 1, GUI.selectionGridStyle);
                if (newSelection != prevSelection && !invalidIndices.Contains(newSelection))
                {
                    selectedProfile = displayedProfiles[newSelection];
                }
            }

            GUILayout.EndScrollView();
            return this.selectedProfile != null;
        }
    }
}
