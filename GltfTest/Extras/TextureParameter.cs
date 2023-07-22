using SharpGLTF.Schema2;
using System.Text.Json;
using WolvenKit.RED4.Types;

namespace GltfTest.Extras;

public class TextureParameter : ExtraProperties
{
    private readonly Material _parent;

    private Int32? _image;

    internal TextureParameter() { }

    internal TextureParameter(Material parent)
    {
        _parent = parent;
    }

    public Image? Image
    {
        get => _image.HasValue ? _parent.LogicalParent.LogicalImages[_image.Value] : null;
        set => _image = value?.LogicalIndex;
    }

    protected override void SerializeProperties(Utf8JsonWriter writer)
    {
        base.SerializeProperties(writer);
        SerializeProperty(writer, "image", _image);
    }

    protected override void DeserializeProperty(string jsonPropertyName, ref Utf8JsonReader reader)
    {
        switch (jsonPropertyName)
        {
            case "image": _image = DeserializePropertyValue<Int32?>(ref reader); break;
            default: base.DeserializeProperty(jsonPropertyName, ref reader); break;
        }
    }
}