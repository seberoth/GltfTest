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

        var materials = ExtractMaterials(cMesh);

        if (cMesh.BoneNames.Count > 0)
        {
            _skeleton = _modelRoot.CreateLogicalNode();
            _skeleton.Name = "Skeleton";
            //_skeleton.LocalTransform = new AffineTransform(new Quaternion(0F, -0.7071067F, 0F, 0.7071068F));

            var (skin, bones) = ExtractSkeleton(cMesh, rendBlob);
        }

        var meshes = ExtractMeshes(rendBlob, materials);

        _modelRoot.MergeBuffers();

        return _modelRoot;
    }
}