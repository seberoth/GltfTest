using GltfTest.Extras;
using SharpGLTF.Schema2;
using WolvenKit.Common.Conversion;
using WolvenKit.Core.Extensions;
using WolvenKit.Modkit.RED4;
using WolvenKit.RED4.Archive;
using WolvenKit.RED4.Archive.CR2W;
using WolvenKit.RED4.CR2W;
using WolvenKit.RED4.CR2W.JSON;
using WolvenKit.RED4.Types;
using WolvenKit.RED4.Types.Exceptions;

namespace GltfTest;

public partial class GltfConverter
{
    private Dictionary<string, Material> ExtractMaterials(CMesh mesh)
    {
        var result = new Dictionary<string, Material>();

        foreach (var materialEntry in mesh.MaterialEntries)
        {
            var materialName = materialEntry.Name.GetResolvedText()!;

            var gMaterial = _modelRoot.CreateMaterial(materialName);
            var cpMaterial = gMaterial.UseExtension<MaterialInstance>();

            IMaterial? tmp1 = null;
            GameFileWrapper? tmp2 = null;
            if (materialEntry.IsLocalInstance)
            {
                if (mesh.PreloadLocalMaterialInstances is { Count: > 0 })
                {
                    tmp1 = mesh.PreloadLocalMaterialInstances[materialEntry.Index].Chunk;
                }

                if (mesh.LocalMaterialBuffer is { Materials.Count: > 0 })
                {
                    tmp1 = mesh.LocalMaterialBuffer.Materials[materialEntry.Index];
                }
            }
            else
            {
                IRedRef? resRef = null;
                if (mesh.PreloadExternalMaterials.Count > 0)
                {
                    resRef = mesh.PreloadExternalMaterials[materialEntry.Index];
                }

                if (mesh.ExternalMaterials.Count > 0)
                {
                    resRef = mesh.ExternalMaterials[materialEntry.Index];
                }

                if (resRef != null)
                {
                    var materialFile = _file.GetResource(resRef);
                    if (materialFile is { Resource: IMaterial resource })
                    {
                        tmp1 = resource;
                        tmp2 = materialFile;
                    }
                }
            }

            if (tmp1 is CMaterialInstance materialInstance)
            {
                var parameters = MaterialParameterDictionary.Create(tmp2 ?? _file, materialInstance);
                parameters.Assign(cpMaterial);

                foreach (var key in cpMaterial.Parameters.Keys)
                {
                    if (cpMaterial.Parameters[key] is { Type: "Texture" } parameter)
                    {
                        cpMaterial.Parameters[key].Value = new TextureParameter(gMaterial)
                        {
                            Image = GetImage(parameters, key)
                        };
                    }
                }

                foreach (var (_, gameFile) in parameters.Resources)
                {
                    ExtractFile(gameFile);
                }
            }

            result.Add(materialName, gMaterial);
        }

        return result;
    }

    private void ExtractMaterial()
    {

    }

    private Image? GetImage(MaterialParameterDictionary materials, string key)
    {
        var parameter = materials.Parameters[key] as CMaterialParameterTexture;
        if (parameter == null)
        {
            return null;
        }

        if (!materials.Resources.TryGetValue(parameter.Texture.DepotPath, out var gameFile))
        {
            gameFile = _file.GetResource(parameter.Texture.DepotPath, parameter.Texture.Flags);
        }

        if (gameFile == null)
        {
            return null;
        }

        var redImage = RedImage.FromXBM((CBitmapTexture)gameFile.Resource);
        redImage.FlipV();

        var image = _modelRoot.CreateImage();
        image.Content = redImage.SaveToPNGMemory();

        return image;
    }

    private void ExtractFile(GameFileWrapper gameFile)
    {
        var extension = Path.GetExtension(gameFile.FileName);
        switch (extension)
        {
            case ".xbm":
                ExtractXbm(gameFile);
                break;

            case ".mlmask":
                ExtractMlMask(gameFile);
                break;

            case ".hp":
                ExtractHP(gameFile);
                break;

            case ".mlsetup":
                ExtractMlSetup(gameFile);
                break;

            case ".mltemplate":
                ExtractMlTemplate(gameFile);
                break;

            case ".gradient":
                break;

            default:
                break;
        }
    }

