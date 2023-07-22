using GltfTest.Extras;
using SharpGLTF.Schema2;
using WolvenKit.Core.Extensions;
using WolvenKit.RED4.Types;
using Vector4 = WolvenKit.RED4.Types.Vector4;

namespace GltfTest;

public class MaterialParameterDictionary : ExtraProperties
{
    public string? BaseTemplate { get; set; }

    public Dictionary<string, CMaterialParameter> Parameters = new();
    public Dictionary<ResourcePath, CResource> Resources = new();

    public void Add(CArray<CHandle<CMaterialParameter>> parameters)
    {
        foreach (var parameterHandle in parameters)
        {
            if (parameterHandle.Chunk is not { } materialParameter)
            {
                continue;
            }

            Parameters.Add(materialParameter.ParameterName.GetResolvedText()!, materialParameter);
        }
    }

    public void Add(CArray<CKeyValuePair> parameters)
    {
        foreach (var parameter in parameters)
        {
            Update(parameter.Key.GetResolvedText().NotNull(), parameter.Value);
        }
    }

    public void Add(GameFileWrapper gameFile)
    {
        Add(gameFile, gameFile.Resource);
    }

    public void Add(GameFileWrapper gameFile, CResource resource)
    {
        if (resource is CMaterialTemplate template)
        {
            BaseTemplate = gameFile.FileName;

            foreach (var parameterHandle in template.Parameters[2])
            {
                if (parameterHandle.Chunk is not { } materialParameter)
                {
                    continue;
                }

                Parameters.Add(materialParameter.ParameterName.GetResolvedText()!, materialParameter);

                ExtractResources(gameFile, materialParameter);
            }
        }

        if (resource is CMaterialInstance instance)
        {
            var file = gameFile.GetResource(instance.BaseMaterial);
            if (file != null)
            {
                file.FileName = instance.BaseMaterial.DepotPath.GetResolvedText();
                Add(file);
            }

            foreach (var pair in instance.Values)
            {
                var key = pair.Key.GetResolvedText().NotNull();
                if (!Parameters.ContainsKey(key))
                {
                    // TODO: Add it? Which type?
                    continue;
                }

                Update(key, pair.Value);
                ExtractResources(gameFile, Parameters[key]);
            }
        }
    }

    private void ExtractResources(GameFileWrapper gameFile, CMaterialParameter materialParameter)
    {
        IRedRef? resourceReference = null;

        if (materialParameter is CMaterialParameterCube cube)
        {
            resourceReference = cube.Texture;
        }

        if (materialParameter is CMaterialParameterDynamicTexture dynamicTexture)
        {
            resourceReference = dynamicTexture.Texture;
        }

        if (materialParameter is CMaterialParameterFoliageParameters foliageParameters)
        {
            resourceReference = foliageParameters.FoliageProfile;
        }

        if (materialParameter is CMaterialParameterGradient gradient)
        {
            resourceReference = gradient.Gradient;
        }

        if (materialParameter is CMaterialParameterHairParameters hairParameters)
        {
            resourceReference = hairParameters.HairProfile;
        }

        if (materialParameter is CMaterialParameterMultilayerMask multilayerMask)
        {
            resourceReference = multilayerMask.Mask;
        }

        if (materialParameter is CMaterialParameterMultilayerSetup multilayerSetup)
        {
            resourceReference = multilayerSetup.Setup;
        }

        if (materialParameter is CMaterialParameterSkinParameters skinParameters)
        {
            resourceReference = skinParameters.SkinProfile;
        }

        if (materialParameter is CMaterialParameterTerrainSetup terrainSetup)
        {
            resourceReference = terrainSetup.Setup;
        }

        if (materialParameter is CMaterialParameterTexture texture)
        {
            resourceReference = texture.Texture;
        }

        if (materialParameter is CMaterialParameterTextureArray textureArray)
        {
            resourceReference = textureArray.Texture;
        }

        if (resourceReference != null)
        {
            var embFile = gameFile.GetResource(resourceReference);
            if (embFile != null)
            {
                embFile.FileName = resourceReference.DepotPath.GetResolvedText();
                Resources[resourceReference.DepotPath] = embFile.Resource;
            }
        }
    }

    private void Update(string key, IRedType value)
    {
        if (Parameters[key] is CMaterialParameterTexture texture)
        {
            if (value is not IRedRef val)
            {
                throw new Exception();
            }

            texture.Texture = new CResourceReference<ITexture>(val.DepotPath, val.Flags);

            return;
        }

        if (Parameters[key] is CMaterialParameterColor color)
        {
            if (value is not CColor val)
            {
                throw new Exception();
            }

            color.Color = val;

            return;
        }

        if (Parameters[key] is CMaterialParameterScalar scalar)
        {
            if (value is not CFloat val)
            {
                throw new Exception();
            }

            scalar.Scalar = val;

            return;
        }

        if (Parameters[key] is CMaterialParameterMultilayerMask multilayerMask)
        {
            if (value is not IRedRef val)
            {
                throw new Exception();
            }

            multilayerMask.Mask = new CResourceReference<Multilayer_Mask>(val.DepotPath, val.Flags);

            return;
        }

        if (Parameters[key] is CMaterialParameterMultilayerSetup multilayerSetup)
        {
            if (value is not IRedRef val)
            {
                throw new Exception();
            }

            multilayerSetup.Setup = new CResourceReference<Multilayer_Setup>(val.DepotPath, val.Flags);

            return;
        }

        if (Parameters[key] is CMaterialParameterVector vector)
        {
            if (value is not Vector4 val)
            {
                throw new Exception();
            }

            vector.Vector = val;

            return;
        }

        throw new Exception();
    }

    internal void Assign(MaterialInstance materialInstance)
    {
        materialInstance.Template = BaseTemplate.NotNull();

        foreach (var (key, value) in Parameters)
        {
            materialInstance.Parameters.Add(key, (MaterialParameter)value);
        }
    }

    public static MaterialParameterDictionary Create(GameFileWrapper gameFile, CResource resource)
    {
        var result = new MaterialParameterDictionary();

        result.Add(gameFile, resource);

        return result;
    }
}