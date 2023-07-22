using SharpGLTF.Schema2;
using System.Text.Json;
using WolvenKit.RED4.Types;

namespace GltfTest.Extras;

public class CNameParameter : ExtraProperties
{
    private String? _name;
    private UInt64? _hash;

    public String? Name
    {
        get => _name;
        set => _name = value;
    }

    public UInt64? Hash
    {
        get => _hash;
        set => _hash = value;
    }

    protected override void SerializeProperties(Utf8JsonWriter writer)
    {
        base.SerializeProperties(writer);
        SerializeProperty(writer, "name", _name);
        SerializeProperty(writer, "hash", _hash);
    }

    protected override void DeserializeProperty(string jsonPropertyName, ref Utf8JsonReader reader)
    {
        switch (jsonPropertyName)
        {
            case "name": _name = DeserializePropertyValue<String?>(ref reader); break;
            case "hash": _hash = DeserializePropertyValue<UInt64?>(ref reader); break;
            default: base.DeserializeProperty(jsonPropertyName, ref reader); break;
        }
    }

    public static explicit operator CNameParameter(CMaterialParameterCpuNameU64 parameter)
    {
        if (parameter.Name.TryGetResolvedText(out var str))
        {
            return new CNameParameter { Name = str };
        }
        else
        {
            return new CNameParameter { Hash = parameter.Name };
        }
    }
}