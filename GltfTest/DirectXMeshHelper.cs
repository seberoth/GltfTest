using System.Numerics;
using SharpGLTF.Schema2;

namespace GltfTest;

public static class DirectXMeshHelper
{
    public static void ComputeNormalsEqualWeight(MeshPrimitive? meshPrimitive, bool cw, out Vector3[] normals)
    {
        if (meshPrimitive == null)
        {
            normals = [];
            return;
        }

        var indices = meshPrimitive.GetIndices().Select(x => (int)x).ToArray();
        var positions = meshPrimitive.GetVertexAccessor("POSITION").AsVector3Array().ToArray();

        var vertexCount = positions.Length;
        var triangleCount = indices.Length / 3;

        normals = new Vector3[vertexCount];

        var vertNormals = new Vector3[vertexCount];

        for (var i = 0; i < triangleCount; i++)
        {
            var i1 = indices[i * 3 + 0];
            var i2 = indices[i * 3 + 1];
            var i3 = indices[i * 3 + 2];

            var v1 = positions[i1];
            var v2 = positions[i2];
            var v3 = positions[i3];

            var u = v2 - v1;
            var v = v3 - v1;

            var faceNormal = Vector3.Normalize(Vector3.Cross(u, v));

            vertNormals[i1] += faceNormal;
            vertNormals[i2] += faceNormal;
            vertNormals[i3] += faceNormal;
        }

        if (cw)
        {
            for (var i = 0; i < vertexCount; i++)
            {
                var n = Vector3.Normalize(vertNormals[i]);
                n = Vector3.Negate(n);
                normals[i] = n;
            }
        }
        else
        {
            for (var i = 0; i < vertexCount; i++)
            {
                var n = Vector3.Normalize(vertNormals[i]);
                normals[i] = n;
            }
        }
    }

    public static void ComputeNormalsWeightedByAngle(MeshPrimitive? meshPrimitive, bool cw, out Vector3[] normals)
    {
        if (meshPrimitive == null)
        {
            normals = [];
            return;
        }

        var indices = meshPrimitive.GetIndices().Select(x => (int)x).ToArray();
        var positions = meshPrimitive.GetVertexAccessor("POSITION").AsVector3Array().ToArray();

        var vertexCount = positions.Length;
        var triangleCount = indices.Length / 3;

        normals = new Vector3[vertexCount];

        var vertNormals = new Vector3[vertexCount];

        for (var i = 0; i < triangleCount; i++)
        {
            var i0 = indices[i * 3 + 0];
            var i1 = indices[i * 3 + 1];
            var i2 = indices[i * 3 + 2];

            var p0 = positions[i0];
            var p1 = positions[i1];
            var p2 = positions[i2];

            var u = p1 - p0;
            var v = p2 - p0;

            var faceNormal = Vector3.Normalize(Vector3.Cross(u, v));

            // Corner 0 -> 1 - 0, 2 - 0
            var a = Vector3.Normalize(u);
            var b = Vector3.Normalize(v);
            var w0 = Dot(a, b);
            w0 = Vector3.Clamp(w0, new Vector3(-1F), new Vector3(1F));
            w0 = ACos(w0);

            // Corner 1 -> 2 - 1, 0 - 1
            var c = Vector3.Normalize(p2 - p1);
            var d = Vector3.Normalize(p0 - p1);
            var w1 = Dot(c, d);
            w1 = Vector3.Clamp(w1, new Vector3(-1F), new Vector3(1F));
            w1 = ACos(w1);

            // Corner 2 -> 0 - 2, 1 - 2
            var e = Vector3.Normalize(p0 - p2);
            var f = Vector3.Normalize(p1 - p2);
            var w2 = Dot(e, f);
            w2 = Vector3.Clamp(w2, new Vector3(-1F), new Vector3(1F));
            w2 = ACos(w2);

            vertNormals[i0] = MultiplyAdd(faceNormal, w0, vertNormals[i0]);
            vertNormals[i1] = MultiplyAdd(faceNormal, w1, vertNormals[i1]);
            vertNormals[i2] = MultiplyAdd(faceNormal, w2, vertNormals[i2]);
        }

        if (cw)
        {
            for (var i = 0; i < vertexCount; i++)
            {
                var n = Vector3.Normalize(vertNormals[i]);
                n = Vector3.Negate(n);
                normals[i] = n;
            }
        }
        else
        {
            for (var i = 0; i < vertexCount; i++)
            {
                var n = Vector3.Normalize(vertNormals[i]);
                normals[i] = n;
            }
        }
    }

