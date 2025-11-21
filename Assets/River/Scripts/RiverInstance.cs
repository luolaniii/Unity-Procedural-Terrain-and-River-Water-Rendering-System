using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RiverSegInfo {
    public float LengthRadio;
    public float halfWidth;
    public Vector3 flowDir;
    public Vector3 center;
}

public class RiverInstance : MonoBehaviour {

    //河流深度
    public float riverDepth;
    //河流长度
    public float riverLength;

    //河流UV.y Repeat次数
    public float riverUVWrapAmount;
    public int segmentAmount;

    public List<RiverSegInfo> riverSegmentInfos;

    public RiverSegInfo GetFlowSegmentInfo (float UVY) {
        if (UVY > 0 && riverSegmentInfos.Count > 0) {
            float _lengthUVYRadio = UVY / riverUVWrapAmount;
            float _lengthRadioPerSegment = 1f / segmentAmount;
            foreach (var segInfo in riverSegmentInfos) {
                if (Mathf.Abs (segInfo.LengthRadio - _lengthUVYRadio) < _lengthRadioPerSegment * 2) {
                    // Debug.Log(segInfo.LengthRadio + " " + _lengthUVYRadio + " " + segInfo.center);
                    return segInfo;
                }
            }
        }
        return null;
    }

}