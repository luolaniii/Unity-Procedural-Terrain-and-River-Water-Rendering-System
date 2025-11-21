using UnityEngine;

/// <summary>
/// Vertex Picker工具的使用示例
/// 演示如何在代码中使用PixelToVertexTool
/// </summary>
public class Examples : MonoBehaviour
{
    void Update()
    {
        // 示例1: 从屏幕坐标获取顶点
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 screenPoint = Input.mousePosition;
            var result = PixelToVertexTool.GetVertexWorldPosition(screenPoint);

            if (result.success)
            {
                Debug.Log($"Clicked vertex at: {result.worldPosition}");
                // 在点击位置创建一个标记
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.transform.position = result.worldPosition;
                marker.transform.localScale = Vector3.one * 0.1f;
            }
        }

        // 示例2: 从自定义射线检测获取最近顶点
        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                var result = PixelToVertexTool.GetNearestVertexFromHit(hit);
                if (result.success)
                {
                    Debug.Log($"Nearest vertex: {result.worldPosition}, Distance: {result.distanceToHitPoint}");
                }
            }
        }
    }

    /// <summary>
    /// 示例方法：批量处理多个点
    /// </summary>
    public void ProcessMultiplePoints(Vector2[] screenPoints)
    {
        foreach (Vector2 point in screenPoints)
        {
            var result = PixelToVertexTool.GetVertexWorldPosition(point);
            if (result.success)
            {
                ProcessPoint(result.worldPosition, result.hitObject);
            }
        }
    }

    /// <summary>
    /// 处理单个点的示例方法
    /// </summary>
    private void ProcessPoint(Vector3 worldPosition, GameObject hitObject)
    {
        // 在这里添加你的逻辑
        // 例如：放置对象、记录坐标、生成路径等

        Debug.Log($"Processing point: {worldPosition} on {hitObject.name}");

        // 示例：创建一个路径点
        GameObject waypoint = new GameObject("Waypoint");
        waypoint.transform.position = worldPosition;
    }

    /// <summary>
    /// 示例：测量两个点击点之间的距离
    /// </summary>
    public void MeasureDistanceExample()
    {
        // 这个示例展示了如何使用工具进行距离测量
        // 在实际使用中，你可能需要存储第一次点击的位置
    }
}

