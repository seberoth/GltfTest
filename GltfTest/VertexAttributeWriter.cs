using System.Numerics;
using static CommunityToolkit.Mvvm.ComponentModel.__Internals.__TaskExtensions.TaskAwaitableWithoutEndValidation;

namespace GltfTest;

public class VertexAttributeWriter : BinaryWriter
{
    public VertexAttributeWriter(Stream stream) : base(stream)
    {

    }

    public void WriteShort4N(Vector3 vector)
    {
        Write((Int16)(vector.X * Int16.MaxValue));
        Write((Int16)(vector.Y * Int16.MaxValue));
        Write((Int16)(vector.Z * Int16.MaxValue));
        Write(Int16.MaxValue);
    }

    public void WriteUByte4(Vector4 vector)
    {
        Write((Byte)vector.X);
        Write((Byte)vector.Y);
        Write((Byte)vector.Z);
        Write((Byte)vector.W);
    }

    public void WriteUByte4N(Vector4 vector)
    {
        Write((Byte)(vector.X * Byte.MaxValue));
        Write((Byte)(vector.Y * Byte.MaxValue));
        Write((Byte)(vector.Z * Byte.MaxValue));
        Write((Byte)(vector.W * Byte.MaxValue));
    }

    public void WriteFloat16_2(Vector2 vector)
    {
        Write((Half)vector.X);
        Write((Half)vector.Y);
    }

    public void WriteFloat16_4(Vector3 vector)
    {
        Write((Half)vector.X);
        Write((Half)vector.Y);
        Write((Half)vector.Z);
        Write((Half)0);
    }

    public void WriteDec4(Vector3 vector)
    {
        var quant = 1f / 1023f;

        var x = (UInt16)((vector.X + 1F) / quant / 2);
        var y = (UInt16)((vector.Y + 1F) / quant / 2);
        var z = (UInt16)((vector.Z + 1F) / quant / 2);
        var w = (Byte)0;

        var u32 = (uint)((x & 0x3FF) | (y & 0x3FF) << 10 | (z & 0x3FF) << 20 | (w & 0x3) << 30);

        Write(u32);
    }

    public void WriteDec4(Vector4 vector)
    {
        var quant = 1f / 1023f;

        var x = (UInt16)((vector.X + 1F) / quant / 2);
        var y = (UInt16)((vector.Y + 1F) / quant / 2);
        var z = (UInt16)((vector.Z + 1F) / quant / 2);
        var w = (Byte)0;

        var u32 = (uint)((x & 0x3FF) | (y & 0x3FF) << 10 | (z & 0x3FF) << 20 | (w & 0x3) << 30);

        Write(u32);
    }

    public void WriteColor(Vector4 vector)
    {
        Write((Byte)(vector.X * Byte.MaxValue));
        Write((Byte)(vector.Y * Byte.MaxValue));
        Write((Byte)(vector.Z * Byte.MaxValue));
        Write((Byte)(vector.W * Byte.MaxValue));
    }
}