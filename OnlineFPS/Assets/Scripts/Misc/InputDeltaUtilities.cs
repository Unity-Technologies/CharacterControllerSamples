using Unity.Mathematics;

namespace OnlineFPS
{
    public static class InputDeltaUtilities
    {
        public const float InputWrapAroundValue = 2000f;

        public static void AddInputDelta(ref float input, float addedDelta)
        {
            input = math.fmod(input + addedDelta, InputWrapAroundValue);
        }

        public static void AddInputDelta(ref float2 input, float2 addedDelta)
        {
            input = math.fmod(input + addedDelta, InputWrapAroundValue);
        }

        public static float GetInputDelta(float currentValue, float previousValue)
        {
            float delta = currentValue - previousValue;

            // When delta is very large, consider that the input has wrapped around
            if (math.abs(delta) > (InputWrapAroundValue * 0.5f))
            {
                delta += (math.sign(previousValue - currentValue) * InputWrapAroundValue);
            }

            return delta;
        }

        public static float2 GetInputDelta(float2 currentValue, float2 previousValue)
        {
            float2 delta = currentValue - previousValue;

            // When delta is very large, consider that the input has wrapped around
            if (math.abs(delta.x) > (InputWrapAroundValue * 0.5f))
            {
                delta.x += (math.sign(previousValue.x - currentValue.x) * InputWrapAroundValue);
            }

            if (math.abs(delta.y) > (InputWrapAroundValue * 0.5f))
            {
                delta.y += (math.sign(previousValue.y - currentValue.y) * InputWrapAroundValue);
            }

            return delta;
        }
    }
}