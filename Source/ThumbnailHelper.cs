using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KSTS
{
    /////////////////////////////////
    /// Following from:  
    /// http://forum.kerbalspaceprogram.com/threads/119609-Manually-generating-ship-thumbnail
    /// 
    public class ThumbnailHelper : MonoBehaviour
    {
		/// <summary>
		/// Generates a thumbnail exactly like the one KSP generates automatically.
		/// Behaves exactly like ShipConstruction.CaptureThumbnail() but allows customizing the resolution.
		/// If you make a whole ship for it you will have a lot of unhappy modules.
		/// </summary>
		public static void CaptureThumbnail(ShipConstruct ship, int resolution, string saveFolder, string craftName)
        {
            if (ship.shipFacility != EditorFacility.VAB)
            {
                CraftThumbnail.TakeSnaphot(ship, resolution, saveFolder, craftName, 35, 135, 35, 135, 0.9f);
            }
            else
            {
                CraftThumbnail.TakeSnaphot(ship, resolution, saveFolder, craftName, 45, 45, 45, 45, 0.9f);
            }
        }

        public static void CaptureThumbnail(ShipConstruct ship, int resolution,
                float elevation, float azimuth, float pitch, float heading, float fov, string saveFolder, string craftName)
        {
            CraftThumbnail.TakeSnaphot(ship, resolution, saveFolder, craftName, elevation, azimuth, pitch, heading, fov);
        }

        // For some reason Unity has no resize method, so we have to implement our own:
        public static Texture2D ResizeTexture(Texture2D input, int width, int height)
        {
            var small = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var rx = (float)input.width / (float)small.width;
            var ry = (float)input.height / (float)small.height;
            for (var y = 0; y < small.height; y++)
            {
                var sy = (int)Math.Round(ry * y);
                for (var x = 0; x < small.width; x++)
                {
                    var sx = (int)Math.Round(rx * x);
                    small.SetPixel(x, y, input.GetPixel(sx, sy));
                }
            }
            small.Apply();
            return small;
        }

        public static string GetThumbnailFilename(TemplateOrigin templateOrigin, string vesselName)
        {
            return HighLogic.SaveFolder + "_" + templateOrigin.ToString() + "_" + vesselName + ".png";
        }
        public static string GetThumbnailFilePath(TemplateOrigin templateOrigin, string vesselName)
        {
            return KSPUtil.ApplicationRootPath + "thumbs/" + GetThumbnailFilename(templateOrigin, vesselName);
        }

        public static bool CaptureThumbnailFromConfig(ConfigNode craftConfig, int resolution, string fullFilePath, bool isVab)
        {
            GameObject renderRoot = BuildCraftModelFromConfig(craftConfig);
            if (renderRoot == null) { return false; }

            try
            {
                renderRoot.SetLayerRecursive(0);
                CaptureThumbnailFromGameObject(renderRoot, resolution, fullFilePath, isVab);
            }
            finally
            {
				if (renderRoot?.gameObject != null) { DestroyImmediate(renderRoot.gameObject); }
				if (renderRoot == null) { DestroyImmediate(renderRoot); }
			}
			return File.Exists(fullFilePath);
		}

        private static void CaptureThumbnailFromGameObject(GameObject root, int resolution, string fullFilePath, bool isVab)
        {
            Bounds bounds = GetRendererBounds(root);
            float camFov = 30f;
            float elevation = isVab ? 45f : 35f;
            float azimuth = isVab ? 45f : 135f;
            float pitch = isVab ? 45f : 35f;
            float heading = isVab ? 45f : 135f;
            float fovFactor = 0.9f;
            float camDist = KSPCameraUtil.GetDistanceToFit(bounds.size, camFov * fovFactor);

            GameObject cameraObj = new("KSTS_SnapshotCamera");
            Camera camera = cameraObj.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = Color.clear;
            camera.fieldOfView = camFov;
            camera.cullingMask = 1;
            camera.enabled = false;
            camera.allowHDR = false;
            Light light = cameraObj.AddComponent<Light>();
            light.renderingLayerMask = 1;
            light.type = LightType.Spot;
            light.range = 100f;
            light.intensity = 0.5f;

            camera.transform.position = bounds.center + Quaternion.AngleAxis(azimuth, Vector3.up) * Quaternion.AngleAxis(elevation, Vector3.right) * (Vector3.back * camDist);
            camera.transform.rotation = Quaternion.AngleAxis(heading, Vector3.up) * Quaternion.AngleAxis(pitch, Vector3.right);
            Texture2D thumbTexture = RenderCamera(camera, resolution, resolution, 24);
            byte[] png = thumbTexture.EncodeToPNG();

            string dir = Path.GetDirectoryName(fullFilePath);
            if (!Directory.Exists(dir)) { Directory.CreateDirectory(dir); }
            File.WriteAllBytes(fullFilePath, png);
            DestroyImmediate(cameraObj);
            DestroyImmediate(thumbTexture);
        }

        private static Texture2D RenderCamera(Camera cam, int width, int height, int depth)
        {
            RenderTexture renderTexture = new(width, height, depth, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            renderTexture.Create();
            RenderTexture active = RenderTexture.active;
            RenderTexture.active = renderTexture;
            cam.targetTexture = renderTexture;
            cam.Render();
            Texture2D texture2D = new(width, height, TextureFormat.ARGB32, mipChain: false);
            texture2D.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, recalculateMipMaps: false);
            texture2D.Apply();
            RenderTexture.active = active;
            cam.targetTexture = null;
            renderTexture.Release();
            DestroyImmediate(renderTexture);
            return texture2D;
        }

        private static GameObject BuildCraftModelFromConfig(ConfigNode craftConfig)
        {
            if (craftConfig == null) { return null; }

            GameObject craftRoot = new("KSTS_ThumbnailRoot");
            craftRoot.SetActive(true);
            try
            {
                Dictionary<uint, GameObject> modelsByCraftId = new();
                Dictionary<uint, uint[]> childLinksByCraftId = new();

                for (int i = 0; i < craftConfig.nodes.Count; i++)
                {
                    ConfigNode partNode = craftConfig.nodes[i];
                    if (!partNode.name.Equals("part", StringComparison.OrdinalIgnoreCase) || !partNode.HasValue("part") || !partNode.HasValue("pos") || !partNode.HasValue("rot")) { continue; }

                    string partName = "";
                    string craftIdStr = "";
                    KSPUtil.GetPartInfo(partNode.GetValue("part"), ref partName, ref craftIdStr);
                    if (string.IsNullOrEmpty(partName) || string.IsNullOrEmpty(craftIdStr)) { continue; }
                    if (!uint.TryParse(craftIdStr, out uint craftId)) { continue; }

                    AvailablePart avPart = PartLoader.getPartInfoByName(partName);
                    if (avPart?.partPrefab == null) { continue; }

                    GameObject partModel = PartUtilsImpl.GetPartModel(avPart, partNode: partNode);
                    if (partModel == null) { continue; }
                    partModel.name = partName + "_" + craftIdStr;

                    partModel.transform.position = KSPUtil.ParseVector3(partNode.GetValue("pos"));
                    partModel.transform.rotation = KSPUtil.ParseQuaternion(partNode.GetValue("rot"));

                    StripToRenderOnly(partModel);
                    partModel.transform.SetParent(craftRoot.transform, worldPositionStays: true);
                    modelsByCraftId[craftId] = partModel;

                    string[] links = partNode.GetValues("link");
                    if (links != null && links.Length > 0)
                    {
                        List<uint> childIds = [];
                        for (int l = 0; l < links.Length; l++)
                        {
                            string idStr = KSPUtil.GetLinkID(links[l]);
                            if (uint.TryParse(idStr, out uint childId))
                            {
                                childIds.Add(childId);
                            }
                        }
                        if (childIds.Count > 0)
                        {
                            childLinksByCraftId[craftId] = [.. childIds];
                        }
                    }
                }

                foreach (KeyValuePair<uint, GameObject> kvp in modelsByCraftId)
                {
                    uint parentId = kvp.Key;
                    GameObject parentModel = kvp.Value;
                    if (!childLinksByCraftId.TryGetValue(parentId, out uint[] childIds)) { continue; }
                    for (int i = 0; i < childIds.Length; i++)
                    {
                        uint childId = childIds[i];
                        if (!modelsByCraftId.TryGetValue(childId, out GameObject childModel)) { continue; }
                        childModel.transform.SetParent(parentModel.transform, worldPositionStays: true);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[KSTS] Error building craft model: " + e.Message);
                DestroyImmediate(craftRoot.gameObject);
				DestroyImmediate(craftRoot);
				return null;
            }
            return craftRoot;
        }

        private static void StripToRenderOnly(GameObject root)
        {
            UnityEngine.Component[] components = root.GetComponents<UnityEngine.Component>();
            components = components.Concat(root.GetComponentsInChildren<UnityEngine.Component>(includeInactive: true)).ToArray();
            for (int i = components.Length - 1; i >= 0; i--)
            {
                UnityEngine.Component component = components[i];
                if (component == null) { continue; }
                if (component is Transform) { continue; }
                if (component is Renderer) { continue; }
                if (component is MeshFilter) { continue; }
                DestroyImmediate(component);                
            }
        }

        private static Bounds GetRendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers.Length == 0) { return new Bounds(root.transform.position, Vector3.one); }
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) { b.Encapsulate(renderers[i].bounds); }
            return b;
        }
    }
}
