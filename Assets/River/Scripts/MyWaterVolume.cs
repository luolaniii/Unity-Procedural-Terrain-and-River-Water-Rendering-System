using System.Collections.Generic;
using UnityEngine;

public class MyWaterVolume : MonoBehaviour {
    public const string TAG = "River";

    //最大水流推力因子
    public float baseWaterPushForceRadio = 1;

    [SerializeField]
    private float density = 1f;

    public float Density {
        get {
            return this.density;
        }
    }

}