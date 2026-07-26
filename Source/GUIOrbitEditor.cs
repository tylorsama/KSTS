using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;
using static KSTS.Statics;

namespace KSTS
{
    class GUIOrbitEditor
    {
        private CelestialBody body = null;
        private GUIRichValueSelector altitudeSelector;
        private GUIRichValueSelector inclinationSelector;
        private GUIRichValueSelector eccentricitySelector;
        private GUIRichValueSelector semiMajorAxisSelector;
        private GUIRichValueSelector longitudeOfAscendingNodeSelector;
        private GUIRichValueSelector argumentOfPeriapsisSelector;
        private GUIRichValueSelector meanAnomalyAtEpochSelector;
        private MissionProfile missionProfile = null;
        private int selectedEditorTab = 0;
        private bool showReferenceVessels;
        private double referenceVesselEpoch;

        public GUIOrbitEditor(MissionProfile missionProfile)
        {
            this.body = FlightGlobals.GetBodyByName(missionProfile.destinationBodyName);
            if(this.body == null) { // in case this flight was registered on a now-invalid body
                this.body = FlightGlobals.GetHomeBody();
            }
            this.missionProfile = missionProfile;
            Reset();
        }

        public void Reset()
        {
            // For deliveries to a body other than the launch body the demonstrated orbit becomes a floor:
            // altitude can only go up (to just below the SoI), and inclination/eccentricity are limited to a
            // tolerance band around the recorded values (a plane- or shape-change was not demonstrated).
            var offOrigin = this.missionProfile.IsForeignBodyDelivery();
            double floorAltitude = offOrigin ? this.missionProfile.maxAltitude : this.missionProfile.minAltitude;
            double ceilingAltitude = this.missionProfile.maxAltitude;
            if (offOrigin)
            {
                ceilingAltitude = this.body.sphereOfInfluence - KSTSSettings.CeilingSoiMargin - this.body.Radius;
                if (ceilingAltitude < floorAltitude) ceilingAltitude = floorAltitude; // Keep the range non-empty for tiny SoIs.
            }
            double defaultAltitude = offOrigin ? floorAltitude : ceilingAltitude;

            double minInclination = offOrigin ? this.missionProfile.deployInclination - KSTSSettings.ToleranceInclination : -180;
            double maxInclination = offOrigin ? this.missionProfile.deployInclination + KSTSSettings.ToleranceInclination : 180;
            double defaultInclination = offOrigin ? this.missionProfile.deployInclination : 0;
            double minEccentricity = offOrigin ? Math.Max(0, this.missionProfile.deployEccentricity - KSTSSettings.ToleranceEccentricity) : 0;
            double maxEccentricity = offOrigin ? Math.Min(0.99, this.missionProfile.deployEccentricity + KSTSSettings.ToleranceEccentricity) : 1;
            double defaultEccentricity = offOrigin ? this.missionProfile.deployEccentricity : 0;
            double minLAN = offOrigin ? this.missionProfile.deployLAN - KSTSSettings.ToleranceInclination : 0;
            double maxLAN = offOrigin ? this.missionProfile.deployLAN + KSTSSettings.ToleranceInclination : 360;
            double defaultLAN = offOrigin ? this.missionProfile.deployLAN : 0;

            // Simple orbits:
            altitudeSelector = new GUIRichValueSelector("Altitude", Math.Floor(defaultAltitude), "m", Math.Ceiling(floorAltitude), Math.Floor(ceilingAltitude), true, "#,##0");
            inclinationSelector = new GUIRichValueSelector("Inclination", defaultInclination, "°", minInclination, maxInclination, true, "+0.000;-0.000");

            // Additional settings for complex orbits:
            eccentricitySelector = new GUIRichValueSelector("Eccentricity", defaultEccentricity, "", minEccentricity, maxEccentricity, true, "0.000");
            semiMajorAxisSelector = new GUIRichValueSelector("SMA", Math.Floor(body.Radius + defaultAltitude), "m", Math.Ceiling(body.Radius + floorAltitude), Math.Floor(body.Radius + ceilingAltitude), true, "#,##0.0");
            longitudeOfAscendingNodeSelector = new GUIRichValueSelector("LAN", defaultLAN, "°", minLAN, maxLAN, true, "0.000");
            argumentOfPeriapsisSelector = new GUIRichValueSelector("AOP", 0, "°", 0, 360, true, "0.000");
            meanAnomalyAtEpochSelector = new GUIRichValueSelector("MAE", 0, "° rad", -Math.PI, Math.PI, true, "0.000");
            showReferenceVessels = false;
            referenceVesselEpoch = 0;
        }

