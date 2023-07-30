﻿using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using SharpDX.Direct3D11;
using SharpGLTF.IO;

namespace GltfTest;

// https://github.com/vpenades/SharpGLTF/blob/master/src/Shared/_Extensions.cs
internal static class Extensions
{
    private const float _UnitLengthThresholdVec3 = 0.00674f;

    internal static bool _IsFinite(this float value)
    {
        return !(float.IsNaN(value) || float.IsInfinity(value));
    }

    internal static bool _IsFinite(this Vector3 v)
    {
        return v.X._IsFinite() && v.Y._IsFinite() && v.Z._IsFinite();
    }

    internal static Boolean IsNormalized(this Vector3 normal)
    {
        if (!normal._IsFinite()) return false;

        return Math.Abs(normal.Length() - 1) <= _UnitLengthThresholdVec3;
    }

    internal static bool IsValidTangent(this Vector4 tangent)
    {
        if (tangent.W != 1 && tangent.W != -1) return false;

        return new Vector3(tangent.X, tangent.Y, tangent.Z).IsNormalized();
    }

    internal static Vector3 SanitizeNormal(this Vector3 normal)
    {
        if (normal == Vector3.Zero) return Vector3.UnitX;
        return normal.IsNormalized() ? normal : Vector3.Normalize(normal);
    }

    internal static Vector4 SanitizeTangent(this Vector4 tangent)
    {
        var n = new Vector3(tangent.X, tangent.Y, tangent.Z).SanitizeNormal();
        var s = float.IsNaN(tangent.W) ? 1 : tangent.W;
        return new Vector4(n, s > 0 ? 1 : -1);
    }

    internal static bool TryGetValue<T>(this JsonContent content, [NotNullWhen(true)] out T? value, params IConvertible[] path) where T : IConvertible
    {
        try
        {
            value = content.GetValue<T>(path);
            return true;
        }
        catch (Exception)
        {
            value = default;
            return false;
        }
    }
}