using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using UnityEngine;
using KSP.UI.Screens;
using System.Text.RegularExpressions;
using KSP.Localization;

using ToolbarControl_NS;
using ClickThroughFix;
using static KSTS.Statics;

namespace KSTS
{

    // Creates the button and contains the functionality to draw the GUI-window (we want to use the same window
    // for different scenes, that is why we have a few helper-classes below)
    [KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
    public class GUI : MonoBehaviour
    {
        public const int WIDTH = 450;
        public const int WIDTH_SPACECENTER = WIDTH + (WIDTH / 6 - 6);

        public static Rect windowPosition = new Rect(300, 60, WIDTH, 400);
        public static bool showGui = false;

        // Styles (initialized in OnReady):
        public static GUIStyle labelStyle = null;
        public static GUIStyle buttonStyle = null;
        public static Color normalGUIbackground;
        public static GUIStyle textFieldStyle = null;
        public static GUIStyle scrollStyle = null;
        public static GUIStyle selectionGridStyle = null;

        // Common resources:
        ToolbarControl toolbarControl = null;
        internal const string MODID = "KSTS_NS";
        internal const string MODNAME = "Kerbal Space Transport System";

        private static int selectedMainTab = 0;
        public static Texture2D placeholderImage = null;
        public static List<CachedShipTemplate> shipTemplates = [];
        public static string currentSaveFolder = "";
        private static Texture2D scratchThumbnail = null;

        private static string helpText = "";
        private static Vector2 helpTabScrollPos = Vector2.zero;

        void Awake()
        {
			DontDestroyOnLoad(this.gameObject);
			if (placeholderImage == null)
            {
                scratchThumbnail = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                placeholderImage = new Texture2D(275, 275, TextureFormat.RGBA32, false);
                placeholderImage.LoadImage(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "../PluginData/placeholder.png")));
                placeholderImage = ThumbnailHelper.ResizeTexture(placeholderImage, 64, 64); // Default-size for our ship-icons
            }

            if (helpText == "")
            {
                var helpFilename = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/../PluginData/help.txt";
                if (File.Exists(helpFilename)) helpText = File.ReadAllText(helpFilename);
                else
                {
                    helpText = "Help-file not found.";
                    Log.Warning("helpFilename: " + helpFilename);
                }
            }
		}

