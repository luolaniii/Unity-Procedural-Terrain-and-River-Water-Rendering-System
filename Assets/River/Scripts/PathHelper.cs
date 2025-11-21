using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathHelper {

    //根据提供的路径获取平滑路径
    public static void GetWayPoints (Vector3[] points, int amountRadio, ref List<Vector3> wayPoints) {
        if (points == null || points.Length <= 1) { Debug.Log ("points is empty!"); return; }

        wayPoints.Clear ();

        Vector3[] vector3s = PathControlPointGenerator (points);

        Vector3 prevPt = Interp (vector3s, 0);
        int SmoothAmount = (points.Length - 1) * amountRadio;
        for (int i = 1; i <= SmoothAmount; i++) {
            float pm = (float) i / SmoothAmount;
            Vector3 currPt = Interp (vector3s, pm);
            wayPoints.Add (currPt);
            prevPt = currPt;
        }
    }

    //Gizmos平滑的绘制提供的路径
    public static void DrawPathHelper (Vector3[] path, Color color) {
        Vector3[] vector3s = PathControlPointGenerator (path);

        Vector3 prevPt = Interp (vector3s, 0);
        Gizmos.color = color;
        int SmoothAmount = path.Length * 20;
        for (int i = 1; i <= SmoothAmount; i++) {
            float pm = (float) i / SmoothAmount;
            Vector3 currPt = Interp (vector3s, pm);
            Gizmos.DrawLine (currPt, prevPt);
            prevPt = currPt;
        }
    }

    //计算路径的长度
    public static float PathLength (Vector3[] path) {
        float pathLength = 0;

        Vector3[] vector3s = PathControlPointGenerator (path);

        Vector3 prevPt = Interp (vector3s, 0);
        int SmoothAmount = path.Length * 20;
        for (int i = 1; i <= SmoothAmount; i++) {
            float pm = (float) i / SmoothAmount;
            Vector3 currPt = Interp (vector3s, pm);
            pathLength += Vector3.Distance (prevPt, currPt);
            prevPt = currPt;
        }

        return pathLength;
    }

    //生成曲线控制点,path.length>=2（为路径添加首尾点，便于绘制Cutmull-Rom曲线）
    private static Vector3[] PathControlPointGenerator (Vector3[] path) {
        Vector3[] suppliedPath;
        Vector3[] vector3s;

        suppliedPath = path;

        int offset = 2;
        vector3s = new Vector3[suppliedPath.Length + offset];
        Array.Copy (suppliedPath, 0, vector3s, 1, suppliedPath.Length);

        //计算第一个控制点和最后一个控制点位置
        vector3s[0] = vector3s[1] + (vector3s[1] - vector3s[2]);
        vector3s[vector3s.Length - 1] = vector3s[vector3s.Length - 2] + (vector3s[vector3s.Length - 2] - vector3s[vector3s.Length - 3]);

        //首位点重合时，形成闭合的Catmull-Rom曲线
        if (vector3s[1] == vector3s[vector3s.Length - 2]) {
            Vector3[] tmpLoopSpline = new Vector3[vector3s.Length];
            Array.Copy (vector3s, tmpLoopSpline, vector3s.Length);
            tmpLoopSpline[0] = tmpLoopSpline[tmpLoopSpline.Length - 3];
            tmpLoopSpline[tmpLoopSpline.Length - 1] = tmpLoopSpline[2];
            vector3s = new Vector3[tmpLoopSpline.Length];
            Array.Copy (tmpLoopSpline, vector3s, tmpLoopSpline.Length);
        }

        return (vector3s);
    }

    //Catmull-Rom曲线 参考：https://blog.csdn.net/u012154588/article/details/98977717
    private static Vector3 Interp (Vector3[] pts, float t) {
        int numSections = pts.Length - 3;
        int currPt = Mathf.Min (Mathf.FloorToInt (t * (float) numSections), numSections - 1);
        float u = t * (float) numSections - (float) currPt;

        Vector3 a = pts[currPt];
        Vector3 b = pts[currPt + 1];
        Vector3 c = pts[currPt + 2];
        Vector3 d = pts[currPt + 3];

        return .5f * (
            (-a + 3f * b - 3f * c + d) * (u * u * u) +
            (2f * a - 5f * b + 4f * c - d) * (u * u) +
            (-a + c) * u +
            2f * b
        );
    }

}