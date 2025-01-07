using System.Runtime.CompilerServices;

namespace VRCOSC.Modules.OSCLeash.Utils;

public static class MathHelper
{
    public static float LerpAngle(float start, float end, float amount)
    {
        float difference = end - start;
        while (difference < -180)
            difference += 360;
        while (difference > 180)
            difference -= 360;
        return start + difference * amount;
    }

    public static float Lerp(float start, float end, float amount)
    {
        return start + (end - start) * amount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Clamp(float n)
    {
        if (float.IsNaN(n) || float.IsInfinity(n))
        {
            return 0.0f;
        }
        return Math.Max(-1.0f, Math.Min(n, 1.0f));
    }
} 