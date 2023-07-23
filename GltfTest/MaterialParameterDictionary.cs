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
    public Dictionary<ResourcePath, GameFileWrapper> Resources = new();

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
            }
            foreach (var (path, flags) in gameFile.GetImports())
            {
                var embFile = gameFile.GetResource(path, flags);
                if (embFile != null)
                {
                    Resources[path] = embFile;
                }
            }
        }

        if (resource is CMaterialInstance instance)
        {
            var file = gameFile.GetResource(instance.BaseMaterial);
            if (file != null)
            {
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
            }
            foreach (var (path, flags) in gameFile.GetImports())
            {
                var embFile = gameFile.GetResource(path, flags);
                if (embFile != null)
                {
                    Resources[path] = embFile;
                }
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

        if (Parameters[key] is CMaterialParameterHairParameters hairParameters)
        {
            if (value is not IRedRef val)
            {
                throw new Exception();
            }

            hairParameters.HairProfile = new CResourceReference<CHairProfile>(val.DepotPath, val.Flags);

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