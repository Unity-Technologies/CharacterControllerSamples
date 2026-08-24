using UnityEngine;

namespace OnlineFPS
{
    public static class GameInput
    {
        public static FPSInputActions InputActions;
        public const float InputWrapAroundValue = 3000f;
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            InputActions = new FPSInputActions();
            InputActions.Enable();
            InputActions.DefaultMap.Enable();
            InputActions.UI.Enable();
        }
    }
}