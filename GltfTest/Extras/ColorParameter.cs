using SharpGLTF.Schema2;
using System.Text.Json;
using WolvenKit.RED4.Types;

namespace GltfTest.Extras;

public class ColorParameter : ExtraProperties
{
    private Byte _red;
    private Byte _green;
    private Byte _blue;
    private Byte _alpha;

    public Byte Red
    {
        get => _red;
        set => _red = value;
    }

    public Byte Green
    {
        get => _green;
        set => _green = value;
    }

    public Byte Blue
    {
        get => _blue;
        set => _blue = value;
    }

    public Byte Alpha
    {
        get => _alpha;
        set => _alpha = value;
    }

    protected override void SerializeProperties(Utf8JsonWriter writer)
    {
        base.SerializeProperties(writer);
        SerializeProperty(writer, "red", _red);
        SerializeProperty(writer, "green", _green);
        SerializeProperty(writer, "blue", _blue);
        SerializeProperty(writer, "alpha", _alpha);
    }

    protected override void DeserializeProperty(string jsonPropertyName, ref Utf8JsonReader reader)
    {
        switch (jsonPropertyName)
        {
            case "red": reader.Read(); _red = reader.GetByte(); break;
            case "green": reader.Read(); _green = reader.GetByte(); break;
            case "blue": reader.Read(); _blue = reader.GetByte(); break;
            case "alpha": reader.Read(); _alpha = reader.GetByte(); break;
            default: base.DeserializeProperty(jsonPropertyName, ref reader); break;
        }
    }

    public static explicit operator ColorParameter(CMaterialParameterColor parameter) =>
        new()
        {
            Red = parameter.Color.Red,
            Green = parameter.Color.Green,
            Blue = parameter.Color.Blue,
            Alpha = parameter.Color.Alpha,
        };
}