using SharpGLTF.Schema2;
using System.Drawing;
using System.Text.Json;
using WolvenKit.RED4.Types;
using YamlDotNet.Core.Tokens;

namespace GltfTest.Extras;

public class MaterialParameter : ExtraProperties
{
    private String _type;
    private ExtraProperties _value;

    public String Type
    {
        get => _type;
        set => _type = value;
    }

    public ExtraProperties Value
    {
        get => _value;
        set => _value = value;
    }

    protected override void SerializeProperties(Utf8JsonWriter writer)
    {
        base.SerializeProperties(writer);
        SerializeProperty(writer, "$type", _type);
        SerializeProperty(writer, "$value", _value);
    }

    protected override void DeserializeProperty(string jsonPropertyName, ref Utf8JsonReader reader)
    {
        switch (jsonPropertyName)
        {
            case "$type": _type = DeserializePropertyValue<String>(ref reader); break;
            case "$value":
                switch (Type)
                {
                    case "Color":
                        _value = DeserializePropertyValue<ColorParameter>(ref reader);
                        break;

                    case "CpuNameU64":
                        _value = DeserializePropertyValue<CNameParameter>(ref reader);
                        break;

                    case "Cube":
                        _value = DeserializePropertyValue<ResourceReferenceParameter>(ref reader);
                        break;

                    case "DynamicTexture":
                        _value = DeserializePropertyValue<ResourceReferenceParameter>(ref reader);
                        break;

                    case "FoliageParameters":
                        _value = DeserializePropertyValue<ResourceReferenceParameter>(ref reader);
                        break;

                    case "Gradient":
                        _value = DeserializePropertyValue<ResourceReferenceParameter>(ref reader);
                        break;

                    case "HairParameters":
                        _value = DeserializePropertyValue<ResourceReferenceParameter>(ref reader);
                        break;

                    case "MultilayerMask":
                        _value = DeserializePropertyValue<ResourceReferenceParameter>(ref reader);
                        break;

                    case "MultilayerSetup":
                        _value = DeserializePropertyValue<ResourceReferenceParameter>(ref reader);
                        break;

                    case "Scalar":
                        _value = DeserializePropertyValue<ScalarParameter>(ref reader);
                        break;

                    case "SkinParameters":
                        _value = DeserializePropertyValue<ResourceReferenceParameter>(ref reader);
                        break;

                    case "StructBuffer":
                        _value = DeserializePropertyValue<StructBufferParameter>(ref reader);
                        break;

                    case "TerrainSetup":
                        _value = DeserializePropertyValue<ResourceReferenceParameter>(ref reader);
                        break;

                    case "Texture":
                        _value = DeserializePropertyValue<ResourceReferenceParameter>(ref reader);
                        break;

                    case "TextureArray":
                        _value = DeserializePropertyValue<ResourceReferenceParameter>(ref reader);
                        break;

                    case "Vector":
                        _value = DeserializePropertyValue<VectorParameter>(ref reader);
                        break;

                    default:
                        _value = DeserializePropertyValue<ResourceReferenceParameter>(ref reader);
                        break;
                }
                break;
            default: base.DeserializeProperty(jsonPropertyName, ref reader); break;
        }
    }

    public static explicit operator MaterialParameter(CMaterialParameter parameter)
    {
        switch (parameter)
        {
            case CMaterialParameterColor color:
                return (MaterialParameter)color;
            case CMaterialParameterCpuNameU64 cpuNameU64:
                return (MaterialParameter)cpuNameU64;
            case CMaterialParameterCube cube:
                return (MaterialParameter)cube;
            case CMaterialParameterDynamicTexture dynamicTexture:
                return (MaterialParameter)dynamicTexture;
            case CMaterialParameterFoliageParameters foliageParameters:
                return (MaterialParameter)foliageParameters;
            case CMaterialParameterGradient gradient:
                return (MaterialParameter)gradient;
            case CMaterialParameterHairParameters hairParameters:
                return (MaterialParameter)hairParameters;
            case CMaterialParameterMultilayerMask multilayerMask:
                return (MaterialParameter)multilayerMask;
            case CMaterialParameterMultilayerSetup multilayerSetup:
                return (MaterialParameter)multilayerSetup;
            case CMaterialParameterScalar scalar:
                return (MaterialParameter)scalar;
            case CMaterialParameterSkinParameters skinParameters:
                return (MaterialParameter)skinParameters;
            case CMaterialParameterStructBuffer structBuffer:
                return (MaterialParameter)structBuffer;
            case CMaterialParameterTerrainSetup terrainSetup:
                return (MaterialParameter)terrainSetup;
            case CMaterialParameterTexture texture:
                return (MaterialParameter)texture;
            case CMaterialParameterTextureArray textureArray:
                return (MaterialParameter)textureArray;
            case CMaterialParameterVector vector:
                return (MaterialParameter)vector;
        }

        throw new NotSupportedException();
    }

