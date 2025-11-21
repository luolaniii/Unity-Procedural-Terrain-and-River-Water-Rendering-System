#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RiverTools.Editor
{
    public static class TerrainHeightmapExporterUtility
    {
        private const string MenuRoot = "Tools/River/";

        private const string DefaultFolder = "Assets/河流水效果/Textures/Generated";

        [MenuItem(MenuRoot + "Bake Terrain Heightmap Asset", priority = 1)]
        public static void BakeTerrainHeightmapAsset()
        {
            ExportSelectedTerrain(EncodeMode.Png, useSavePanel: false);
        }

        [MenuItem(MenuRoot + "Export Selected Terrain Heightmap (PNG)", priority = 10)]
        public static void ExportSelectedTerrainAsPng()
        {
            ExportSelectedTerrain(EncodeMode.Png, useSavePanel: true);
        }

        [MenuItem(MenuRoot + "Export Selected Terrain Heightmap (EXR)", priority = 11)]
        public static void ExportSelectedTerrainAsExr()
        {
            ExportSelectedTerrain(EncodeMode.Exr, useSavePanel: true);
        }

        [MenuItem(MenuRoot + "Bake Terrain Heightmap Asset", true)]
        [MenuItem(MenuRoot + "Export Selected Terrain Heightmap (PNG)", true)]
        [MenuItem(MenuRoot + "Export Selected Terrain Heightmap (EXR)", true)]
        private static bool ValidateTerrainSelected()
        {
            return GetSelectedTerrain() != null;
        }

        private enum EncodeMode
        {
            Png,
            Exr
        }

        private static void ExportSelectedTerrain(EncodeMode encodeMode, bool useSavePanel)
        {
            var terrain = GetSelectedTerrain();
            if (terrain == null)
            {
                EditorUtility.DisplayDialog("Export Terrain Heightmap",
                    "请选择一个包含 Terrain 组件的对象。", "好的");
                return;
            }

            var terrainData = terrain.terrainData;
            if (terrainData == null)
            {
                EditorUtility.DisplayDialog("Export Terrain Heightmap",
                    "选中的 Terrain 没有可用的 TerrainData。", "好的");
                return;
            }

            int resolution = terrainData.heightmapResolution;
            var heights = terrainData.GetHeights(0, 0, resolution, resolution);

            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true);

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float normalized = heights[y, x];
                    texture.SetPixel(x, y, new Color(normalized, normalized, normalized, 1f));
                }
            }

            texture.Apply();

            string defaultName = terrain.name + "_Heightmap";
            string extension = encodeMode == EncodeMode.Png ? "png" : "exr";
            string savePath;

            if (useSavePanel)
            {
                string panelTitle = $"Export Terrain Heightmap ({extension.ToUpperInvariant()})";
                savePath = EditorUtility.SaveFilePanel(panelTitle, "", defaultName, extension);
                if (string.IsNullOrEmpty(savePath))
                {
                    Object.DestroyImmediate(texture);
                    return;
                }
            }
            else
            {
                string projectFolder = Path.Combine(Application.dataPath, DefaultFolder.Substring("Assets/".Length));
                if (!Directory.Exists(projectFolder))
                {
                    Directory.CreateDirectory(projectFolder);
                }

                savePath = Path.Combine(projectFolder, $"{defaultName}.{extension}");
            }

            byte[] bytes = encodeMode == EncodeMode.Png
                ? texture.EncodeToPNG()
                : texture.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat | Texture2D.EXRFlags.CompressZIP);

            File.WriteAllBytes(savePath, bytes);
            Object.DestroyImmediate(texture);

            if (!useSavePanel && savePath.StartsWith(Application.dataPath))
            {
                string assetPath = "Assets" + savePath.Substring(Application.dataPath.Length);
                AssetDatabase.ImportAsset(assetPath);
                ConfigureTextureImporter(assetPath);
                EditorUtility.DisplayDialog("Bake Terrain Heightmap Asset",
                    $"高度图已生成：\n{assetPath}", "完成");
            }
            else
            {
                EditorUtility.DisplayDialog("Export Terrain Heightmap",
                    $"高度图已导出到：\n{savePath}", "完成");
            }
        }

        private static Terrain GetSelectedTerrain()
        {
            if (Selection.activeGameObject == null)
            {
                return null;
            }

            return Selection.activeGameObject.GetComponent<Terrain>();
        }

        private static void ConfigureTextureImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
    }
}
#endif