        void Start()
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle("Label");
                buttonStyle = new GUIStyle("Button");
                normalGUIbackground = UnityEngine.GUI.backgroundColor;
                textFieldStyle = new GUIStyle("TextField");
                scrollStyle = HighLogic.Skin.scrollView;
                selectionGridStyle = new GUIStyle(GUI.buttonStyle) { richText = true, fontStyle = FontStyle.Normal, alignment = TextAnchor.UpperLeft };
            }
            InitializeButton();
        }

        void InitializeButton()
        {
            if (toolbarControl != null)
                return;

            toolbarControl = gameObject.AddComponent<ToolbarControl>();
            toolbarControl.AddToAllToolbars(GuiOn, GuiOff,
                ApplicationLauncher.AppScenes.SPACECENTER |
                ApplicationLauncher.AppScenes.TRACKSTATION |
                ApplicationLauncher.AppScenes.FLIGHT,
                MODID,
                "KSTSButton",
                "KSTS/PluginData/KSTS_icon_38",
                "KSTS/PluginData/KSTS_icon_24",
                MODNAME
            );

        }

        private void GuiOn()
        {
            Log.Warning("KSTS: GuiOn");
            showGui = true;
            if (HighLogic.LoadedScene == GameScenes.SPACECENTER)
                windowPosition.width = WIDTH_SPACECENTER;
            else
                windowPosition.width = WIDTH;
            UpdateVesselTemplates();
        }

        private void GuiOff()
        {
            showGui = false;
        }

        public static string FormatDuration(double duration)
        {
            var dayLength = 24;
            if (GameSettings.KERBIN_TIME) dayLength = 6;
            var seconds = duration % 60;
            var minutes = ((int)(duration / 60)) % 60;
            var hours = ((int)(duration / 60 / 60)) % dayLength;
            var days = ((int)(duration / 60 / 60 / dayLength));
            return String.Format("{0:0}/{1:00}:{2:00}:{3:00.00}", days, hours, minutes, seconds);
        }

        public static string FormatAltitude(double altitude)
        {
            if (altitude >= 1000000) return (altitude / 1000).ToString("#,##0km");
            else return altitude.ToString("#,##0m");
        }

        // Returns a thumbnail for a given vessel-name (used to find fitting images for vessels used in mission-profiles):
        public static Texture2D GetVesselThumbnail(string vesselName)
        {
            return GUI.shipTemplates.Find(x => Localizer.Format(x.template.shipName) == vesselName)?.thumbnail ?? GUI.placeholderImage;
        }

        public static void UpdateVesselTemplates()
        {
            //int i = 0;
			shipTemplates ??= [];
			foreach (TemplateOrigin templateOrigin in Enum.GetValues(typeof(TemplateOrigin)))
            {
                string baseDirectory = GetBaseDirectoryForOrigin(templateOrigin);
                if (!Directory.Exists(baseDirectory)) { return; }
                string[] matchedFiles = Directory.GetFiles(baseDirectory, "*.craft", SearchOption.AllDirectories);
                if (matchedFiles == null || matchedFiles.Length == 0) { return; }
                foreach (string matchedFile in matchedFiles)
                {
                    try
                    {
                        string validFileName = Path.GetFileNameWithoutExtension(matchedFile);
                        if (validFileName == "" || validFileName == "Auto-Saved Ship") { continue; } // Skip auto-saves
                        if (shipTemplates.Exists(x => x.vesselName == validFileName)) { continue; }
                        CachedShipTemplate existingTemaplate = GUI.shipTemplates.Find(x => x.vesselName == validFileName);
                        if (existingTemaplate != null && existingTemaplate.lastWriteTime >= File.GetLastWriteTime(matchedFile)) { continue; }
                        Debug.Log("[KSTS] Found new template: " + matchedFile + " in " + templateOrigin.ToString());
                        CachedShipTemplate cachedTemplate = new CachedShipTemplate
                        {
                            vesselName = validFileName,
                            template = ShipConstruction.LoadTemplate(matchedFile),
                            templateOrigin = templateOrigin,
                            lastWriteTime = File.GetLastWriteTime(matchedFile),
                            thumbnail = placeholderImage
                        };
                        cachedTemplate.IngestNodes();
                        GUI.shipTemplates.Add(cachedTemplate);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("[KSTS] ReadAllCraftFiles failed for '" + matchedFile + "': " + e.ToString());
                    }
                }
                //i++;
                //if (i >= MaxFilesPerRun) { break; }
            }
            GUI.shipTemplates.Sort((x, y) => x.template.shipName.CompareTo(y.template.shipName));
            if (HighLogic.LoadedSceneIsEditor) { ProcessMissingThumbnails(); }
        }
        static string GetBaseDirectoryForOrigin(TemplateOrigin origin) //follow ShipConstruction.cs path logic
        {
            if (origin == TemplateOrigin.SPH) { return ShipConstruction.GetCurrentGameShipsPathFor(EditorFacility.SPH); }
            if (origin == TemplateOrigin.Subassemblies) { return KSPUtil.ApplicationRootPath + "saves/" + HighLogic.SaveFolder + "/Subassemblies/"; }
            return ShipConstruction.GetCurrentGameShipsPathFor(EditorFacility.VAB);
        }
        static TemplateOrigin GetOriginForBaseDirectory(string shipPath)
        {
            if (shipPath.Contains("/Ships/SPH")) { return TemplateOrigin.SPH; }
            if (shipPath.Contains("/Subassemblies")) { return TemplateOrigin.Subassemblies; }
            return TemplateOrigin.VAB; // Default fallback
        }
        // Try to render missing thumbnails
        static void ProcessMissingThumbnails()
        {
            shipTemplates ??= [];
            List<CachedShipTemplate> defaultThumbnailCrafts = [.. shipTemplates.Where(x => x.thumbnail == placeholderImage)];
            if (defaultThumbnailCrafts.Count == 0) { return; }
			foreach (CachedShipTemplate fixableCraft in defaultThumbnailCrafts)
            {
                fixableCraft.AcquireThumbnail();
			}
        }

        public static void Reset()
        {
            if (GUI.shipTemplates != null)
            {
                foreach (CachedShipTemplate template in GUI.shipTemplates)
                {
                    Destroy(template);
                }
            }
            GUI.shipTemplates = [];
            GUIStartDeployMissionTab.Reset();
            GUIStartTransportMissionTab.Reset();
            GUIRecordingTab.Reset();
        }

        // Moved here to avoid reinitializing every single loop
        static readonly string[] toolbarStrings = ["Flights", "Deploy", "Transport", "Construct", "Record", "Help", "Settings"];

        // Applies the configured UI scale (KSTSSettings.UiScale) around the window's top-left corner and returns the
        // previous GUI matrix. Each window site calls this before GUILayoutWindow and restores it via EndScale.
        public static Matrix4x4 BeginScale()
        {
            var previous = UnityEngine.GUI.matrix;
            if (KSTSSettings.UiScale != 1.0)
                GUIUtility.ScaleAroundPivot(Vector2.one * (float)KSTSSettings.UiScale, new Vector2(windowPosition.x, windowPosition.y));
            return previous;
        }

        public static void EndScale(Matrix4x4 previous)
        {
            UnityEngine.GUI.matrix = previous;
        }

        // Since the Unity 2019 update (KSP 1.8), a GUILayout scroll view no longer asks its window for the height of
        // its content: it reports a minimum of 32px and the window keeps whatever height its Rect last had, so every
        // tab was squeezed into the initial 400px and the rest was cut off. Instead we remember each scroll view's
        // content height from the previous repaint and request exactly that, capped to what fits on screen (beyond
        // that it scrolls). Together with resetting the window height every frame (see FitWindowToContent), the
        // window grows and shrinks with the active tab.
        private static readonly Dictionary<string, float> scrollContentHeights = new Dictionary<string, float>();
        private const float DEFAULT_SCROLL_HEIGHT = 300;
        private const float MIN_SCROLL_HEIGHT = 60;
        private const float WINDOW_CHROME_HEIGHT = 220; // Title, tab bar and the buttons some tabs draw below their list.

        public static Vector2 BeginAutoScroll(string key, Vector2 scrollPos)
        {
            if (!scrollContentHeights.TryGetValue(key, out float contentHeight)) contentHeight = DEFAULT_SCROLL_HEIGHT;
            var scale = (float)KSTSSettings.UiScale;
            var maxHeight = Mathf.Max(MIN_SCROLL_HEIGHT, (Screen.height - windowPosition.y) / scale - WINDOW_CHROME_HEIGHT);
            var height = Mathf.Clamp(contentHeight, MIN_SCROLL_HEIGHT, maxHeight);
            return GUILayout.BeginScrollView(scrollPos, scrollStyle, GUILayout.Height(height));
        }

        public static void EndAutoScroll(string key)
        {
            if (Event.current.type == EventType.Repaint)
            {
                try
                {
                    // Inside the scroll view, the last control's rect is in content coordinates, so its bottom edge is
                    // the content height:
                    scrollContentHeights[key] = GUILayoutUtility.GetLastRect().yMax + scrollStyle.padding.vertical + 4;
                }
                catch (ArgumentException) { } // Nothing was drawn inside the scroll view this frame.
            }
            GUILayout.EndScrollView();
        }

        // Called by the scene helpers right before GUILayoutWindow: a GUILayout window only ever grows to its content's
        // minimum size, so dropping the height lets it shrink back when a shorter tab is selected.
        public static void FitWindowToContent()
        {
            windowPosition.height = 0;
        }

        // Is called by our helper-classes to draw the actual window:
        public static void DrawWindow()
        {
            if (!showGui) {return;}
            try
            {
                GUILayout.BeginVertical();

                // Title:

                GUILayout.BeginArea(new Rect(0, 3, windowPosition.width /* windowStyle.fixedWidth */, 20));
                GUILayout.Label("<size=14><b>Kerbal Space Transport System</b></size>", new GUIStyle(GUI.labelStyle) { fixedWidth = windowPosition.width /*  windowStyle.fixedWidth */, alignment = TextAnchor.MiddleCenter });
                GUILayout.EndArea();

                // Tab-Switcher:
                if (MissionController.missionProfiles.Count == 0)
                {
                    int x = WIDTH / 6 - 6;
                    GUILayout.BeginHorizontal();
                    UnityEngine.GUI.enabled = false;
                    for (int i = 0; i < 4; i++)
                        GUILayout.Button(toolbarStrings[i], GUILayout.Width(x));
                    UnityEngine.GUI.enabled = true;
                    if (GUILayout.Button(toolbarStrings[4], GUILayout.Width(x)))
                        selectedMainTab = 4;
                    if (GUILayout.Button(toolbarStrings[5], GUILayout.Width(x)))
                        selectedMainTab = 5;
                    if (HighLogic.LoadedScene == GameScenes.SPACECENTER)
                    {
                        if (GUILayout.Button(toolbarStrings[6], GUILayout.Width(x)))
                            selectedMainTab = 6;
                    }
                    GUILayout.EndHorizontal();
                }
                else
                    selectedMainTab = GUILayout.Toolbar(selectedMainTab, toolbarStrings);

                switch (selectedMainTab)
                {
                    // Flights:
                    case 0:
                        GUIFlightsTab.Display();
                        break;

                    // Deploy:
                    case 1:
                        if (HighLogic.LoadedScene == GameScenes.FLIGHT)
                        {
                            GUI.BeginAutoScroll("GUI:294", Vector2.zero);
                            GUILayout.Label("<b>Please go to the Space Center to launch a new mission.</b>");
                            GUI.EndAutoScroll("GUI:294");
                        }
                        else if (GUIStartDeployMissionTab.Display()) selectedMainTab = 0;
                        break;

                    // Transport:
                    case 2:
                        if (HighLogic.LoadedScene == GameScenes.FLIGHT)
                        {
                            GUI.BeginAutoScroll("GUI:305", Vector2.zero);
                            GUILayout.Label("<b>Please go to the Space Center to launch a new mission.</b>");
                            GUI.EndAutoScroll("GUI:305");
                        }
                        else if (GUIStartTransportMissionTab.Display()) selectedMainTab = 0;
                        break;

                    // Construct:
                    case 3:
                        if (HighLogic.LoadedScene == GameScenes.FLIGHT)
                        {
                            GUI.BeginAutoScroll("GUI:316", Vector2.zero);
                            GUILayout.Label("<b>Please go to the Space Center to launch a new mission.</b>");
                            GUI.EndAutoScroll("GUI:316");
                        }
                        else if (GUIStartConstructMissionTab.Display()) selectedMainTab = 0;
                        break;

                    // Record:
                    case 4:
                        GUIRecordingTab.Display();
                        break;

                    // Help:
                    case 5:
                        helpTabScrollPos = GUI.BeginAutoScroll("GUI:330", helpTabScrollPos);
                        GUILayout.Label(helpText);
                        GUI.EndAutoScroll("GUI:330");
                        break;

                    case 6:

                        GUILayout.Label("<size=14><b>Alarm Clock Settings:</b></size>");
                        GUI.BeginAutoScroll("GUI:338", new Vector2(0, 0));

                        MissionController.useKACifAvailable = GUILayout.Toggle(MissionController.useKACifAvailable, "Use Kerbal Alarm Clock (if available)");
                        MissionController.useStockAlarmClock = GUILayout.Toggle(MissionController.useStockAlarmClock, "Use Stock Alarm Clock");

                        GUI.EndAutoScroll("GUI:338");

                        break;

                    default:
                        GUILayout.Label("<b>Not implemented yet.</b>");
                        break;
                }

                GUILayout.EndVertical();
                UnityEngine.GUI.DragWindow();
            }
            catch (Exception e)
            {
                Debug.LogError("DrawWindow(): " + e.ToString());
            }
        }
    }
}
