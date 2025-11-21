using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RiverWidthInfo {
    public float LengthRadio;
    public float halfWidth;
}

[System.Serializable]
public class WayPoint {
    public Vector3 pos;
}

[RequireComponent (typeof (MeshFilter))]
[RequireComponent (typeof (MeshRenderer))]
public class River : MonoBehaviour {

    //网格分辨率（大地形需要更高的分辨率）
    public int xSize = 200;
    public int ySize = 200;
    public bool showGizmos = true;
    //每个顶点间的距离（大地形需要更大的间距来覆盖全图）
    public Vector2 offsetBetweenVert = new Vector2 (50f, 50f);

    //河流整个长度对应的宽度变化
    public AnimationCurve riverWidthWholeLengthCurve = AnimationCurve.Linear (0f, 0f, 1f, 1f);
    //河流整体宽度变化随机程度
    [Range (0, 5f)] public float riverRandomWidthWholeLengthScale = 1f;
    //河床弯曲曲线
    public AnimationCurve riverBedCurve = AnimationCurve.EaseInOut (0f, 0f, 1f, 1f);
    //河流随机因子
    public Vector2 riverRandomRadio = Vector2.one;
    //最大河流宽度
    public float maxRiverWidth;
    public float riverExpandWidth = 2;
    //河流深度
    public float riverDepth = 1;
    public float riverSickRadio = 0.3f;
    //UV Repeat长度
    public float repeatLength;
    //每节最小间隔
    public int amountRadio = 3;
    //路径节点
    public List<WayPoint> wayPoints = new List<WayPoint> ();
    //高度图
    public Texture2D heightTexture;
    [Range (0, 2000f)]
    public float maxTerrainHeight = 500;
    public Material riverMat;

    [Header("真实地形修改")]
    [Tooltip("要修改的目标地形（拖拽场景中的Terrain对象）")]
    public Terrain targetTerrain;
    [Tooltip("是否在生成河流时自动备份地形")]
    public bool autoBackupTerrain = true;
    [Tooltip("是否修改真实地形（关闭则只修改参考网格）")]
    public bool modifyRealTerrain = true;
    [Tooltip("是否对河床进行平滑处理（提高平滑度，但会增加计算时间）")]
    public bool smoothRiverBed = true;
    [Tooltip("平滑迭代次数（越大越平滑，但越慢）")]
    [Range(1, 10)]
    public int smoothIterations = 3;
    [Tooltip("平滑半径（像素数，越大越平滑，但越慢）")]
    [Range(1, 5)]
    public int smoothRadius = 2;

    [Header("运行时显示设置")]
    [Tooltip("是否在运行时显示地形参考网格（关闭则只在编辑器中可见，不影响河流网格）")]
    public bool showRiverMeshInRuntime = false;

    MeshFilter m_filter;
    MeshRenderer m_renderer;
    Mesh m_mesh;
    bool m_isDrawing;
    Vector3 m_lastDrawPoint;
    float m_lastMaxRiverUVY;

    
    // 私有变量：存储原始高度图备份
    private float[,] m_originalTerrainHeights;
    private bool m_hasBackup = false;

    GameObject m_riverObject;
    RiverInstance m_riverInstance;
    GameObject RiverObject {
        get {
            // m_riverObject = GameObject.Find ("RiverObject");
            if (m_riverObject == null) {
                m_riverObject = new GameObject ("RiverObject");
                m_riverObject.transform.SetParent (transform);
                //注意这里的layer和Raycast中lay的区别
                m_riverObject.layer = 4; // Water层
                m_riverObject.tag = MyWaterVolume.TAG;
                RiverObject.AddComponent<MeshRenderer> ();
                RiverObject.AddComponent<MeshFilter> ();
                RiverObject.AddComponent<MyWaterVolume> ();
                m_riverInstance = RiverObject.AddComponent<RiverInstance>();
            }
            return m_riverObject;
        }
    }

    public bool IsOnDraw {
        get => this.m_isDrawing;
        set => m_isDrawing = value;
    }

