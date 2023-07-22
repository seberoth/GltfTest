using SharpDX;
using SharpGLTF.Schema2;
using System.Text.Json;
using WolvenKit.RED4.Types;

namespace GltfTest.Extras;

public class ResourceReferenceParameter : ExtraProperties
{
    private ResourcePath _resourcePath;
    private InternalEnums.EImportFlags _flags;

    public ResourcePath ResourcePath
    {
        get => _resourcePath;
        set => _resourcePath = value;
    }

    public InternalEnums.EImportFlags Flags
    {
        get => _flags;
        set => _flags = value;
    }

    protected override void SerializeProperties(Utf8JsonWriter writer)
    {
        base.SerializeProperties(writer);
        if (_resourcePath.TryGetResolvedText(out var str))
        {
            SerializeProperty(writer, "pathName", str);
        }
        else
        {
            SerializeProperty(writer, "pathHash", (UInt64)_resourcePath);
        }
    }

    protected override void DeserializeProperty(string jsonPropertyName, ref Utf8JsonReader reader)
    {
        switch (jsonPropertyName)
        {
            case "pathName": _resourcePath = DeserializePropertyValue<String>(ref reader); break;
            case "pathHash": _resourcePath = DeserializePropertyValue<UInt64>(ref reader); break;
            default: base.DeserializeProperty(jsonPropertyName, ref reader); break;
        }
    }

    public static explicit operator ResourceReferenceParameter(CMaterialParameterCube parameter) => ToResourceParameter("Cube", parameter.Texture);
    public static explicit operator ResourceReferenceParameter(CMaterialParameterDynamicTexture parameter) => ToResourceParameter("DynamicTexture", parameter.Texture);
    public static explicit operator ResourceReferenceParameter(CMaterialParameterFoliageParameters parameter) => ToResourceParameter("FoliageParameters", parameter.FoliageProfile);
    public static explicit operator ResourceReferenceParameter(CMaterialParameterGradient parameter) => ToResourceParameter("Gradient", parameter.Gradient);
    public static explicit operator ResourceReferenceParameter(CMaterialParameterHairParameters parameter) => ToResourceParameter("HairParameters", parameter.HairProfile);
    public static explicit operator ResourceReferenceParameter(CMaterialParameterMultilayerMask parameter) => ToResourceParameter("MultilayerMask", parameter.Mask);
    public static explicit operator ResourceReferenceParameter(CMaterialParameterMultilayerSetup parameter) => ToResourceParameter("MultilayerSetup", parameter.Setup);
    public static explicit operator ResourceReferenceParameter(CMaterialParameterSkinParameters parameter) => ToResourceParameter("SkinParameters", parameter.SkinProfile);
    public static explicit operator ResourceReferenceParameter(CMaterialParameterTerrainSetup parameter) => ToResourceParameter("TerrainSetup", parameter.Setup);
    public static explicit operator ResourceReferenceParameter(CMaterialParameterTexture parameter) => ToResourceParameter("Texture", parameter.Texture);
    public static explicit operator ResourceReferenceParameter(CMaterialParameterTextureArray parameter) => ToResourceParameter("TextureArray", parameter.Texture);

    private static ResourceReferenceParameter ToResourceParameter(string type, IRedRef resourceReference)
    {
        return new ResourceReferenceParameter
        {
            ResourcePath = resourceReference.DepotPath,
            Flags = resourceReference.Flags
        };
    }
}