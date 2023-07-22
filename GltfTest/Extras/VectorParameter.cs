using SharpGLTF.Schema2;
using System.Text.Json;
using WolvenKit.RED4.Types;

namespace GltfTest.Extras;

public class VectorParameter : ExtraProperties
{
    private Single _x;
    private Single _y;
    private Single _z;
    private Single _w;

    public Single X
    {
        get => _x;
        set => _x = value;
    }

    public Single Y
    {
        get => _y;
        set => _y = value;
    }

    public Single Z
    {
        get => _z;
        set => _z = value;
    }

    public Single W
    {
        get => _w;
        set => _w = value;
    }

    protected override void SerializeProperties(Utf8JsonWriter writer)
    {
        base.SerializeProperties(writer);
        SerializeProperty(writer, "x", _x);
        SerializeProperty(writer, "y", _y);
        SerializeProperty(writer, "z", _z);
        SerializeProperty(writer, "w", _w);
    }

    protected override void DeserializeProperty(string jsonPropertyName, ref Utf8JsonReader reader)
    {
        switch (jsonPropertyName)
        {
            case "x": _x = DeserializePropertyValue<Single>(ref reader); break;
            case "y": _y = DeserializePropertyValue<Single>(ref reader); break;
            case "z": _z = DeserializePropertyValue<Single>(ref reader); break;
            case "w": _w = DeserializePropertyValue<Single>(ref reader); break;
            default: base.DeserializeProperty(jsonPropertyName, ref reader); break;
        }
    }

    public static explicit operator VectorParameter(CMaterialParameterVector parameter) =>
        new()
        {
            X = parameter.Vector.X,
            Y = parameter.Vector.Y,
            Z = parameter.Vector.Z,
            W = parameter.Vector.W,
        };
}