    void Start()
    {
        // 运行时检查并隐藏地形参考网格（如果设置了不显示）
        // 注意：河流网格始终显示，不受此选项影响
        if (!showRiverMeshInRuntime)
        {
            // 隐藏地形参考网格
            if (m_renderer == null)
            {
                m_renderer = GetComponent<MeshRenderer>();
            }
            if (m_renderer != null)
            {
                m_renderer.enabled = false;
            }
        }
    }

    public void CreateMeshPrepare () {
        List<GameObject> _childs = new List<GameObject> ();
        for (int i = 0; i < transform.childCount; i++) {
            GameObject _child = transform.GetChild (i).gameObject;
            _childs.Add (_child);
            //注意：这里不能直接在里面删除子物体
        }
        for (int i = 0; i < _childs.Count; i++) {
            DestroyImmediate (_childs[i]);
        }
        m_riverObject = null;
    }

    public void BeginDrawPath () {
        wayPoints.Clear ();
        m_riverObject = null;

        if (m_renderer != null) {
            MeshCollider _terrainMeshCollider = m_renderer.GetComponent<MeshCollider> ();
            if (_terrainMeshCollider != null) _terrainMeshCollider.enabled = true;
        }

    }

    public void EndDrawPath () {
        //禁用网格碰撞
        if (m_renderer != null) {
            MeshCollider _terrainMeshCollider = m_renderer.GetComponent<MeshCollider> ();
            if (_terrainMeshCollider != null) _terrainMeshCollider.enabled = false;
        }
    }

    public void RecordCurrentPoint (Vector3 p) {
        m_lastDrawPoint = p;
        wayPoints.Add (new WayPoint () { pos = p });
    }

    //创建网格
    public void CreateMesh () {

        m_renderer = GetComponent<MeshRenderer> ();
        m_filter = GetComponent<MeshFilter> ();
        m_mesh = m_filter.sharedMesh;
        if (m_mesh == null)
        {
            m_mesh = new Mesh();
            m_filter.sharedMesh = m_mesh;
        }

        if (heightTexture == null)
        {
            Debug.LogError("River.CreateMesh 缺少 heightTexture，请先在 Inspector 中指定高度图。");
            return;
        }
        MeshCollider meshCollider = GetComponent<MeshCollider> ();
        if (meshCollider != null) DestroyImmediate (meshCollider);

        m_mesh.name = "Procedural Grid";

        Vector3[] vertices = new Vector3[(xSize + 1) * (ySize + 1)];
        Vector2[] uv = new Vector2[vertices.Length];
        Vector4[] tangents = new Vector4[vertices.Length];
        Vector4 tangent = new Vector4 (1f, 0f, 0f, -1f);
        for (int i = 0, y = 0; y <= ySize; y++) {
            for (int x = 0; x <= xSize; x++, i++) {

                Vector2 _coords = new Vector2 ((float) x / xSize, (float) y / ySize);
                //图片采样坐标
                int _texcoordX = (int) (heightTexture.width * _coords.x);
                int _texcoordY = (int) (heightTexture.height * _coords.y);
                //采样图片颜色
                float _r = heightTexture.GetPixel (_texcoordX, _texcoordY).r;
                //顶点位置
                //注意：需要在创建网格的时候就确定顶点的高度，否则顶点的排列可能会被优化而出现错误的顺序
                vertices[i] = new Vector3 (x * offsetBetweenVert.x, maxTerrainHeight * _r, y * offsetBetweenVert.y);
                uv[i] = new Vector2 ((float) x / xSize, (float) y / ySize);
                tangents[i] = tangent;
            }
        }

        int[] triangles = new int[xSize * ySize * 6];
        for (int ti = 0, vi = 0, y = 0; y < ySize; y++, vi++) {
            for (int x = 0; x < xSize; x++, ti += 6, vi++) {
                triangles[ti] = vi;
                triangles[ti + 3] = triangles[ti + 2] = vi + 1;
                triangles[ti + 4] = triangles[ti + 1] = vi + xSize + 1;
                triangles[ti + 5] = vi + xSize + 2;
            }
        }

        m_mesh.vertices = vertices;
        m_mesh.uv = uv;
        m_mesh.triangles = triangles;
        m_mesh.tangents = tangents;

        m_mesh.RecalculateNormals ();
        m_mesh.RecalculateBounds ();

        gameObject.AddComponent<MeshCollider> ().enabled = true;

        //运行时隐藏地形参考网格（如果设置了不显示）
        if (Application.isPlaying && !showRiverMeshInRuntime)
        {
            if (m_renderer != null)
            {
                m_renderer.enabled = false;
            }
        }

    }

