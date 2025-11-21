using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

/// <summary>
/// 独立的顶点选择器窗口 - 可在Unity编辑器的Window菜单中找到
/// 提供像素到顶点坐标转换功能
/// </summary>
public class VertexPickerWindow : EditorWindow
{
    [MenuItem("Window/Vertex Picker", false, 2000)]
    static void ShowWindow()
    {
        VertexPickerWindow window = GetWindow<VertexPickerWindow>("Vertex Picker");
        window.minSize = new Vector2(400, 500);
        window.Show();
    }

    // UI相关变量
    private bool isPickingModeActive = false;
    private PixelToVertexTool.VertexClickResult lastResult;
    private Vector2 scrollPosition;

    // 设置相关变量
    private float maxRaycastDistance = 1000f;
    private int raycastLayerMask = -1; // 默认所有层
    private bool showGizmos = true;
    private float gizmoSize = 0.05f;
    private bool useExactIntersection = true; // 是否使用精确交点而不是最近顶点
    private bool placeSelectedObject = false;

    void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        // 标题
        GUILayout.Label("🎯 Vertex Picker Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 功能说明
        EditorGUILayout.HelpBox(
            "This tool allows you to click anywhere in the Scene view to get the world position of the nearest vertex on any mesh.\n\n" +
            "1. Enable Picking Mode\n" +
            "2. Click in Scene view\n" +
            "3. View results below",
            MessageType.Info
        );

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // 设置面板
        EditorGUILayout.LabelField("⚙️ Settings", EditorStyles.boldLabel);

        maxRaycastDistance = EditorGUILayout.FloatField("Max Raycast Distance", maxRaycastDistance);

        EditorGUILayout.BeginHorizontal();
        raycastLayerMask = EditorGUILayout.IntField("Raycast Layer Mask", raycastLayerMask);
        if (GUILayout.Button("?", GUILayout.Width(20)))
        {
            EditorUtility.DisplayDialog("Layer Mask Help",
                "-1 = All layers\n" +
                "0 = Default layer only\n" +
                "1 = Layer 1 only\n" +
                "You can combine layers using bitwise OR (e.g., 1 | 2 | 4)",
                "OK");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        useExactIntersection = EditorGUILayout.Toggle("Use Exact Intersection", useExactIntersection);
        EditorGUILayout.HelpBox(useExactIntersection ?
            "Returns the exact ray-surface intersection point (most accurate)" :
            "Returns the nearest vertex to the intersection point", MessageType.Info);

        EditorGUILayout.Space();
        showGizmos = EditorGUILayout.Toggle("Show Gizmos", showGizmos);
        if (showGizmos)
        {
            gizmoSize = EditorGUILayout.Slider("Gizmo Size", gizmoSize, 0.01f, 0.5f);
        }

        EditorGUILayout.Space();
        placeSelectedObject = EditorGUILayout.Toggle("Move Selected Object", placeSelectedObject);
        if (placeSelectedObject && Selection.activeTransform == null)
        {
            EditorGUILayout.HelpBox("请选择一个需要移动的物体。Scene 视图点击时将把它移动到拾取位置。", MessageType.Info);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // 模式切换
        EditorGUILayout.LabelField("🎮 Control", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        isPickingModeActive = EditorGUILayout.Toggle("Picking Mode Active", isPickingModeActive);
        if (EditorGUI.EndChangeCheck())
        {
            if (isPickingModeActive)
            {
                SceneView.duringSceneGui += OnSceneGUI;
                Debug.Log("Vertex Picker: Picking mode activated. Click in Scene view to pick vertices.");
            }
            else
            {
                SceneView.duringSceneGui -= OnSceneGUI;
                Debug.Log("Vertex Picker: Picking mode deactivated.");
            }
        }

        if (isPickingModeActive)
        {
            EditorGUILayout.HelpBox("🔴 ACTIVE: Click anywhere in the Scene view to pick a vertex.", MessageType.Warning);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // 结果显示
        EditorGUILayout.LabelField("📊 Last Result", EditorStyles.boldLabel);

        if (lastResult.success)
        {
            DisplaySuccessResult();
        }
        else if (!string.IsNullOrEmpty(lastResult.errorMessage))
        {
            EditorGUILayout.HelpBox($"❌ Error: {lastResult.errorMessage}", MessageType.Error);
        }
        else
        {
            EditorGUILayout.HelpBox("📍 No vertex selected yet. Enable picking mode and click in Scene view.", MessageType.Info);
        }

        EditorGUILayout.EndScrollView();
    }

    void DisplaySuccessResult()
    {
        EditorGUILayout.BeginVertical("box");

        // 基本信息
        EditorGUILayout.LabelField("Object:", lastResult.hitObject != null ? lastResult.hitObject.name : "Unknown");

        if (useExactIntersection)
        {
            EditorGUILayout.LabelField("Type:", "Exact Intersection Point");
        }
        else
        {
            EditorGUILayout.LabelField("Vertex Index:", lastResult.vertexIndex.ToString());
            EditorGUILayout.LabelField("Distance to Hit:", $"{lastResult.distanceToHitPoint:F3} units");
        }

        EditorGUILayout.Space();

        // 坐标信息
        EditorGUILayout.LabelField("World Position:", EditorStyles.boldLabel);
        EditorGUILayout.Vector3Field("", lastResult.worldPosition);

        EditorGUILayout.Space();

        // 实用按钮
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("📋 Copy Position"))
        {
            string positionText = $"{lastResult.worldPosition.x:F6}, {lastResult.worldPosition.y:F6}, {lastResult.worldPosition.z:F6}";
            EditorGUIUtility.systemCopyBuffer = positionText;
            ShowNotification(new GUIContent($"Position copied: {positionText}"));
            Debug.Log($"Vertex Position copied: {positionText}");
        }

        if (GUILayout.Button("📝 Copy Vector3"))
        {
            string vectorText = $"new Vector3({lastResult.worldPosition.x:F6}f, {lastResult.worldPosition.y:F6}f, {lastResult.worldPosition.z:F6}f)";
            EditorGUIUtility.systemCopyBuffer = vectorText;
            ShowNotification(new GUIContent("Vector3 code copied"));
            Debug.Log($"Vector3 code copied: {vectorText}");
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("📊 Log Details"))
        {
            if (useExactIntersection)
            {
                Debug.Log($"[Vertex Picker] Exact intersection at {lastResult.worldPosition} on {lastResult.hitObject.name}");
            }
            else
            {
                Debug.Log($"[Vertex Picker] Selected vertex {lastResult.vertexIndex} on {lastResult.hitObject.name} at {lastResult.worldPosition}");
            }
            ShowNotification(new GUIContent("Details logged"));
        }

        if (GUILayout.Button("🎯 Select Object"))
        {
            Selection.activeGameObject = lastResult.hitObject;
            ShowNotification(new GUIContent("Object selected"));
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    void OnSceneGUI(SceneView sceneView)
    {
        if (!isPickingModeActive) return;

        // 处理鼠标事件
        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0) // 左键点击
        {
            // 在OnSceneGUI中，e.mousePosition已经是相对于Scene视图的坐标
            Vector2 mousePos = e.mousePosition;

            // 使用 HandleUtility 生成与 PixelToVertexTool 一致的射线，避免坐标偏差
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);

            // 执行射线检测
            if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, raycastLayerMask))
            {
                PixelToVertexTool.VertexClickResult result;

                if (useExactIntersection)
                {
                    // 返回精确的射线-表面交点
                    result = PixelToVertexTool.VertexClickResult.Success(
                        hit.point,
                        -1, // 没有具体的顶点索引
                        hit.transform.gameObject,
                        0f // 交点处距离为0
                    );
                }
                else
                {
                    // 返回最近的顶点
                    result = PixelToVertexTool.GetNearestVertexFromHit(hit);
                }

                lastResult = result;

                if (result.success)
                {
                    if (useExactIntersection)
                    {
                        Debug.Log($"[Vertex Picker] Exact intersection at {result.worldPosition} on {result.hitObject.name}");
                    }
                    else
                    {
                        Debug.Log($"[Vertex Picker] Selected vertex {result.vertexIndex} on {result.hitObject.name} at {result.worldPosition}");
                    }

                    if (placeSelectedObject)
                    {
                        Transform activeTransform = Selection.activeTransform;
                        if (activeTransform != null)
                        {
                            Undo.RecordObject(activeTransform, "Move Selected Object");
                            activeTransform.position = result.worldPosition;
                            EditorUtility.SetDirty(activeTransform);
                        }
                    }
                }
                else
                {
                    Debug.LogWarning($"[Vertex Picker] Selection failed: {result.errorMessage}");
                }
            }
            else
            {
                lastResult = PixelToVertexTool.VertexClickResult.Failure("No object hit by raycast");
                Debug.LogWarning("[Vertex Picker] No object hit by raycast");
            }

            e.Use();
            Repaint();
            sceneView.Repaint();
        }

        // 显示Gizmos
        if (showGizmos && lastResult.success)
        {
            DrawGizmos();
        }
    }

    void DrawGizmos()
    {
        if (Event.current.type != EventType.Repaint) return;

        // 设置颜色和绘制顶点
        Handles.color = Color.yellow;
        Handles.SphereHandleCap(0, lastResult.worldPosition, Quaternion.identity, gizmoSize, EventType.Repaint);

        // 显示坐标标签
        string labelText = useExactIntersection ?
            $"Intersection\n{lastResult.worldPosition:F2}" :
            $"Vertex {lastResult.vertexIndex}\n{lastResult.worldPosition:F2}";

        Handles.Label(lastResult.worldPosition + Vector3.up * gizmoSize * 2,
            labelText,
            EditorStyles.whiteBoldLabel);
    }

    void OnDestroy()
    {
        // 确保清理事件监听器
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    void OnDisable()
    {
        // 确保清理事件监听器
        SceneView.duringSceneGui -= OnSceneGUI;
    }
}
