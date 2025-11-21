#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MaterialParameterTool.Editor
{
    /// <summary>
    /// 快速搜索并调节材质属性的工具窗口。
    /// Window -> Material Parameter Finder
    /// </summary>
    public class MaterialParameterFinderWindow : EditorWindow
    {
        private string _propertyKeyword = string.Empty;
        private string _materialKeyword = string.Empty;
        private bool _includeHidden = false;
        private Vector2 _scrollPosition;
        private readonly List<MaterialPropertyEntry> _results = new List<MaterialPropertyEntry>();

        private class MaterialPropertyEntry
        {
            public Material material;
            public string displayName;
            public string propertyName;
            public ShaderUtil.ShaderPropertyType propertyType;
            public int propertyIndex;
        }

        [MenuItem("Window/Material Parameter Finder", priority = 2050)]
        public static void ShowWindow()
        {
            var window = GetWindow<MaterialParameterFinderWindow>("Material Finder");
            window.minSize = new Vector2(420f, 300f);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("🔍 Material Parameter Finder", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUI.BeginChangeCheck();
                _propertyKeyword = EditorGUILayout.TextField("Property Keyword", _propertyKeyword);
                _materialKeyword = EditorGUILayout.TextField("Material Filter", _materialKeyword);
                _includeHidden = EditorGUILayout.ToggleLeft("Include Hidden Properties", _includeHidden);
                bool autoSearch = EditorGUI.EndChangeCheck();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Search", GUILayout.Height(24)))
                    {
                        Search();
                    }

                    if (GUILayout.Button("Clear Results", GUILayout.Height(24)))
                    {
                        _results.Clear();
                    }
                }

                if (autoSearch && !string.IsNullOrEmpty(_propertyKeyword))
                {
                    Search();
                }
            }

            EditorGUILayout.Space();
            DrawResults();
        }

        private void Search()
        {
            _results.Clear();
            string[] materialGuids = AssetDatabase.FindAssets("t:Material");

            for (int i = 0; i < materialGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(materialGuids[i]);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) continue;

                if (!string.IsNullOrEmpty(_materialKeyword) &&
                    material.name.IndexOf(_materialKeyword, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                Shader shader = material.shader;
                if (shader == null) continue;

                int propertyCount = ShaderUtil.GetPropertyCount(shader);
                for (int propertyIndex = 0; propertyIndex < propertyCount; propertyIndex++)
                {
                    if (!_includeHidden && ShaderUtil.IsShaderPropertyHidden(shader, propertyIndex))
                    {
                        continue;
                    }

                    string propertyName = ShaderUtil.GetPropertyName(shader, propertyIndex);
                    string displayName = ShaderUtil.GetPropertyDescription(shader, propertyIndex);
                    var propertyType = ShaderUtil.GetPropertyType(shader, propertyIndex);

                    if (!string.IsNullOrEmpty(_propertyKeyword) &&
                        displayName.IndexOf(_propertyKeyword, System.StringComparison.OrdinalIgnoreCase) < 0 &&
                        propertyName.IndexOf(_propertyKeyword, System.StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    _results.Add(new MaterialPropertyEntry
                    {
                        material = material,
                        displayName = string.IsNullOrEmpty(displayName) ? propertyName : displayName,
                        propertyName = propertyName,
                        propertyType = propertyType,
                        propertyIndex = propertyIndex
                    });
                }
            }

            _results.Sort((a, b) =>
            {
                int materialCompare = string.Compare(a.material.name, b.material.name, System.StringComparison.OrdinalIgnoreCase);
                if (materialCompare != 0) return materialCompare;
                return string.Compare(a.propertyName, b.propertyName, System.StringComparison.OrdinalIgnoreCase);
            });
        }

        private void DrawResults()
        {
            EditorGUILayout.LabelField($"Results ({_results.Count})", EditorStyles.boldLabel);

            if (_results.Count == 0)
            {
                EditorGUILayout.HelpBox("没有找到匹配的材质属性。请尝试调整搜索关键词。", MessageType.Info);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            string currentMaterialName = null;

            foreach (var entry in _results)
            {
                if (currentMaterialName != entry.material.name)
                {
                    currentMaterialName = entry.material.name;
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField(currentMaterialName, EditorStyles.toolbarDropDown);
                }

                using (new EditorGUILayout.HorizontalScope("box"))
                {
                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField($"{entry.displayName} ({entry.propertyName})", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Type: {entry.propertyType}", EditorStyles.miniLabel);
                    EditorGUILayout.EndVertical();

                    GUILayout.FlexibleSpace();
                    DrawPropertyField(entry);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawPropertyField(MaterialPropertyEntry entry)
        {
            Material material = entry.material;
            string propertyName = entry.propertyName;

            EditorGUI.BeginChangeCheck();
            object newValue = null;

            switch (entry.propertyType)
            {
                case ShaderUtil.ShaderPropertyType.Color:
                    newValue = EditorGUILayout.ColorField(material.GetColor(propertyName), GUILayout.Width(130f));
                    break;
                case ShaderUtil.ShaderPropertyType.Vector:
                    newValue = EditorGUILayout.Vector4Field("", material.GetVector(propertyName), GUILayout.Width(200f));
                    break;
                case ShaderUtil.ShaderPropertyType.Float:
                case ShaderUtil.ShaderPropertyType.Range:
                    float currentValue = material.GetFloat(propertyName);
                    if (entry.propertyType == ShaderUtil.ShaderPropertyType.Range)
                    {
                        float min = ShaderUtil.GetRangeLimits(material.shader, entry.propertyIndex, 1);
                        float max = ShaderUtil.GetRangeLimits(material.shader, entry.propertyIndex, 2);
                        newValue = EditorGUILayout.Slider(currentValue, min, max, GUILayout.Width(180f));
                    }
                    else
                    {
                        newValue = EditorGUILayout.FloatField(currentValue, GUILayout.Width(120f));
                    }
                    break;
                case ShaderUtil.ShaderPropertyType.TexEnv:
                    Texture currentTex = material.GetTexture(propertyName);
                    newValue = EditorGUILayout.ObjectField(currentTex, typeof(Texture), false, GUILayout.Width(160f));
                    break;
                default:
                    EditorGUILayout.LabelField("N/A", GUILayout.Width(60f));
                    return;
            }

            if (EditorGUI.EndChangeCheck())
            {
                ApplyMaterialChange(material, propertyName, entry.propertyType, newValue);
            }
        }

        private static void ApplyMaterialChange(Material material, string propertyName, ShaderUtil.ShaderPropertyType propertyType, object value)
        {
            if (material == null) return;

            Undo.RecordObject(material, "Modify Material Property");

            switch (propertyType)
            {
                case ShaderUtil.ShaderPropertyType.Color:
                    material.SetColor(propertyName, (Color)value);
                    break;
                case ShaderUtil.ShaderPropertyType.Vector:
                    material.SetVector(propertyName, (Vector4)value);
                    break;
                case ShaderUtil.ShaderPropertyType.Float:
                case ShaderUtil.ShaderPropertyType.Range:
                    material.SetFloat(propertyName, (float)value);
                    break;
                case ShaderUtil.ShaderPropertyType.TexEnv:
                    material.SetTexture(propertyName, (Texture)value);
                    break;
            }

            EditorUtility.SetDirty(material);
        }
    }
}
#endif