    public static explicit operator MaterialParameter(CMaterialParameterColor parameter)
    {
        return new MaterialParameter
        {
            Type = "Color",
            Value = (ColorParameter)parameter
        };
    }

    public static explicit operator MaterialParameter(CMaterialParameterCpuNameU64 parameter)
    {
        return new MaterialParameter
        {
            Type = "CpuNameU64",
            Value = (CNameParameter)parameter
        };
    }

    public static explicit operator MaterialParameter(CMaterialParameterCube parameter)
    {
        return new MaterialParameter
        {
            Type = "Cube",
            Value = (ResourceReferenceParameter)parameter
        };
    }

    public static explicit operator MaterialParameter(CMaterialParameterDynamicTexture parameter)
    {
        return new MaterialParameter
        {
            Type = "DynamicTexture",
            Value = (ResourceReferenceParameter)parameter
        };
    }

    public static explicit operator MaterialParameter(CMaterialParameterFoliageParameters parameter)
    {
        return new MaterialParameter
        {
            Type = "FoliageParameters",
            Value = (ResourceReferenceParameter)parameter
        };
    }

    public static explicit operator MaterialParameter(CMaterialParameterGradient parameter)
    {
        return new MaterialParameter
        {
            Type = "Gradient",
            Value = (ResourceReferenceParameter)parameter
        };
    }

    public static explicit operator MaterialParameter(CMaterialParameterHairParameters parameter)
    {
        return new MaterialParameter
        {
            Type = "HairParameters",
            Value = (ResourceReferenceParameter)parameter
        };
    }

    public static explicit operator MaterialParameter(CMaterialParameterMultilayerMask parameter)
    {
        return new MaterialParameter
        {
            Type = "MultilayerMask",
            Value = (ResourceReferenceParameter)parameter
        };
    }

    public static explicit operator MaterialParameter(CMaterialParameterMultilayerSetup parameter)
    {
        return new MaterialParameter
        {
            Type = "MultilayerSetup",
            Value = (ResourceReferenceParameter)parameter
        };
    }

    public static explicit operator MaterialParameter(CMaterialParameterScalar parameter)
    {
        return new MaterialParameter
        {
            Type = "Scalar",
            Value = (ScalarParameter)parameter
        };
    }

    public static explicit operator MaterialParameter(CMaterialParameterSkinParameters parameter)
    {
        return new MaterialParameter
        {
            Type = "SkinParameters",
            Value = (ResourceReferenceParameter)parameter
        };
    }

    public static explicit operator MaterialParameter(CMaterialParameterStructBuffer parameter)
    {
        return new MaterialParameter
        {
            Type = "StructBuffer",
            Value = (StructBufferParameter)parameter
        };
    }

    public static explicit operator MaterialParameter(CMaterialParameterTerrainSetup parameter)
    {
        return new MaterialParameter
        {
            Type = "TerrainSetup",
            Value = (ResourceReferenceParameter)parameter
        };
    }

    public static explicit operator MaterialParameter(CMaterialParameterTexture parameter)
    {
        return new MaterialParameter
        {
            Type = "Texture",
            Value = (ResourceReferenceParameter)parameter
        };
    }

    public static explicit operator MaterialParameter(CMaterialParameterTextureArray parameter)
    {
        return new MaterialParameter
        {
            Type = "TextureArray",
            Value = (ResourceReferenceParameter)parameter
        };
    }

    public static explicit operator MaterialParameter(CMaterialParameterVector parameter)
    {
        return new MaterialParameter
        {
            Type = "Vector",
            Value = (VectorParameter)parameter
        };
    }
}