    public static void ComputeNormalsWeightedByArea(MeshPrimitive? meshPrimitive, bool cw, out Vector3[] normals)
    {
        if (meshPrimitive == null)
        {
            normals = [];
            return;
        }

        var indices = meshPrimitive.GetIndices().Select(x => (int)x).ToArray();
        var positions = meshPrimitive.GetVertexAccessor("POSITION").AsVector3Array().ToArray();

        var vertexCount = positions.Length;
        var triangleCount = indices.Length / 3;

        normals = new Vector3[vertexCount];

        var vertNormals = new Vector3[vertexCount];

        for (var i = 0; i < triangleCount; i++)
        {
            var i0 = indices[i * 3 + 0];
            var i1 = indices[i * 3 + 1];
            var i2 = indices[i * 3 + 2];

            var p0 = positions[i0];
            var p1 = positions[i1];
            var p2 = positions[i2];

            var u = p1 - p0;
            var v = p2 - p0;

            var faceNormal = Vector3.Normalize(Vector3.Cross(u, v));

            // Corner 0 -> 1 - 0, 2 - 0
            var w0 = Vector3.Cross(u, v);
            w0 = Length(w0);

            // Corner 1 -> 2 - 1, 0 - 1
            var c = p2 - p1;
            var d = p0 - p1;
            var w1 = Vector3.Cross(c, d);
            w1 = Length(w1);

            // Corner 2 -> 0 - 2, 1 - 2
            var e = p0 - p2;
            var f = p1 - p2;
            var w2 = Vector3.Cross(e, f);
            w2 = Length(w2);

            vertNormals[i0] = MultiplyAdd(faceNormal, w0, vertNormals[i0]);
            vertNormals[i1] = MultiplyAdd(faceNormal, w1, vertNormals[i1]);
            vertNormals[i2] = MultiplyAdd(faceNormal, w2, vertNormals[i2]);
        }

        if (cw)
        {
            for (var i = 0; i < vertexCount; i++)
            {
                var n = Vector3.Normalize(vertNormals[i]);
                n = Vector3.Negate(n);
                normals[i] = n;
            }
        }
        else
        {
            for (var i = 0; i < vertexCount; i++)
            {
                var n = Vector3.Normalize(vertNormals[i]);
                normals[i] = n;
            }
        }
    }

    public static void ComputeTangentFrame(MeshPrimitive? meshPrimitive, out Vector4[] tangents, out Vector3[] bitangents)
    {
        if (meshPrimitive == null)
        {
            tangents = [];
            bitangents = [];
            return;
        }

        var indices = meshPrimitive.GetIndices().Select(x => (int)x).ToArray();
        var positions = meshPrimitive.GetVertexAccessor("POSITION").AsVector3Array().ToArray();
        var normals = meshPrimitive.GetVertexAccessor("NORMAL").AsVector3Array().ToArray();
        var texCoords = meshPrimitive.GetVertexAccessor("TEXCOORD_0").AsVector2Array().ToArray();

        var vertexCount = positions.Length;
        var triangleCount = indices.Length / 3;

        tangents = new Vector4[vertexCount];
        bitangents = new Vector3[vertexCount];

        var tan1 = new Vector3[vertexCount];
        var tan2 = new Vector3[vertexCount];

        for (var i = 0; i < triangleCount; i++)
        {
            var i1 = indices[i * 3 + 0];
            var i2 = indices[i * 3 + 1];
            var i3 = indices[i * 3 + 2];

            var v1 = positions[i1];
            var v2 = positions[i2];
            var v3 = positions[i3];

            var w1 = texCoords[i1];
            var w2 = texCoords[i2];
            var w3 = texCoords[i3];

            var x1 = v2.X - v1.X;
            var x2 = v3.X - v1.X;
            var y1 = v2.Y - v1.Y;
            var y2 = v3.Y - v1.Y;
            var z1 = v2.Z - v1.Z;
            var z2 = v3.Z - v1.Z;

            var s1 = w2.X - w1.X;
            var s2 = w3.X - w1.X;
            var t1 = w2.Y - w1.Y;
            var t2 = w3.Y - w1.Y;

            var r = 1.0f / (s1 * t2 - s2 * t1);
            var sdir = new Vector3((t2 * x1 - t1 * x2) * r, (t2 * y1 - t1 * y2) * r, (t2 * z1 - t1 * z2) * r);
            var tdir = new Vector3((s1 * x2 - s2 * x1) * r, (s1 * y2 - s2 * y1) * r, (s1 * z2 - s2 * z1) * r);

            tan1[i1] += sdir;
            tan1[i2] += sdir;
            tan1[i3] += sdir;

            tan2[i1] += tdir;
            tan2[i2] += tdir;
            tan2[i3] += tdir;
        }

        for (var i = 0; i < vertexCount; i++)
        {
            var n = normals[i];
            var t = tan1[i];

            // Gram-Schmidt orthogonalize
            var tangent = Vector3.Normalize(t - n * Vector3.Dot(n, t));

            // Calculate handedness
            var w = (Vector3.Dot(Vector3.Cross(n, t), tan2[i]) < 0.0f) ? -1.0f : 1.0f;

            tangents[i] = new Vector4(tangent, w);
            bitangents[i] = Vector3.Cross(n, tangent) * w;
        }
    }

    private static Vector3 Dot(Vector3 value1, Vector3 value2)
    {
        return new Vector3(Vector3.Dot(value1, value2));
    }

    private static Vector3 Length(Vector3 value1)
    {
        return new Vector3(value1.Length());
    }

    private static Vector3 ACos(Vector3 value1)
    {
        return new Vector3(MathF.Acos(value1.X), MathF.Acos(value1.Y), MathF.Acos(value1.Z));
    }

    public static Vector3 MultiplyAdd(Vector3 value1, Vector3 value2, Vector3 value3)
    {
        return value1 * value2 + value3;
    }

    public static Vector4 MultiplyAdd(Vector4 value1, Vector4 value2, Vector4 value3)
    {
        return value1 * value2 + value3;
    }
}