using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor (typeof (River))]
public class RiverDraw : Editor {

    River m_river;
    bool m_enableDraw;

    void OnEnable () {
        m_river = (River) target;
    }

    public override void OnInspectorGUI () {

        if (GUILayout.Button ("创建网格")) {
            m_river.CreateMeshPrepare();
            m_river.CreateMesh ();
        }
        if (GUILayout.Button ("开始绘制")) {
            m_river.BeginDrawPath ();
            m_enableDraw = true;
        }
        if (GUILayout.Button ("结束绘制")) {
            m_river.EndDrawPath ();
            m_enableDraw = false;
        }

        GUILayout.Space(10);

        if (GUILayout.Button ("创建河流网格")) {
            m_river.CreateRiverMesh();
        }

        GUILayout.Space(10);
        EditorGUILayout.LabelField("地形管理", EditorStyles.boldLabel);

        if (GUILayout.Button ("手动备份地形")) {
            m_river.BackupTerrainHeights();
        }

        if (GUILayout.Button ("恢复地形到备份状态")) {
            if (EditorUtility.DisplayDialog(
                "确认恢复", 
                "确定要恢复地形到备份状态吗？当前的修改将丢失。", 
                "确定", 
                "取消"))
            {
                m_river.RestoreTerrainFromBackup();
            }
        }

        base.OnInspectorGUI ();

    }
    public void OnSceneGUI () {

        Event e = Event.current;
        if (m_enableDraw) {
            if (e.type == EventType.MouseDown && e.button == 0 && e.alt) {
                Ray ray = HandleUtility.GUIPointToWorldRay (Event.current.mousePosition);
                RaycastHit hit;
                if (Physics.Raycast (ray, out hit, Mathf.Infinity)) {
                    e.Use ();

                    if (!m_river.IsOnDraw) {
                        m_river.RecordCurrentPoint (hit.point);
                        m_river.IsOnDraw = true;
                    } else {
                        m_river.RecordCurrentPoint (hit.point);
                    }

                }
            } else {

            }
        }

    }

}