    //创建河流网格
    public void CreateRiverMesh () {

        Vector3[] _wayPoints = wayPoints.Select (x => x.pos).ToArray ();
        List<Vector3> _resultWayPoints = new List<Vector3> ();
        PathHelper.GetWayPoints (_wayPoints, amountRadio, ref _resultWayPoints);

        RiverObject.transform.position = _wayPoints[0];
        MeshRenderer _riverRenderer = RiverObject.GetComponent<MeshRenderer> ();
        MeshFilter _riverFilter = RiverObject.GetComponent<MeshFilter> ();

        MeshCollider _meshCollider = RiverObject.GetComponent<MeshCollider> ();
        if (_meshCollider == null) _meshCollider = RiverObject.AddComponent<MeshCollider> ();
        DestroyImmediate (_meshCollider);

        Mesh _riverMesh = new Mesh ();
        _riverMesh.name = "RiverMesh";

        //顶点
        RiverVertexCaculate (_riverMesh, ref _resultWayPoints, 0f);

        //三角形
        int[] _triangles = new int[(_resultWayPoints.Count - 1) * 6];
        for (int v = 0, ti = 0; v < _resultWayPoints.Count - 1; v++, ti += 6) {
            _triangles[ti] = 2 * v;
            _triangles[ti + 4] = _triangles[ti + 1] = 2 * v + 1;
            _triangles[ti + 2] = _triangles[ti + 3] = 2 * v + 2;
            _triangles[ti + 5] = 2 * v + 3;
        }

        _riverMesh.triangles = _triangles;
        _riverFilter.sharedMesh = _riverMesh;

        //网格碰撞体
        _meshCollider = RiverObject.AddComponent<MeshCollider> ();
        _meshCollider.isTrigger = true; // 设置为触发器，触发浮力系统

        //添加材质
        _riverRenderer.sharedMaterial = riverMat;

        //地形下陷处理
        if (modifyRealTerrain && targetTerrain != null)
        {
            // 修改真实 Terrain
            if (autoBackupTerrain && !m_hasBackup)
            {
                BackupTerrainHeights();  // 首次自动备份
            }
            ModifyRealTerrainHeightmap();  // 修改真实地形
        }
        else
        {
            // 保留原来的逻辑：只修改参考网格
            TerrainSink (m_mesh);
        }

        //加大河流宽度，使之与地面贴合
        RiverVertexCaculate (_riverMesh, ref _resultWayPoints, riverExpandWidth);

        //重新计算
        _riverMesh.RecalculateNormals ();
        _riverMesh.RecalculateBounds ();
        _riverMesh.RecalculateTangents ();

        // 注意：河流网格始终显示，不受showRiverMeshInRuntime选项影响

    }

