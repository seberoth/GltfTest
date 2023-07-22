using SharpGLTF.Schema2;
using System.Text.Json;
using WolvenKit.RED4.Types;

namespace GltfTest.Extras;

public class StructBufferParameter : ExtraProperties
{
    protected override void SerializeProperties(Utf8JsonWriter writer)
    {
        base.SerializeProperties(writer);
    }

    protected override void DeserializeProperty(string jsonPropertyName, ref Utf8JsonReader reader)
    {
        switch (jsonPropertyName)
        {
            default: base.DeserializeProperty(jsonPropertyName, ref reader); break;
        }
    }

    public static explicit operator StructBufferParameter(CMaterialParameterStructBuffer parameter) => new();
}