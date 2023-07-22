using SharpGLTF.Schema2;
using System.Text.Json;
using WolvenKit.RED4.Types;

namespace GltfTest.Extras;

internal class ScalarParameter : ExtraProperties
{
    private Single _min;
    private Single _max;
    private Single _scalar;

    public Single Min
    {
        get => _min;
        set => _min = value;
    }

    public Single Max
    {
        get => _max;
        set => _max = value;
    }

    public Single Scalar
    {
        get => _scalar;
        set => _scalar = value;
    }

    protected override void SerializeProperties(Utf8JsonWriter writer)
    {
        base.SerializeProperties(writer);
        SerializeProperty(writer, "min", _min);
        SerializeProperty(writer, "max", _max);
        SerializeProperty(writer, "scalar", _scalar);
    }

    protected override void DeserializeProperty(string jsonPropertyName, ref Utf8JsonReader reader)
    {
        switch (jsonPropertyName)
        {
            case "min": _min = DeserializePropertyValue<Single>(ref reader); break;
            case "max": _max = DeserializePropertyValue<Single>(ref reader); break;
            case "scalar": _scalar = DeserializePropertyValue<Single>(ref reader); break;
            default: base.DeserializeProperty(jsonPropertyName, ref reader); break;
        }
    }

    public static explicit operator ScalarParameter(CMaterialParameterScalar parameter) => 
        new()
        {
            Min = parameter.Min, 
            Max = parameter.Max, 
            Scalar = parameter.Scalar
        };
}