        static string[] options = { "Simple Orbit", "Complex Orbit" };
        public void DisplayEditor()
        {
            var newSelection = GUILayout.Toolbar(selectedEditorTab, options);
            if (newSelection != selectedEditorTab)
            {
                selectedEditorTab = newSelection;
                Reset();
            }

            switch (selectedEditorTab)
            {
                case 0: // Simple Editpor:
                    altitudeSelector.Display();
                    inclinationSelector.Display();
                    break;

                case 1: // Complex Editor:
                    // Display a button / list to select a reference vessel:
                    if (!showReferenceVessels) {
                        if (GUILayout.Button("Select Reference Vessel", GUI.buttonStyle)) showReferenceVessels = true;
                    }
                    else
                    {
                        var refVessels = new List<string>();
                        var refOrbits = new List<Orbit>();

                        // Find applicable vessels:
                        foreach (var refVessel in FlightGlobals.Vessels)
                        {
                            if (refVessel.situation != Vessel.Situations.ORBITING || refVessel.orbit.referenceBody != this.body) continue;
                            if (refVessel.orbit.ApA > Math.Floor(this.missionProfile.maxAltitude)) continue;
                            refVessels.Add("<b>" + Localizer.Format(refVessel.vesselName) + "</b> (" + refVessel.vesselType.ToString() + ")");
                            refOrbits.Add(refVessel.orbit);
                        }

                        GUILayout.Label("<b>Select Reference Vessel:</b>");
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("", new GUIStyle(GUI.labelStyle) { fixedWidth = 10 }); // Just to indent the following list a little bit
                        var selectedRefVessel = GUILayout.SelectionGrid(-1, refVessels.ToArray(), 1, GUI.selectionGridStyle);
                        GUILayout.EndHorizontal();
                        if (selectedRefVessel >= 0)
                        {
                            var refOrbit = refOrbits[selectedRefVessel];
                            inclinationSelector.Value = refOrbit.inclination;
                            eccentricitySelector.Value = refOrbit.eccentricity;
                            semiMajorAxisSelector.Value = refOrbit.semiMajorAxis;
                            longitudeOfAscendingNodeSelector.Value = refOrbit.LAN;
                            argumentOfPeriapsisSelector.Value = refOrbit.argumentOfPeriapsis;

                            // This should only be between -PI..+PI, but some created vessels like Kerbals to rescue have values 0..+2PI:
                            if (refOrbit.meanAnomalyAtEpoch > Math.PI) meanAnomalyAtEpochSelector.Value = refOrbit.meanAnomalyAtEpoch - Math.PI * 2;
                            else meanAnomalyAtEpochSelector.Value = refOrbit.meanAnomalyAtEpoch;

                            referenceVesselEpoch = refOrbit.epoch;
                            showReferenceVessels = false;

                        }
                    }

                    inclinationSelector.Display();
                    eccentricitySelector.Display();
                    semiMajorAxisSelector.Display();
                    longitudeOfAscendingNodeSelector.Display();
                    argumentOfPeriapsisSelector.Display();
                    meanAnomalyAtEpochSelector.Display();
                    break;

                default:
                    throw new Exception("unexpected option " + selectedEditorTab.ToString());
            }
        }


