using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameResourcesManaged : MonoBehaviour
{
    public static GameResourcesManaged Instance;
    
    public GameObject NameTagPrefab;

    private void OnEnable()
    {
        Instance = this;
    }
}
