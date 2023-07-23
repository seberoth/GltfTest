using GltfTest.Extras;
using SharpDX.Win32;
using SharpGLTF.Schema2;
using WolvenKit.RED4.Types;

namespace GltfTest;

public partial class GltfConverter
{
    public ModelRoot? ToGltf(CMesh cMesh)
    {
        if (cMesh is not { RenderResourceBlob.Chunk: rendRenderMeshBlob rendBlob })
        {
            return null;
        }

        _modelRoot.UseScene(0).Name = "Scene";
        _modelRoot.DefaultScene = _modelRoot.UseScene(0);

        var materials = ExtractMaterials(cMesh);

        var variantList = new List<VariantsRootEntry>();
        foreach (var appearanceHandle in cMesh.Appearances)
        {
            if (appearanceHandle.Chunk == null)
            {
                continue;
            }

            var entry = new VariantsRootEntry { Name = appearanceHandle.Chunk.Name.GetResolvedText()! };
            foreach (var chunkMaterial in appearanceHandle.Chunk.ChunkMaterials)
            {
                entry.Materials.Add(chunkMaterial.GetResolvedText()!);
            }
            variantList.Add(entry);
        }

        if (variantList.Count == 0)
        {
            throw new Exception();
        }

        if (cMesh.Appearances.Count > 1)
        {
            _modelRoot.UseExtension<VariantsRootExtension>().Variants = variantList;
        }

        if (cMesh.BoneNames.Count > 0)
        {
            _skeleton = _modelRoot.UseScene(0).CreateNode();
            _skeleton.Name = "Skeleton";
            //_skeleton.LocalTransform = new AffineTransform(new Quaternion(0F, -0.7071067F, 0F, 0.7071068F));

            var (skin, bones) = ExtractSkeleton(cMesh, rendBlob);
        }

        var meshes = ExtractMeshes(rendBlob, variantList, materials);

        _modelRoot.MergeBuffers();

        return _modelRoot;
    }
}