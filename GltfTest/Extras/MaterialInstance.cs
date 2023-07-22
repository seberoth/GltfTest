using SharpGLTF.Schema2;
using System.Text.Json;
using SharpGLTF.IO;

namespace GltfTest.Extras;

internal class MaterialInstance : ExtraProperties
{
    private readonly Material _parent;

    private string _template;
    private Dictionary<string, MaterialParameter> _parameters = new();

    internal MaterialInstance(Material parent)
    {
        _parent = parent;
    }

    public string Template
    {
        get => _template;
        set => _template = value;
    }

    public Dictionary<string, MaterialParameter> Parameters
    {
        get => _parameters;
        set => _parameters = value;
    }

    protected override void SerializeProperties(Utf8JsonWriter writer)
    {
        base.SerializeProperties(writer);
        SerializeProperty(writer, "Template", _template);
        foreach (var (key, value) in _parameters)
        {
            if (value is JsonSerializable serializable)
            {
                SerializePropertyObject(writer, key, serializable);
            }
            else
            {
                SerializeProperty(writer, key, value);
            }
        }
    }

    protected override void DeserializeProperty(string jsonPropertyName, ref Utf8JsonReader reader)
    {
        switch (jsonPropertyName)
        {
            case "Template": _template = DeserializePropertyValue<string>(ref reader); break;
            default: _parameters.Add(jsonPropertyName, DeserializePropertyValue<MaterialParameter>(ref reader)); break;
        }
    }
}