        public Orbit GetOrbit()
        {
            switch (selectedEditorTab)
            {
                case 0: // Simple Editpor:
                {
                    // Off-origin deliveries must keep the recorded orbital plane, so pin the ascending node too:
                    var simpleLAN = this.missionProfile.IsForeignBodyDelivery() ? this.missionProfile.deployLAN : 0;
                    return CreateSimpleOrbit(this.body, altitudeSelector.Value, inclinationSelector.Value, simpleLAN);
                }

                case 1: // Complex Editor:
                    // Not sure if we should set an epoch when coying over orbital-values from a reference vessel, but it's probably fine to leave it.
                    return CreateOrbit(inclinationSelector.Value, eccentricitySelector.Value, semiMajorAxisSelector.Value, longitudeOfAscendingNodeSelector.Value, argumentOfPeriapsisSelector.Value, meanAnomalyAtEpochSelector.Value, referenceVesselEpoch, this.body);

                default:
                    throw new Exception("unexpected option " + selectedEditorTab.ToString());
            }
        }

        public static Orbit CreateSimpleOrbit(CelestialBody body, double altitude, double inclination, double longitudeOfAscendingNode = 0)
        {
            return GUIOrbitEditor.CreateOrbit(inclination, 0, altitude + body.Radius, longitudeOfAscendingNode, 0, 0, 0, body);
        }

        public static Orbit CreateOrbit(double inclination, double eccentricity, double semiMajorAxis, double longitudeOfAscendingNode, double argumentOfPeriapsis, double meanAnomalyAtEpoch, double epoch, CelestialBody body)
        {
            if (double.IsNaN(inclination)) inclination = 0;
            if (double.IsNaN(eccentricity)) eccentricity = 0;
            if (double.IsNaN(semiMajorAxis)) semiMajorAxis = body.Radius + body.atmosphereDepth + 10000;
            if (double.IsNaN(longitudeOfAscendingNode)) longitudeOfAscendingNode = 0;
            if (double.IsNaN(argumentOfPeriapsis)) argumentOfPeriapsis = 0;
            if (double.IsNaN(meanAnomalyAtEpoch)) meanAnomalyAtEpoch = 0;
            if (double.IsNaN(epoch)) meanAnomalyAtEpoch = Planetarium.GetUniversalTime();
            if (Math.Sign(eccentricity - 1) == Math.Sign(semiMajorAxis)) semiMajorAxis = -semiMajorAxis;

            if (Math.Sign(semiMajorAxis) >= 0)
            {
                while (meanAnomalyAtEpoch < 0) meanAnomalyAtEpoch += Math.PI * 2;
                while (meanAnomalyAtEpoch > Math.PI * 2) meanAnomalyAtEpoch -= Math.PI * 2;
            }

            return new Orbit(inclination, eccentricity, semiMajorAxis, longitudeOfAscendingNode, argumentOfPeriapsis, meanAnomalyAtEpoch, epoch, body);
        }

        public static ConfigNode SaveOrbitToNode(Orbit orbit, string nodeName="orbit")
        {
            var node = new ConfigNode(nodeName);
            node.AddValue("inclination", orbit.inclination);
            node.AddValue("eccentricity", orbit.eccentricity);
            node.AddValue("semiMajorAxis", orbit.semiMajorAxis);
            node.AddValue("longitudeOfAscendingNode", orbit.LAN);
            node.AddValue("argumentOfPeriapsis", orbit.argumentOfPeriapsis);
            node.AddValue("meanAnomalyEpoch", orbit.meanAnomalyAtEpoch);
            node.AddValue("epoch", orbit.epoch);
            node.AddValue("body", orbit.referenceBody.bodyName);
            return node;
        }

        public static Orbit CreateOrbitFromNode(ConfigNode node)
        {
            return CreateOrbit(
                Double.Parse(node.GetValue("inclination")),
                Double.Parse(node.GetValue("eccentricity")),
                Double.Parse(node.GetValue("semiMajorAxis")),
                Double.Parse(node.GetValue("longitudeOfAscendingNode")),
                Double.Parse(node.GetValue("argumentOfPeriapsis")),
                Double.Parse(node.GetValue("meanAnomalyEpoch")),
                Double.Parse(node.GetValue("epoch")),
                FlightGlobals.Bodies.Find(x => x.bodyName == node.GetValue("body"))
            );
        }

