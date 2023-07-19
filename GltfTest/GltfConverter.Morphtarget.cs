using SharpGLTF.Schema2;
using WolvenKit.RED4.Types;

namespace GltfTest;

public partial class GltfConverter
{
    public ModelRoot ToGltf(MorphTargetMesh morphTargetMesh)
    {
        if (morphTargetMesh.Blob.Chunk is rendRenderMorphTargetMeshBlob rendRenderMorphTargetMeshBlob &&
            rendRenderMorphTargetMeshBlob.BaseBlob.Chunk is rendRenderMeshBlob rendBlob)
        {
            ExtractMeshes(rendBlob);
        }

        _modelRoot.MergeBuffers();

        return _modelRoot;
    }
}