    void RiverVertexCaculate (Mesh _riverMesh, ref List<Vector3> _resultWayPoints, float riverWidthExpand) {

        List<RiverSegInfo> m_riverSegmentInfos = new List<RiverSegInfo> ();

        //是河流随机有大有小
        Vector3 _h = Vector3.zero;
        Vector3[] _vertexs = new Vector3[_resultWayPoints.Count * 2];
        Vector2[] _uvs = new Vector2[_resultWayPoints.Count * 2];
        float _riverLength = PathHelper.PathLength (_resultWayPoints.ToArray ());
        float _uvWrap = m_lastMaxRiverUVY = _riverLength / repeatLength;

        //保存每条河流的必要数据
        if (m_riverInstance != null) {
            m_riverInstance.riverUVWrapAmount = _uvWrap;
            m_riverInstance.riverLength = _riverLength;
            m_riverInstance.segmentAmount = _resultWayPoints.Count - 1;
            m_riverInstance.riverDepth = riverDepth;
        }

        for (int i = 0; i < _resultWayPoints.Count; i++) {
            Vector3 _vetexOffset = Vector3.zero;
            //河流流向
            if (i < _resultWayPoints.Count - 1) {
                _vetexOffset = _resultWayPoints[i + 1] - _resultWayPoints[i];
            }
            //河流水平方向
            _h = Vector3.Cross (_vetexOffset, Vector3.up).normalized;
            //河流下沉量
            Vector3 _riverSinkAmount = riverDepth * riverSickRadio * Vector3.down;
            //河流宽度
            Vector3 _wayPoint = _resultWayPoints[i];
            float _halfRiverWidth = (maxRiverWidth *
                (1 + riverRandomWidthWholeLengthScale *
                    (Mathf.PerlinNoise (_wayPoint.x * riverRandomRadio.x, _wayPoint.z * riverRandomRadio.y))) +
                riverWidthExpand) * 0.5f;
            float _lengthPercents = (float) i / _resultWayPoints.Count;
            _halfRiverWidth *= riverWidthWholeLengthCurve.Evaluate (_lengthPercents);
            //计算曲线两边的顶点位置
            _vertexs[2 * i] = RiverObject.transform.InverseTransformPoint (_resultWayPoints[i] - _h * _halfRiverWidth + _riverSinkAmount);
            _vertexs[2 * i + 1] = RiverObject.transform.InverseTransformPoint (_resultWayPoints[i] + _h * _halfRiverWidth + _riverSinkAmount);
            //记录河流段信息
            m_riverSegmentInfos.Add (
                new RiverSegInfo () {
                    LengthRadio = (float) i / _resultWayPoints.Count,
                    halfWidth = _halfRiverWidth,
                    flowDir = _vetexOffset.normalized,
                    center = _resultWayPoints[i]
                }
            );
            //v
            _uvs[2 * i].y = _uvs[2 * i + 1].y = (float) i / (_resultWayPoints.Count - 1) * _uvWrap;
            //u
            _uvs[2 * i].x = 0;
            _uvs[2 * i + 1].x = 1;
        }

        _riverMesh.vertices = _vertexs;
        _riverMesh.uv = _uvs;

        //保存每条河流的必要数据
        if (m_riverInstance != null) {
            m_riverInstance.riverSegmentInfos = m_riverSegmentInfos;
        }
    }

    //地形下陷
    void TerrainSink (Mesh mesh) {
        Vector3[] _vertexs = mesh.vertices;
        for (int i = 0; i < _vertexs.Length; i++) {
            //网格下沉riverDepth(这里实际是上升）->射线检测，碰撞到河，则保持网格的下沉，否则取原来网格
            //的高度
            Vector3 _worldPos = transform.TransformPoint (_vertexs[i]);
            Vector3 _rayOriginPos = _worldPos + Vector3.up * riverDepth;
            Ray _ray = new Ray (_rayOriginPos, Vector3.down);
            RaycastHit hit;
            if (Physics.Raycast (_ray, out hit, 100)) {
                //地形按照曲线平滑下陷
                float _uvxDis = Mathf.Abs (hit.textureCoord.x - 0.5f) * 2;
                // float _rr = hit.textureCoord.y / m_lastMaxRiverUVY;
                // float _halfWidth = GetRiverWidth(_rr);
                float _riverBedBlend = riverBedCurve.Evaluate (_uvxDis);
                _vertexs[i] = transform.InverseTransformPoint (_worldPos - Vector3.up * riverDepth * _riverBedBlend);
            }
        }

        mesh.vertices = _vertexs;

    }

    //获取河流对应比例位置的河流宽度
    private float GetRiverWidth (float p) {
        if (m_riverInstance != null && m_riverInstance.riverSegmentInfos != null && m_riverInstance.riverSegmentInfos.Count > 0) {
            for (int i = 0; i < m_riverInstance.riverSegmentInfos.Count; i++) {
                if (Mathf.Abs (m_riverInstance.riverSegmentInfos[i].LengthRadio - p) <= 0.1f) {
                    return m_riverInstance.riverSegmentInfos[i].halfWidth;
                }
            }
        }
        return 1;
    }

