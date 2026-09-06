using System;
using System.Numerics;

namespace ClickToMove.Util;

internal static class MathUtil
{
    public static float DistanceXZ(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }

    // Yaw that faces from -> to, in the game's rotation convention.
    public static float YawTo(Vector3 from, Vector3 to)
    {
        return MathF.Atan2(to.X - from.X, to.Z - from.Z);
    }

    // Picks W/A/S/D keys that walk toward a screen-space offset.
    // Screen origin is upper left, Y grows down, so screen-up is forward.
    public static void KeysForScreenDelta(Vector2 delta, out bool w, out bool a, out bool s, out bool d)
    {
        w = a = s = d = false;
        if (delta.LengthSquared() < 4.0f)
        {
            w = true;
            return;
        }

        var angleDeg = MathF.Atan2(delta.X, -delta.Y) * (180.0f / MathF.PI);
        if (angleDeg >= -22.5f && angleDeg <= 22.5f)
        {
            w = true;
        }
        else if (angleDeg > 22.5f && angleDeg <= 67.5f)
        {
            w = true;
            d = true;
        }
        else if (angleDeg > 67.5f && angleDeg <= 112.5f)
        {
            d = true;
        }
        else if (angleDeg > 112.5f && angleDeg <= 157.5f)
        {
            s = true;
            d = true;
        }
        else if (angleDeg > 157.5f || angleDeg < -157.5f)
        {
            s = true;
        }
        else if (angleDeg >= -157.5f && angleDeg < -112.5f)
        {
            s = true;
            a = true;
        }
        else if (angleDeg >= -112.5f && angleDeg < -67.5f)
        {
            a = true;
        }
        else
        {
            w = true;
            a = true;
        }
    }
}
