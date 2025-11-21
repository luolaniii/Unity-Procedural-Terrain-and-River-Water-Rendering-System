using System.Collections.Generic;
using UnityEngine;

[RequireComponent (typeof (Collider))]
[RequireComponent (typeof (Rigidbody))]
[RequireComponent (typeof (MeshFilter))]
public class MyFloatObject : MonoBehaviour {

    public float centerDragForceRadio = 0.005f;
    //物体撞击到其它物体的地面因子
    public float boundRadio = 0.1f;
    //物体撞击到地面时产生的最大扭矩
    public float torqueRadio = 2f;
    [SerializeField]
    private bool calculateDensity = false;

    [SerializeField]
    private float density = 0.75f;

    [SerializeField]
    [Range (0f, 1f)]
    //体元尺寸
    private float normalizedVoxelSize = 0.5f;

    [SerializeField]
    private float dragInWater = 1f;

    [SerializeField]
    private float angularDragInWater = 1f;

    private MyWaterVolume water;
    private RiverInstance river;
    private new Collider collider;
    private new Rigidbody rigidbody;
    private float initialDrag;
    private float initialAngularDrag;
    private Vector3 voxelSize;
    private Vector3[] voxels;
    private Vector3 currentVoxelWaterForce;

    protected virtual void Awake () {
        this.collider = this.GetComponent<Collider> ();
        this.rigidbody = this.GetComponent<Rigidbody> ();

        this.initialDrag = this.rigidbody.drag;
        this.initialAngularDrag = this.rigidbody.angularDrag;

        if (this.calculateDensity) {
            float objectVolume = MathfUtils.CalculateVolume_Mesh (this.GetComponent<MeshFilter> ().mesh, this.transform);
            this.density = this.rigidbody.mass / objectVolume;
        }
    }

    private void Update() {
        if(transform.position.y < -100){
            Destroy(gameObject);
        }
    }

    protected virtual void FixedUpdate () {
        if (this.water != null && this.voxels.Length > 0) {
            //将总共的浮力分到每个体元
            Vector3 forceAtSingleVoxel = this.CalculateMaxBuoyancyForce () / this.voxels.Length;
            Bounds bounds = this.collider.bounds;
            //每个体元的高度
            float voxelHeight = bounds.size.y * this.normalizedVoxelSize;

            float submergedVolume = 0f;
            RiverSegInfo segmentInfo = null;
            for (int i = 0; i < this.voxels.Length; i++) {
                Vector3 worldPoint = this.transform.TransformPoint (this.voxels[i]);

            //获取水深度
            RaycastHit hit = this.GetHitInfoOnWater (worldPoint);
            float waterLevel = hit.point.y;
            //体元的深度（体元底部到水面）
            float deepLevel = waterLevel - worldPoint.y + (voxelHeight / 2f);
            // 0 - 完全出了水面  1 - 完全沉入水中                 
            float submergedFactor = Mathf.Clamp (deepLevel / voxelHeight, 0f, 1f);
            submergedVolume += submergedFactor;
            //水面的法线
            Vector3 surfaceNormal = hit.normal;
            
            //水对物体的推力计算
            float ndotUp = Mathf.Clamp01 (Vector3.Dot (surfaceNormal, Vector3.up));
            segmentInfo = river.GetFlowSegmentInfo (hit.textureCoord.y);
            if (segmentInfo != null) {
                Vector3 flowDirection = segmentInfo.flowDir;
                //这里1.2f - ndotUp是确保当物体处于完全水平的水面时也能向水流方向运动
                currentVoxelWaterForce = (1.2f - ndotUp) * flowDirection * water.baseWaterPushForceRadio;
                Quaternion surfaceRotation = Quaternion.FromToRotation (this.water.transform.up, (surfaceNormal + flowDirection).normalized);
                //沉入水中越少，朝水面法线偏移越厉害，抖动的越厉害
                surfaceRotation = Quaternion.Slerp (surfaceRotation, Quaternion.identity, submergedFactor);

                Vector3 finalVoxelForce = surfaceRotation * ((forceAtSingleVoxel + currentVoxelWaterForce) * submergedFactor);
                //添加力
                this.rigidbody.AddForceAtPosition (finalVoxelForce, worldPoint);

                Debug.DrawLine (worldPoint, worldPoint + finalVoxelForce.normalized, Color.blue);
                Debug.DrawLine (worldPoint, worldPoint + flowDirection);
            }
            }

            submergedVolume /= this.voxels.Length; // 0 - object is fully out of the water, 1 - object is fully submerged

            this.rigidbody.drag = Mathf.Lerp (this.initialDrag, this.dragInWater, submergedVolume);
            this.rigidbody.angularDrag = Mathf.Lerp (this.initialAngularDrag, this.angularDragInWater, submergedVolume);

            RaycastHit hitx = this.GetHitInfoOnWater (transform.position);
            RiverSegInfo segmentInfox = river.GetFlowSegmentInfo (hitx.textureCoord.y);
            //防止物体冲出河道
            if(segmentInfox != null){
                Vector3 offset = transform.position - (segmentInfox.center + river.riverDepth * Vector3.down);
                this.rigidbody.AddForce(-offset *  centerDragForceRadio,ForceMode.Impulse);
                Debug.DrawLine(transform.position,(segmentInfox.center + river.riverDepth * Vector3.down));
            }

        }
    }

