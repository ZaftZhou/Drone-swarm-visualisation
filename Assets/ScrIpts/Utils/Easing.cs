using UnityEngine;

namespace Utils
{
    public static class Easing
    {
        public static float EaseInCircular(float x)
        {
            return 1 - Mathf.Sqrt(1 - Mathf.Pow(x, 2));
        }
        public static float EaseOutCircular(float x)
        {
            return Mathf.Sqrt(1 - Mathf.Pow(x - 1, 2));
        }

        public static float EaseInExponential(float x)
        {
            return x == 0.0f ? 0.0f : Mathf.Pow(2, 10 * x - 10);
        }

        public static float EaseOutExponential(float x)
        {
            return x == 1.0f ? 1.0f : 1 - Mathf.Pow(2, -10 * x);
        }
        public static float EaseInSine(float x)
        {
            return 1 - Mathf.Cos((x * Mathf.PI) / 2);

        }
    }
}
