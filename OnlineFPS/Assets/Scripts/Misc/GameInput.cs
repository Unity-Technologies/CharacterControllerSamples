using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OnlineFPS
{
    public static class GameInput
    {
        public static FPSInputActions InputActions;
        public const float InputWrapAroundValue = 3000f;

        public static void Initialize()
        {
            InputActions = new FPSInputActions();
            InputActions.Enable();
            InputActions.DefaultMap.Enable();
            InputActions.UI.Enable();
        }
    }
}