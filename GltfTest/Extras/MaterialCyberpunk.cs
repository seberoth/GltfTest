using SharpGLTF.Schema2;
using System.Text.Json;

namespace GltfTest.Extras;

internal sealed class MaterialCyberpunk : ExtraProperties
{
    private readonly Material _parent;

    private int? _albedo;
    private int? _secondaryAlbedo;
    private ScalarInfo? _secondaryAlbedoInfluence;
    private ScalarInfo? _secondaryAlbedoTintColorInfluence;
    private int? _normal;
    private int? _detailNormal;
    private int? _roughness;
    private float? _detailRoughnessBiasMin;
    private float? _detailRoughnessBiasMax;

    internal MaterialCyberpunk(Material parent)
    {
        _parent = parent;
    }

    public Image? Albedo
    {
        get => _albedo.HasValue ? _parent.LogicalParent.LogicalImages[_albedo.Value] : null;
        set => _albedo = value?.LogicalIndex;
    }

    public Image? SecondaryAlbedo
    {
        get => _secondaryAlbedo.HasValue ? _parent.LogicalParent.LogicalImages[_secondaryAlbedo.Value] : null;
        set => _secondaryAlbedo = value?.LogicalIndex;
    }

    public ScalarInfo? SecondaryAlbedoInfluence
    {
        get => _secondaryAlbedoInfluence;
        set => _secondaryAlbedoInfluence = value;
    }

    public ScalarInfo? SecondaryAlbedoTintColorInfluence
    {
        get => _secondaryAlbedoTintColorInfluence;
        set => _secondaryAlbedoTintColorInfluence = value;
    }

    public Image? Normal
    {
        get => _normal.HasValue ? _parent.LogicalParent.LogicalImages[_normal.Value] : null;
        set => _normal = value?.LogicalIndex;
    }

    public Image? DetailNormal
    {
        get => _detailNormal.HasValue ? _parent.LogicalParent.LogicalImages[_detailNormal.Value] : null;
        set => _detailNormal = value?.LogicalIndex;
    }

    public Image? Roughness
    {
        get => _roughness.HasValue ? _parent.LogicalParent.LogicalImages[_roughness.Value] : null;
        set => _roughness = value?.LogicalIndex;
    }

    public float? DetailRoughnessBiasMin
    {
        get => _detailRoughnessBiasMin;
        set => _detailRoughnessBiasMin = value;
    }

    public float? DetailRoughnessBiasMax
    {
        get => _detailRoughnessBiasMax;
        set => _detailRoughnessBiasMax = value;
    }

    protected override void SerializeProperties(Utf8JsonWriter writer)
    {
        base.SerializeProperties(writer);
        SerializeProperty(writer, "albedo", _albedo);
        SerializeProperty(writer, "secondaryAlbedo", _secondaryAlbedo);
        SerializePropertyObject(writer, "secondaryAlbedoInfluence", _secondaryAlbedoInfluence);
        SerializePropertyObject(writer, "secondaryAlbedoTintColorInfluence", _secondaryAlbedoTintColorInfluence);
        SerializeProperty(writer, "normal", _normal);
        SerializeProperty(writer, "detailNormal", _detailNormal);
        SerializeProperty(writer, "roughness", _roughness);
        SerializeProperty(writer, "detailRoughnessBiasMax", _detailRoughnessBiasMax);
    }

    protected override void DeserializeProperty(string jsonPropertyName, ref Utf8JsonReader reader)
    {
        switch (jsonPropertyName)
        {
            case "albedo": _albedo = DeserializePropertyValue<int?>(ref reader); break;
            case "secondaryAlbedo": _secondaryAlbedo = DeserializePropertyValue<int?>(ref reader); break;
            case "secondaryAlbedoInfluence": _secondaryAlbedoInfluence = DeserializePropertyValue<ScalarInfo?>(ref reader); break;
            case "secondaryAlbedoTintColorInfluence": _secondaryAlbedoTintColorInfluence = DeserializePropertyValue<ScalarInfo?>(ref reader); break;
            case "normal": _normal = DeserializePropertyValue<int?>(ref reader); break;
            case "detailNormal": _detailNormal = DeserializePropertyValue<int?>(ref reader); break;
            case "roughness": _roughness = DeserializePropertyValue<int?>(ref reader); break;
            case "detailRoughnessBiasMin": _detailRoughnessBiasMin = DeserializePropertyValue<float?>(ref reader); break;
            case "detailRoughnessBiasMax": _detailRoughnessBiasMax = DeserializePropertyValue<float?>(ref reader); break;
            default: base.DeserializeProperty(jsonPropertyName, ref reader); break;
        }
    }
}