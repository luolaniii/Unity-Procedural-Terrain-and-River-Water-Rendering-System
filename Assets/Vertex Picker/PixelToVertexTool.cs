using UnityEngine;
using UnityEditor;
using System.Linq;

/// <summary>
/// 像素到顶点工具 - 点击场景中的像素点获取对应网格顶点的世界坐标
/// 可在任何项目中复用的独立工具类
/// </summary>
public static class PixelToVertexTool
{
    /// <summary>
    /// 点击结果数据结构
    /// </summary>
    public struct VertexClickResult
    {
        public bool success;
        public Vector3 worldPosition;
        public int vertexIndex;
        public GameObject hitObject;
        public float distanceToHitPoint;
        public string errorMessage;

        public static VertexClickResult Success(Vector3 position, int index, GameObject obj, float distance)
        {
            return new VertexClickResult
            {
                success = true,
                worldPosition = position,
                vertexIndex = index,
                hitObject = obj,
                distanceToHitPoint = distance,
                errorMessage = ""
            };
        }

        public static VertexClickResult Failure(string message)
        {
            return new VertexClickResult
            {
                success = false,
                worldPosition = Vector3.zero,
                vertexIndex = -1,
                hitObject = null,
                distanceToHitPoint = 0,
                errorMessage = message
            };
        }
    }

    /// <summary>
    /// 从屏幕坐标获取最近顶点的世界坐标
    /// </summary>
    /// <param name="screenPoint">屏幕坐标</param>
    /// <param name="maxDistance">射线检测最大距离</param>
    /// <param name="layerMask">射线检测的层遮罩，默认检测所有层</param>
    /// <returns>点击结果</returns>
    public static VertexClickResult GetVertexWorldPosition(Vector2 screenPoint, float maxDistance = 1000f, int layerMask = -1)
    {
        // 将屏幕坐标转换为射线
        Ray ray = HandleUtility.GUIPointToWorldRay(screenPoint);

        // 执行射线检测
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, layerMask))
        {
            return GetNearestVertexFromHit(hit);
        }

        return VertexClickResult.Failure("No object hit by raycast");
    }

    /// <summary>
    /// 从射线检测结果获取最近顶点的世界坐标
    /// </summary>
    /// <param name="hit">射线检测结果</param>
    /// <returns>点击结果</returns>
    public static VertexClickResult GetNearestVertexFromHit(RaycastHit hit)
    {
        GameObject hitObject = hit.transform.gameObject;

        // 获取MeshFilter组件
        MeshFilter meshFilter = hitObject.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            return VertexClickResult.Failure("Hit object has no MeshFilter component");
        }

        Mesh mesh = meshFilter.sharedMesh;
        if (mesh == null)
        {
            return VertexClickResult.Failure("MeshFilter has no mesh assigned");
        }

        // 获取顶点数组
        Vector3[] vertices = mesh.vertices;
        if (vertices == null || vertices.Length == 0)
        {
            return VertexClickResult.Failure("Mesh has no vertices");
        }

        // 将顶点从本地坐标转换为世界坐标
        Vector3[] worldVertices = new Vector3[vertices.Length];
        Transform transform = hitObject.transform;

        for (int i = 0; i < vertices.Length; i++)
        {
            worldVertices[i] = transform.TransformPoint(vertices[i]);
        }

        // 找到距离点击点最近的顶点
        int nearestIndex = 0;
        float nearestDistance = Vector3.Distance(worldVertices[0], hit.point);

        for (int i = 1; i < worldVertices.Length; i++)
        {
            float distance = Vector3.Distance(worldVertices[i], hit.point);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        return VertexClickResult.Success(worldVertices[nearestIndex], nearestIndex, hitObject, nearestDistance);
    }

    /// <summary>
    /// 获取网格的所有顶点世界坐标
    /// </summary>
    /// <param name="meshObject">包含网格的游戏对象</param>
    /// <returns>世界坐标顶点数组</returns>
    public static Vector3[] GetMeshWorldVertices(GameObject meshObject)
    {
        MeshFilter meshFilter = meshObject.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            return null;
        }

        Vector3[] localVertices = meshFilter.sharedMesh.vertices;
        Vector3[] worldVertices = new Vector3[localVertices.Length];

        for (int i = 0; i < localVertices.Length; i++)
        {
            worldVertices[i] = meshObject.transform.TransformPoint(localVertices[i]);
        }

        return worldVertices;
    }

    /// <summary>
    /// 在Scene视图中可视化顶点（用于调试）
    /// </summary>
    /// <param name="vertices">要可视化的顶点数组</param>
    /// <param name="color">显示颜色</param>
    /// <param name="size">显示大小</param>
    public static void VisualizeVertices(Vector3[] vertices, Color color, float size = 0.1f)
    {
        if (vertices == null) return;

        Color originalColor = Gizmos.color;
        Gizmos.color = color;

        foreach (Vector3 vertex in vertices)
        {
            Gizmos.DrawSphere(vertex, size);
        }

        Gizmos.color = originalColor;
    }

    /// <summary>
    /// 在Scene视图中可视化单个顶点（用于调试）
    /// </summary>
    /// <param name="vertex">要可视化的顶点</param>
    /// <param name="color">显示颜色</param>
    /// <param name="size">显示大小</param>
    public static void VisualizeVertex(Vector3 vertex, Color color, float size = 0.1f)
    {
        Color originalColor = Gizmos.color;
        Gizmos.color = color;
        Gizmos.DrawSphere(vertex, size);
        Gizmos.color = originalColor;
    }
}