    private void ExtractXbm(GameFileWrapper gameFile)
    {
        var destFileName = new FileInfo(Path.Combine(_depotPath, gameFile.FileName.Replace('\\', Path.DirectorySeparatorChar)));
        if (!File.Exists(destFileName.FullName.Replace(".xbm", ".png")))
        {
            if (destFileName.Directory == null)
            {
                return;
            }

            if (!destFileName.Directory.Exists)
            {
                destFileName.Directory.Create();
            }

            var redImage = RedImage.FromXBM((CBitmapTexture)gameFile.Resource);
            redImage.SaveToPNG(destFileName.FullName.Replace(".xbm", ".png"));
        }
    }

    private void ExtractMlMask(GameFileWrapper gameFile)
    {
        var destFileName = new FileInfo(Path.Combine(_depotPath, gameFile.FileName.Replace('\\', Path.DirectorySeparatorChar)));
        if (!File.Exists(destFileName.FullName.Replace(".mlmask", "_0.png")))
        {
            var resource = gameFile.Resource;
            if (resource is not Multilayer_Mask mlmask || mlmask.RenderResourceBlob.RenderResourceBlobPC.Chunk is not rendRenderMultilayerMaskBlobPC blob)
            {
                return;
            }

            if (destFileName.Directory == null)
            {
                return;
            }

            if (!destFileName.Directory.Exists)
            {
                destFileName.Directory.Create();
            }

            var cnt = 0;
            foreach (var img in ModTools.GetRedImages(blob))
            {
                img.SaveToPNG(destFileName.FullName.Replace(".mlmask", $"_{cnt++}.png"));
                img.Dispose();
            }
        }
    }

    private void ExtractHP(GameFileWrapper gameFile)
    {
        var destFileName = new FileInfo(Path.Combine(_depotPath, $"{gameFile.FileName.Replace('\\', Path.DirectorySeparatorChar)}.json"));
        if (!destFileName.Exists)
        {
            if (destFileName.Directory == null)
            {
                return;
            }

            if (!destFileName.Directory.Exists)
            {
                destFileName.Directory.Create();
            }

            var dto = new RedFileDto(new CR2WFile { RootChunk = gameFile.Resource });
            var doc = RedJsonSerializer.Serialize(dto);
            File.WriteAllText(destFileName.FullName, doc);
        }
    }

    private void ExtractMlSetup(GameFileWrapper gameFile)
    {
        var destFileName = new FileInfo(Path.Combine(_depotPath, $"{gameFile.FileName.Replace('\\', Path.DirectorySeparatorChar)}.json"));
        if (!destFileName.Exists)
        {
            if (destFileName.Directory == null)
            {
                return;
            }

            if (!destFileName.Directory.Exists)
            {
                destFileName.Directory.Create();
            }

            var dto = new RedFileDto(new CR2WFile { RootChunk = gameFile.Resource });
            var doc = RedJsonSerializer.Serialize(dto);
            File.WriteAllText(destFileName.FullName, doc);

            foreach (var (resourcePath, flags) in gameFile.GetImports())
            {
                var importGameFile = gameFile.GetResource(resourcePath, flags);
                if (importGameFile != null)
                {
                    ExtractFile(importGameFile);
                }
            }
        }
    }

    private void ExtractMlTemplate(GameFileWrapper gameFile)
    {
        var destFileName = new FileInfo(Path.Combine(_depotPath, $"{gameFile.FileName.Replace('\\', Path.DirectorySeparatorChar)}.json"));
        if (!destFileName.Exists)
        {
            if (destFileName.Directory == null)
            {
                return;
            }

            if (!destFileName.Directory.Exists)
            {
                destFileName.Directory.Create();
            }

            var dto = new RedFileDto(new CR2WFile { RootChunk = gameFile.Resource });
            var doc = RedJsonSerializer.Serialize(dto);
            File.WriteAllText(destFileName.FullName, doc);

            foreach (var (resourcePath, flags) in gameFile.GetImports())
            {
                var importGameFile = gameFile.GetResource(resourcePath, flags);
                if (importGameFile != null)
                {
                    ExtractFile(importGameFile);
                }
            }
        }
    }
}