        // Retrurns a new orbit, which is following the given orbit at the given distance:
        public static Orbit CreateFollowingOrbit(Orbit referenceOrbit, double distance)
        {
            var orbit = CreateOrbit(referenceOrbit.inclination, referenceOrbit.eccentricity, referenceOrbit.semiMajorAxis, referenceOrbit.LAN, referenceOrbit.argumentOfPeriapsis, referenceOrbit.meanAnomalyAtEpoch, referenceOrbit.epoch, referenceOrbit.referenceBody);
            // The distance ("chord") between to points on a circle is given by: chord = 2r * sin( alpha / 2 )
            var angle = Math.Sinh(distance / (2 * orbit.semiMajorAxis)) * 2; // Find the angle for the given distance
            orbit.meanAnomalyAtEpoch += angle;
            return orbit;
        }

        // Modifies and returns the given orbit so that a vessel of the given size won't collide with any other vessel on the same orbit:
        public static Orbit ApplySafetyDistance(Orbit orbit, float vesselSize)
        {
            // Find out how many degrees one meter is on the given orbit (same formula as above):
            var anglePerMeters = Math.Sinh(1.0 / (2 * orbit.semiMajorAxis)) * 2;

            // Check with every other vessel on simmilar orbits, if they might collide in the future:
            var rnd = new System.Random();
            var adjustmentIterations = 0;
            bool orbitAdjusted;
            do
            {
                orbitAdjusted = false;
                foreach (var vessel in FlightGlobals.Vessels)
                {
                    if (vessel.situation != Vessel.Situations.ORBITING) continue;
                    if (vessel.orbit.referenceBody != orbit.referenceBody) continue;

                    // Find the next rendezvous (most of these parameters are just guesses, but they seem to work):
                    var UT = Planetarium.GetUniversalTime();
                    double dT = 86400;
                    double threshold = 5000;
                    var MinUT = Planetarium.GetUniversalTime() - 86400;
                    var MaxUT = Planetarium.GetUniversalTime() + 86400;
                    double epsilon = 360;
                    var maxIterations = 25;
                    var iterationCount = 0;
                    vessel.orbit.UpdateFromUT(Planetarium.GetUniversalTime()); // We apparently have to update both orbits to the current time to make this work.
                    orbit.UpdateFromUT(Planetarium.GetUniversalTime());
                    var closestApproach = Orbit._SolveClosestApproach(vessel.orbit, orbit, ref UT, dT, threshold, MinUT, MaxUT, epsilon, maxIterations, ref iterationCount);
                    if (closestApproach < 0) continue; // No contact
                    if (closestApproach > 10000) continue; // 10km should be fine

                    double blockerSize = TargetVessel.GetVesselSize(vessel);
                    if (closestApproach < (blockerSize + vesselSize) / 2) // We assume the closest approach is calculated from the center of mass, which is why we use /2
                    {
                        // Adjust orbit:
                        var adjustedAngle = (blockerSize / 2 + vesselSize / 2 + 1) * anglePerMeters; // Size of both vessels + 1m
                        if (adjustmentIterations >= 90) adjustedAngle *= rnd.Next(1, 1000); // Lets get bolder here, time is running out ...

                        // Modifying "orbit.meanAnomalyAtEpoch" works for the actual vessel, but apparently one would have to call some other method as well to updated
                        // additional internals of the object, because the "_SolveClosestApproach"-function does not register this change, which is why we simply create
                        // a new, modified orbit:
                        orbit = CreateOrbit(orbit.inclination, orbit.eccentricity, orbit.semiMajorAxis, orbit.LAN, orbit.argumentOfPeriapsis, orbit.meanAnomalyAtEpoch + adjustedAngle, orbit.epoch, orbit.referenceBody);

                        orbitAdjusted = true;
                        Log.Warning("adjusting planned orbit by " + adjustedAngle + "° to avoid collision with '" + Localizer.Format(vessel.vesselName) + "' (closest approach " + closestApproach.ToString() + "m @ " + UT.ToString() + " after " + iterationCount + " orbits)");
                    }
                }
                adjustmentIterations++;
                if (adjustmentIterations >= 100 && orbitAdjusted)
                {
                    Debug.LogError("unable to find a safe orbit after " + adjustmentIterations.ToString() + " iterations, the vessels will likely crash");
                    break;
                }
            }
            while (orbitAdjusted);
            return orbit;
        }
    }
}