    /// <summary>
    /// 备份地形的原始高度图
    /// </summary>
    public void BackupTerrainHeights()  // 改为 public，方便手动调用
    {
        Debug.Log("=== 开始备份地形 ===");
        
        if (targetTerrain == null)
        {
            Debug.LogWarning("River: 无法备份地形，targetTerrain 为空");
            return;
        }

        TerrainData terrainData = targetTerrain.terrainData;
        if (terrainData == null)
        {
            Debug.LogError("River: Terrain 缺少 TerrainData");
            return;
        }

        int resolution = terrainData.heightmapResolution;
        
        Debug.Log($"正在读取高度图：分辨率 {resolution}x{resolution}");
        
        // 从 TerrainData 获取整个高度图（0,0 起点，完整分辨率）
        m_originalTerrainHeights = terrainData.GetHeights(0, 0, resolution, resolution);
        
        if (m_originalTerrainHeights == null)
        {
            Debug.LogError("备份失败：GetHeights 返回 null");
            m_hasBackup = false;
            return;
        }
        
        m_hasBackup = true;
        
        Debug.Log($"✓ 地形高度图已备份 (分辨率: {resolution}x{resolution})");
        Debug.Log($"✓ 备份数据大小: {m_originalTerrainHeights.Length} 个点");
        Debug.Log($"✓ m_hasBackup = {m_hasBackup}");
        Debug.Log("=== 备份完成 ===");
    }

    /// <summary>
    /// 恢复地形到备份状态
    /// </summary>
    public void RestoreTerrainFromBackup()
    {
        Debug.Log("=== 开始恢复地形 ===");
        Debug.Log($"m_hasBackup = {m_hasBackup}");
        Debug.Log($"m_originalTerrainHeights == null ? {m_originalTerrainHeights == null}");
        
        if (!m_hasBackup || m_originalTerrainHeights == null)
        {
            Debug.LogWarning("River: 没有可用的地形备份数据");
            Debug.LogWarning("请确保在修改地形前已经备份（勾选 Auto Backup Terrain）");
            return;
        }

        if (targetTerrain == null)
        {
            Debug.LogError("River: targetTerrain 为空，无法恢复");
            return;
        }

        Debug.Log($"targetTerrain: {targetTerrain.name}");
        Debug.Log($"备份数据尺寸: {m_originalTerrainHeights.GetLength(0)}x{m_originalTerrainHeights.GetLength(1)}");

        TerrainData terrainData = targetTerrain.terrainData;
        int resolution = terrainData.heightmapResolution;
        
        Debug.Log($"当前地形分辨率: {resolution}x{resolution}");
        
        // 检查分辨率是否匹配
        if (m_originalTerrainHeights.GetLength(0) != resolution || 
            m_originalTerrainHeights.GetLength(1) != resolution)
        {
            Debug.LogError($"备份数据分辨率 ({m_originalTerrainHeights.GetLength(0)}x{m_originalTerrainHeights.GetLength(1)}) " +
                          $"与当前地形分辨率 ({resolution}x{resolution}) 不匹配！");
            return;
        }
        
        // 恢复高度图
        terrainData.SetHeights(0, 0, m_originalTerrainHeights);
        
        // 强制刷新地形
        targetTerrain.Flush();
        
        // Unity 编辑器中需要标记为脏数据
        #if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(targetTerrain);
        UnityEditor.EditorUtility.SetDirty(terrainData);
        #endif
        
        Debug.Log("✓ 地形已恢复到备份状态");
        Debug.Log("=== 恢复完成 ===");
    }

