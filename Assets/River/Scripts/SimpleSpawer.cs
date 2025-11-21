using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleSpawer : MonoBehaviour
{

    public List<GameObject> prefabs = new List<GameObject>();


    void Update()
    {
        if(Input.GetKeyDown(KeyCode.K)){
            Instantiate(prefabs[Random.Range(0,prefabs.Count)],transform.position,Quaternion.identity);
        }
    }
}