    protected virtual void OnTriggerEnter (Collider other) {
        if (other.CompareTag (MyWaterVolume.TAG)) {
            this.water = other.GetComponent<MyWaterVolume> ();
            this.river = other.GetComponent<RiverInstance> ();
            if (this.voxels == null) {
                this.voxels = this.CutIntoVoxels ();
            }
        } else if (other.name == "Ground") {
            this.rigidbody.AddTorque (
                new Vector3 (
                    Random.Range (-torqueRadio, torqueRadio),
                    Random.Range (-torqueRadio, torqueRadio),
                    Random.Range (-torqueRadio, torqueRadio)),
                ForceMode.Impulse
            );
            this.rigidbody.velocity = (currentVoxelWaterForce - rigidbody.velocity) * boundRadio;
        }
    }

    protected virtual void OnTriggerExit (Collider other) {
        if (other.CompareTag (MyWaterVolume.TAG)) {
            // this.water = null;
        }
    }

    //绘制出体元
    protected virtual void OnDrawGizmos () {
        if (this.voxels != null) {
            for (int i = 0; i < this.voxels.Length; i++) {
                Gizmos.color = Color.magenta - new Color (0f, 0f, 0f, 0.75f);
                Gizmos.DrawCube (this.transform.TransformPoint (this.voxels[i]), this.voxelSize * 0.8f);
            }
        }
    }

    //计算最大的浮力
    private Vector3 CalculateMaxBuoyancyForce () {
        //物体体积
        float objectVolume = this.rigidbody.mass / this.density;
        //浮力 = 水的密度*排出水的体积（物体体积）*-1 *重力
        Vector3 maxBuoyancyForce = this.water.Density * objectVolume * -Physics.gravity;

        return maxBuoyancyForce;
    }

    //切割体元
    private Vector3[] CutIntoVoxels () {
        Quaternion initialRotation = this.transform.rotation;
        this.transform.rotation = Quaternion.identity;

        Bounds bounds = this.collider.bounds;
        this.voxelSize.x = bounds.size.x * this.normalizedVoxelSize;
        this.voxelSize.y = bounds.size.y * this.normalizedVoxelSize;
        this.voxelSize.z = bounds.size.z * this.normalizedVoxelSize;
        int voxelsCountForEachAxis = Mathf.RoundToInt (1f / this.normalizedVoxelSize);
        List<Vector3> voxels = new List<Vector3> (voxelsCountForEachAxis * voxelsCountForEachAxis * voxelsCountForEachAxis);

        for (int i = 0; i < voxelsCountForEachAxis; i++) {
            for (int j = 0; j < voxelsCountForEachAxis; j++) {
                for (int k = 0; k < voxelsCountForEachAxis; k++) {
                    float pX = bounds.min.x + this.voxelSize.x * (0.5f + i);
                    float pY = bounds.min.y + this.voxelSize.y * (0.5f + j);
                    float pZ = bounds.min.z + this.voxelSize.z * (0.5f + k);

                    Vector3 point = new Vector3 (pX, pY, pZ);
                    if (ColliderUtils.IsPointInsideCollider (point, this.collider, ref bounds)) {
                        voxels.Add (this.transform.InverseTransformPoint (point));
                    }
                }
            }
        }

        this.transform.rotation = initialRotation;

        return voxels.ToArray ();
    }

    //从水面上方垂直向下发射射线，得到和水面的交点
    public RaycastHit GetHitInfoOnWater (Vector3 worldPoint) {
        Vector3 _originUP = worldPoint + Vector3.up * 1000;
        Ray _rayDown = new Ray (_originUP, Vector3.down);
        RaycastHit hit;
        if (Physics.Raycast (_rayDown, out hit, Mathf.Infinity,1<<LayerMask.NameToLayer("Water"))) {
            return hit;
        }

        return hit;
    }
}