    /// <summary>
    /// 平滑高度图（仅在河流范围内，使用可变半径高斯模糊）
    /// </summary>
    private float[,] SmoothHeightmapInRiver(float[,] heights, bool[,] isInRiver, int minX, int maxX, int minZ, int maxZ, int iterations)
    {
        int resolution = heights.GetLength(0);
        float[,] smoothed = (float[,])heights.Clone();
        
        // 使用用户设置的平滑半径
        int radius = smoothRadius;
        
        for (int iter = 0; iter < iterations; iter++)
        {
            float[,] temp = (float[,])smoothed.Clone();
            
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    // 只平滑河流范围内的点
                    if (!isInRiver[x, z])
                        continue;
                    
                    // 使用可变半径的高斯核平滑
                    float sum = 0f;
                    float weightSum = 0f;
                    
                    // 遍历半径范围内的所有点
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        for (int dz = -radius; dz <= radius; dz++)
                        {
                            int nx = x + dx;
                            int nz = z + dz;
                            
                            if (nx >= 0 && nx < resolution && nz >= 0 && nz < resolution)
                            {
                                // 只从河流范围内的邻居采样
                                if (isInRiver[nx, nz])
                                {
                                    // 计算距离
                                    float distance = Mathf.Sqrt(dx * dx + dz * dz);
                                    
                                    // 高斯权重：距离越远权重越小
                                    // 使用高斯函数：weight = exp(-(distance^2) / (2 * sigma^2))
                                    float sigma = radius / 2.0f;  // 标准差
                                    float weight = Mathf.Exp(-(distance * distance) / (2f * sigma * sigma));
                                    
                                    sum += temp[nx, nz] * weight;
                                    weightSum += weight;
                                }
                            }
                        }
                    }
                    
                    // 只有当有足够的邻居时才平滑
                    if (weightSum > 0)
                    {
                        smoothed[x, z] = sum / weightSum;
                    }
                }
            }
        }
        
        return smoothed;
    }

    /// <summary>
    /// 修改真实 Unity Terrain 的高度图，使地形沿河流下陷
    /// </summary>
    private void ModifyRealTerrainHeightmap()
    {
        // ===== 步骤1：验证必要条件 =====
        if (targetTerrain == null)
        {
            Debug.LogError("River: 未指定 targetTerrain！请在 Inspector 中拖拽 Terrain 对象到该字段。");
            return;
        }

        if (RiverObject == null)
        {
            Debug.LogError("River: RiverObject 不存在，请先生成河流网格");
            return;
        }

        TerrainData terrainData = targetTerrain.terrainData;
        if (terrainData == null)
        {
            Debug.LogError("River: Terrain 缺少 TerrainData");
            return;
        }

        // ===== 步骤2：获取地形参数 =====
        int resolution = terrainData.heightmapResolution;  // 如：513
        Vector3 terrainPos = targetTerrain.transform.position;  // 地形世界坐标起点
        Vector3 terrainSize = terrainData.size;  // 地形尺寸（如：1000, 600, 1000）
        
        Debug.Log($"开始修改地形：分辨率={resolution}x{resolution}, 尺寸={terrainSize}");

        // ===== 步骤3：获取当前高度图 =====
        // 这里从 TerrainData 获取整个高度图数组
        // heights[x, z] 是一个二维数组，每个值范围 0-1
        float[,] heights = terrainData.GetHeights(0, 0, resolution, resolution);

        // ===== 步骤4：获取河流碰撞体（用于射线检测）=====
        MeshCollider riverCollider = RiverObject.GetComponent<MeshCollider>();
        if (riverCollider == null)
        {
            Debug.LogError("River: RiverObject 缺少 MeshCollider，无法进行射线检测");
            return;
        }

        // 确保碰撞体启用（临时启用用于检测）
        bool wasRiverEnabled = riverCollider.enabled;
        riverCollider.enabled = true;

        // ===== 临时禁用地形参考网格的碰撞体，避免射线被它拦截 =====
        MeshCollider terrainMeshCollider = GetComponent<MeshCollider>();
        bool wasTerrainMeshEnabled = false;
        if (terrainMeshCollider != null)
        {
            wasTerrainMeshEnabled = terrainMeshCollider.enabled;
            terrainMeshCollider.enabled = false;
            Debug.Log("已临时禁用地形参考网格碰撞体");
        }

        // ===== 临时禁用真实地形的碰撞体（如果有）=====
        TerrainCollider realTerrainCollider = targetTerrain.GetComponent<TerrainCollider>();
        bool wasRealTerrainEnabled = false;
        if (realTerrainCollider != null)
        {
            wasRealTerrainEnabled = realTerrainCollider.enabled;
            realTerrainCollider.enabled = false;
            Debug.Log("已临时禁用真实地形碰撞体");
        }

        // ===== 步骤5：计算河流影响范围（优化性能）=====
        // 获取河流的包围盒（世界坐标）
        Bounds riverBounds = RiverObject.GetComponent<MeshRenderer>().bounds;
        
        // 扩展边界，确保覆盖完整（包括河床过渡区域）
        riverBounds.Expand(maxRiverWidth * 2 + riverDepth);

        // 将世界坐标的包围盒转换为高度图的索引范围
        // 注意：这里 X 和 Z 是交换的！
        // riverBounds.min.z（世界Z） → minX（高度图第一个索引）
        // riverBounds.min.x（世界X） → minZ（高度图第二个索引）
        
        // 计算最小X索引：世界Z坐标 → 高度图X索引
        // (世界Z - 地形起点Z) / 地形Z长度 = 归一化位置（0-1）
        // 归一化位置 * (分辨率-1) = 索引位置
        int minX = Mathf.Max(0, 
            Mathf.FloorToInt((riverBounds.min.z - terrainPos.z) / terrainSize.z * (resolution - 1)));
        int maxX = Mathf.Min(resolution - 1, 
            Mathf.CeilToInt((riverBounds.max.z - terrainPos.z) / terrainSize.z * (resolution - 1)));
        
        // 计算最小Z索引：世界X坐标 → 高度图Z索引
        int minZ = Mathf.Max(0, 
            Mathf.FloorToInt((riverBounds.min.x - terrainPos.x) / terrainSize.x * (resolution - 1)));
        int maxZ = Mathf.Min(resolution - 1, 
            Mathf.CeilToInt((riverBounds.max.x - terrainPos.x) / terrainSize.x * (resolution - 1)));

        Debug.Log($"优化范围：高度图索引 X[{minX}-{maxX}], Z[{minZ}-{maxZ}] (总共 {(maxX-minX+1)*(maxZ-minZ+1)} 个点)");
        Debug.Log($"河流参数：riverDepth={riverDepth}, maxRiverWidth={maxRiverWidth}");
        Debug.Log($"河流包围盒：{riverBounds}");
        
        // 诊断信息：计算地形点间距
        float terrainPointSpacingX = terrainSize.x / (resolution - 1);
        float terrainPointSpacingZ = terrainSize.z / (resolution - 1);
        Debug.Log($"⚠️ 诊断信息：");
        Debug.Log($"  地形点间距: X={terrainPointSpacingX:F2}米, Z={terrainPointSpacingZ:F2}米");
        Debug.Log($"  河流宽度: {maxRiverWidth}米");
        Debug.Log($"  河流宽度覆盖点数: 约 {maxRiverWidth / terrainPointSpacingX:F1} 个点");
        if (maxRiverWidth / terrainPointSpacingX < 5)
        {
            Debug.LogWarning($"⚠️ 警告：河流太窄！河流宽度只覆盖 {maxRiverWidth / terrainPointSpacingX:F1} 个地形点");
            Debug.LogWarning($"  建议：将 maxRiverWidth 增加到至少 {terrainPointSpacingX * 8:F0} 米");
        }

        // ===== 步骤6：遍历高度图并修改 =====
        int modifiedCount = 0;
        int rayHitCount = 0;  // 射线命中次数（包括非河流对象）
        int totalRayCount = 0;  // 总射线数
        
        // 创建标记数组，记录哪些点在河流范围内
        bool[,] isInRiver = new bool[resolution, resolution];
        
        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                totalRayCount++;
                
                // 6.1 将高度图索引转换为世界坐标
                // 注意：heights[x,z] 对应世界坐标是交换的！
                // x → 世界Z, z → 世界X
                
                float normalizedX = x / (float)(resolution - 1);  // 0-1
                float normalizedZ = z / (float)(resolution - 1);  // 0-1
                
                // 世界X = 地形起点X + normalizedZ * 地形X长度（用Z索引）
                float worldX = terrainPos.x + normalizedZ * terrainSize.x;
                // 世界Z = 地形起点Z + normalizedX * 地形Z长度（用X索引）
                float worldZ = terrainPos.z + normalizedX * terrainSize.z;
                
                float currentHeightNormalized = heights[x, z];  // 0-1 范围
                float currentHeightWorld = currentHeightNormalized * terrainSize.y;  // 实际高度（米）
                float worldY = terrainPos.y + currentHeightWorld;

                // 6.2 从该点上方发射射线，检测是否命中河流
                // 增加射线起点高度，确保在河流上方
                float rayStartHeight = Mathf.Max(worldY + riverDepth * 2f, riverBounds.max.y + 100f);
                Vector3 rayOrigin = new Vector3(worldX, rayStartHeight, worldZ);
                Ray ray = new Ray(rayOrigin, Vector3.down);
                RaycastHit hit;

                // 射线长度：足够长以覆盖整个高度范围
                float rayDistance = rayStartHeight - terrainPos.y + 100f;
                if (Physics.Raycast(ray, out hit, rayDistance))
                {
                    rayHitCount++;
                    
                    // 6.3 检查是否命中河流对象
                    if (hit.collider == riverCollider)
                    {
                        // 标记这个点在河流范围内
                        isInRiver[x, z] = true;
                        
                        // 6.4 计算下沉量（使用 UV 坐标和曲线）
                        // hit.textureCoord.x: 0（河流左边）到 1（河流右边）
                        // 中心是 0.5，距离中心越远下沉越少
                        float distanceFromCenter = Mathf.Abs(hit.textureCoord.x - 0.5f) * 2f;  // 0-1
                        
                        // 使用曲线控制下沉过渡（中心下沉最多，边缘平滑过渡）
                        float sinkBlend = riverBedCurve.Evaluate(distanceFromCenter);
                        
                        // 实际下沉量（世界单位，米）
                        float sinkAmount = riverDepth * sinkBlend;

                        // 6.5 计算新的世界高度
                        float newWorldY = worldY - sinkAmount;
                        
                        // 6.6 转换回高度图的归一化值（0-1）
                        float newHeightNormalized = (newWorldY - terrainPos.y) / terrainSize.y;
                        
                        // 6.7 限制范围，防止超出边界
                        newHeightNormalized = Mathf.Clamp01(newHeightNormalized);

                        // 6.8 写入高度图数组
                        heights[x, z] = newHeightNormalized;
                        
                        modifiedCount++;
                    }
                }
            }
        }

        // ===== 步骤7：平滑处理（可选）=====
        if (smoothRiverBed && modifiedCount > 0)
        {
            Debug.Log($"开始河床平滑处理（半径={smoothRadius}, 迭代 {smoothIterations} 次）...");
            heights = SmoothHeightmapInRiver(heights, isInRiver, minX, maxX, minZ, maxZ, smoothIterations);
            Debug.Log("✓ 河床平滑完成");
        }

        // ===== 步骤8：一次性应用所有修改 =====
        // 将修改后的高度图写回 TerrainData
        terrainData.SetHeights(0, 0, heights);

        // ===== 步骤9：刷新地形渲染 =====
        targetTerrain.Flush();

        // ===== 步骤10：恢复所有碰撞体状态 =====
        riverCollider.enabled = wasRiverEnabled;
        
        if (terrainMeshCollider != null)
        {
            terrainMeshCollider.enabled = wasTerrainMeshEnabled;
            Debug.Log("已恢复地形参考网格碰撞体状态");
        }
        
        if (realTerrainCollider != null)
        {
            realTerrainCollider.enabled = wasRealTerrainEnabled;
            Debug.Log("已恢复真实地形碰撞体状态");
        }

        Debug.Log($"✓ 地形修改完成！");
        Debug.Log($"  - 总检测点数: {totalRayCount}");
        Debug.Log($"  - 射线命中数: {rayHitCount} ({(float)rayHitCount/totalRayCount*100:F1}%)");
        Debug.Log($"  - 命中河流数: {modifiedCount} ({(float)modifiedCount/totalRayCount*100:F1}%)");
        
        if (modifiedCount == 0)
        {
            Debug.LogWarning("⚠️ 没有修改任何高度点！可能的原因：");
            Debug.LogWarning("  1. 河流网格太小，地形分辨率太高，两个高度点之间没有河流");
            Debug.LogWarning("  2. 河流位置与地形不匹配");
            Debug.LogWarning("  3. riverDepth 或 maxRiverWidth 参数太小");
            Debug.LogWarning($"  建议：增大 maxRiverWidth (当前={maxRiverWidth}) 或降低地形分辨率");
        }
    }

    private void OnDrawGizmos () {
        if (showGizmos && wayPoints.Count > 0) {
            foreach (var item in wayPoints)
                Gizmos.DrawSphere (item.pos, 0.5f);

            if (wayPoints.Count > 1) {
                Vector3[] _wayPoints = wayPoints.Select (x => x.pos).ToArray ();
                PathHelper.DrawPathHelper (_wayPoints, Color.white);
            }
        }
    }

}