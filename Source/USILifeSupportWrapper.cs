using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static KSTS.Statics;

namespace KSTS
{
    public class USILifeSupportWrapper
    {

        static USILifeSupportWrapper instance = null;
        static readonly object padlock = new object();

        UsiApi _usiApi;

        USILifeSupportWrapper()
        {
            _usiApi = new UsiApi();
        }

        public static USILifeSupportWrapper Instance
        {
            get
            {
                lock (padlock)
                {
                    if (instance == null)
                    {
                        instance = new USILifeSupportWrapper();
                    }
                    return instance;
                }
            }
        }

        internal static Type GetType(string name)
        {
            Type type = null;
            AssemblyLoader.loadedAssemblies.TypeOperation(t =>
            {
                if (t.FullName == name)
                {
                    type = t;
                }
            });
            return type;
        }

        public void PrepForLaunch(Vessel vessel, IEnumerable<ProtoCrewMember> crewMembers, double secondsInTransit)
        {
            if (_usiApi.Present)
            {
                _usiApi.PrepForLaunch(vessel, crewMembers, secondsInTransit);
            }
        }

        public bool Present()
        {
            return _usiApi.Present;
        }
        class UsiApi
        {
            private readonly Type _lifeSupportManager;
            private readonly PropertyInfo _instance;
            private readonly MethodInfo _fetchKerbal;
            private readonly MethodInfo _fetchVessel;
            private readonly MethodInfo _trackKerbal;

            private readonly Type _lifeSupportStatus;
            private readonly PropertyInfo[] _timeProperties;
            private readonly PropertyInfo _maxOffKerbinTime;
            private readonly PropertyInfo _previousVesselId;
            private readonly PropertyInfo _currentVesselId;

            public bool Present { get; private set; }

            internal UsiApi()
            {
                //var k = LifeSupportManager.Instance.FetchKerbal(c);
                //LifeSupportManager.GetTotalHabTime(VesselStatus, vessel);
                //if (!IsKerbalTracked(crew.name))
                //{
                //    var k = new LifeSupportStatus();
                //    k.KerbalName = crew.name;
                //    k.HomeBodyId = FlightGlobals.GetHomeBodyIndex();
                //    k.LastPlanet = FlightGlobals.GetHomeBodyIndex();
                //    k.LastMeal = Planetarium.GetUniversalTime();
                //    k.LastEC = Planetarium.GetUniversalTime();
                //    k.LastAtHome = Planetarium.GetUniversalTime();
                //    k.LastSOIChange = Planetarium.GetUniversalTime();
                //    k.MaxOffKerbinTime = Planetarium.GetUniversalTime() + 648000;
                //    k.TimeEnteredVessel = Planetarium.GetUniversalTime();
                //    k.CurrentVesselId = "?UNKNOWN?";
                //    k.PreviousVesselId = "??UNKNOWN??";
                //    k.LastUpdate = Planetarium.GetUniversalTime();
                //    k.IsGrouchy = false;
                //    k.OldTrait = crew.experienceTrait.Title;
                //    TrackKerbal(k);
                //}

                _lifeSupportManager = USILifeSupportWrapper.GetType("LifeSupport.LifeSupportManager");
                if (_lifeSupportManager != null)
                {
                    _instance = _lifeSupportManager.GetProperty("Instance");
                    _fetchKerbal = _lifeSupportManager.GetMethod("FetchKerbal");
                    _fetchVessel = _lifeSupportManager.GetMethod("FetchVessel");
                    _trackKerbal = _lifeSupportManager.GetMethod("TrackKerbal");

                    _lifeSupportStatus = USILifeSupportWrapper.GetType("LifeSupport.LifeSupportStatus");
                    _timeProperties = new PropertyInfo[] {
                        _lifeSupportStatus.GetProperty("LastMeal"),
                        _lifeSupportStatus.GetProperty("LastEC"),
                        _lifeSupportStatus.GetProperty("LastAtHome"),
                        _lifeSupportStatus.GetProperty("LastSOIChange"),
                        _lifeSupportStatus.GetProperty("TimeEnteredVessel"),
                        _lifeSupportStatus.GetProperty("LastUpdate"),
                    };
                    _maxOffKerbinTime = _lifeSupportStatus.GetProperty("MaxOffKerbinTime");
                    _previousVesselId = _lifeSupportStatus.GetProperty("PreviousVesselId");
                    _currentVesselId = _lifeSupportStatus.GetProperty("CurrentVesselId");

                    Present = true;
                }
            }

            internal void PrepForLaunch(Vessel vessel, IEnumerable<ProtoCrewMember> crewMembers, double secondsInTransit)
            {
                try
                {
                    var instance = _instance.GetValue(null, null);

                    var vesselId = vessel.id.ToString();
                    var vesselStatus = _fetchVessel.Invoke(instance, new object[] { vesselId });
                    foreach (var kerbal in crewMembers)
                    {
                        try
                        {
                            var kerbalStatus = _fetchKerbal.Invoke(instance, new object[] { kerbal });
                            var timeEnteredVessel = Planetarium.GetUniversalTime() - secondsInTransit;
                            foreach (var item in _timeProperties)
                            {
                                item.SetValue(kerbalStatus, timeEnteredVessel, null);
                            }
                            // I GetTotalHabTime doesn't work when the vessel isn't loaded, but it should set itself.
                            // var habTime = (double)_getTotalHabTime.Invoke(instance, new object[] { vesselStatus, vessel });
                            _maxOffKerbinTime.SetValue(kerbalStatus, 0, null);
                            _previousVesselId.SetValue(kerbalStatus, _currentVesselId.GetValue(kerbalStatus, null), null);
                            _currentVesselId.SetValue(kerbalStatus, vesselId, null);
                            _trackKerbal.Invoke(instance, new object[] { kerbalStatus });
                            Log.Warning($"{kerbal.name} was prepped for launch using {_lifeSupportManager.Name}, launch times were set to {timeEnteredVessel} seconds UT.");
                        }
                        catch (NullReferenceException ex)
                        {
                            Debug.LogError($"Failed to prep {kerbal} for launch.");
                            Log.Exception(ex);
                        }
                    }
                }
                catch (NullReferenceException ex)
                {
                    Debug.LogError($"Failed to prep {vessel} for launch.");
                    Log.Exception(ex);
                }
            }
        }
    }
}
