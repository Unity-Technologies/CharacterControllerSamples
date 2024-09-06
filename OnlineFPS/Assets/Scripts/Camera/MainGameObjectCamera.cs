

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OnlineFPS
{
    public class MainGameObjectCamera : MonoBehaviour
    {
        public static Camera Instance;

        void Awake()
        {
            Instance = GetComponent<UnityEngine.Camera>();
        }
    }
}