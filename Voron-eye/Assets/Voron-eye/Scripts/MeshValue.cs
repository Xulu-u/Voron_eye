using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshValue : MonoBehaviour
{
    public float yValue;
    public static MeshValue ins;

    private void Awake()
    {
        ins = this;
    }
}
