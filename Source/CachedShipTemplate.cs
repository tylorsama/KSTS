using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using ClickThroughFix;
using KSP.Localization;
using KSP.UI.Screens; // For "ApplicationLauncherButton"
using KSTS;
using ToolbarControl_NS;
using UnityEngine;

namespace KSTS
{

    public enum TemplateOrigin { VAB, SPH, Subassemblies };

    // Helper class to store a ships template (from the craft's save-file) together with its generated thumbnail:
    public class CachedShipTemplate : MonoBehaviour
    {
        public ShipTemplate template = null;
        public Texture2D thumbnail = null;
        public TemplateOrigin templateOrigin;
        public string vesselName = null;
        public DateTime lastWriteTime = DateTime.MinValue;

        private List<AvailablePart> cachedParts = null;

        public int crewCapacity = 0;
        public double dryMass = 0;

        private bool invalidated = false;

        void Awake()
        {
            DontDestroyOnLoad(this.gameObject);
        }

        public void IngestNodes()
        {
            if (this.cachedParts != null && this.dryMass > 0) { this.AcquireThumbnail(); return; }
            if (this.template == null || this.template.config == null) { this.invalidated = true; return; }
            this.cachedParts = [];
            this.crewCapacity = 0;
            this.dryMass = 0;

            foreach (ConfigNode node in template.config.GetNodes())
            {
                try
                {
                    if (!string.Equals(node.name, "part", StringComparison.OrdinalIgnoreCase)) { continue; }
                    if (!node.HasValue("part")) { continue; }

                    string partName = node.GetValue("part");
                    partName = partName.Substring(0, partName.LastIndexOf("_"));    //XKCD_208
                    if (!KSTS.partDictionary.TryGetValue(partName, out AvailablePart availablePart))
                    {
                        Debug.LogError("[KSTS] Part '" + partName + "' not found in global part-directory");
                        continue;
                    }
                    this.cachedParts.Add(availablePart);

                    if (availablePart.partConfig.HasValue("CrewCapacity"))
                    {
                        if (int.TryParse(availablePart.partConfig.GetValue("CrewCapacity"), out int parsedCapacity))
                            this.crewCapacity += parsedCapacity;
                    }
                    if (availablePart.partConfig.HasValue("mass"))
                    {
                        if (Double.TryParse(availablePart.partConfig.GetValue("mass"), out double parsedMass))
                            this.dryMass += parsedMass;
                        if (Double.TryParse(node.GetValue("modMass"), out parsedMass))
                            this.dryMass += parsedMass;
                    }
                }
                catch (Exception e) { Debug.LogError("[KSTS] Node indigestion! " + e.ToString()); }
            }
            if (this.dryMass == 0 || this.cachedParts.Count == 0) { invalidated = true; return; }
			this.AcquireThumbnail();
        }

        internal void AcquireThumbnail()
        {
            if (this.thumbnail != null && this.thumbnail != GUI.placeholderImage) { return; }
            this.thumbnail ??= GUI.placeholderImage;
            string thumbnailFilePath = ThumbnailHelper.GetThumbnailFilePath(this.templateOrigin, this.vesselName);
            if (File.Exists(thumbnailFilePath))
            {
                Texture2D scratchThumbnail = new Texture2D(2, 2);
                scratchThumbnail.LoadImage(File.ReadAllBytes(thumbnailFilePath));
                this.thumbnail = ThumbnailHelper.ResizeTexture(scratchThumbnail, 64, 64);
                return;
            }
            if (!HighLogic.LoadedSceneIsEditor || this.invalidated) { return; }
            if (this.template?.config == null) { this.invalidated = true; return; }

            bool isVab = this.templateOrigin == TemplateOrigin.VAB;
            ThumbnailHelper.CaptureThumbnailFromConfig(this.template.config, 256, thumbnailFilePath, isVab);
            if (File.Exists(thumbnailFilePath))
            {
                Texture2D scratchThumbnail = new Texture2D(2, 2);
                scratchThumbnail.LoadImage(File.ReadAllBytes(thumbnailFilePath));
                this.thumbnail = ThumbnailHelper.ResizeTexture(scratchThumbnail, 64, 64);
                Debug.Log("[KSTS] Successfully captured thumbnail for " + this.vesselName);
                return;
            }
            this.invalidated = true;
        }
    }
}
