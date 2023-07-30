using GltfTest.Extras;
using SharpGLTF.Schema2;
using SharpGLTF.Validation;
using System.Numerics;
using WolvenKit.RED4.Types;
using Vector4 = WolvenKit.RED4.Types.Vector4;

namespace GltfTest;

public class GltfImporter
{
    private readonly ModelRoot _modelRoot;
    private readonly CMesh _mesh = new CMesh();
    private readonly rendRenderMeshBlob _blob = new rendRenderMeshBlob();

    public GltfImporter(string filePath)
    {
        _modelRoot = ModelRoot.Load(filePath, new ReadSettings { Validation = ValidationMode.Strict });
    }

    public void ToMesh()
    {
        var variants = _modelRoot.GetExtension<VariantsRootExtension>();
        if (variants != null)
        {
            foreach (var entry in variants.Variants)
            {
                _mesh.Appearances.Add(new meshMeshAppearance { Name = entry.Name });
            }
        }

        for (ushort i = 0; i < _modelRoot.LogicalMaterials.Count; i++)
        {
            _mesh.MaterialEntries.Add(new CMeshMaterialEntry
            {
                Index = i,
                IsLocalInstance = true,
                Name = _modelRoot.LogicalMaterials[i].Name
            });

            var materialInstance = _modelRoot.LogicalMaterials[i].GetExtension<MaterialInstance>();
            if (materialInstance != null)
            {
                if (_mesh.LocalMaterialBuffer.Materials == null)
                {
                    _mesh.LocalMaterialBuffer.Materials = new CArray<IMaterial>();
                }

                var mi = new CMaterialInstance();
                mi.BaseMaterial = new CResourceReference<IMaterial>(materialInstance.Template);

                foreach (var (name, parameter) in materialInstance.Parameters)
                {
                    if (parameter.Value is ResourceReferenceParameter resourceReference)
                    {
                        switch (parameter.Type)
                        {
                            case "Texture":
                                mi.Values.Add(new CKeyValuePair(name, new CResourceReference<IMaterial>(resourceReference.ResourcePath)));
                                continue;

                            case "HairParameters":
                                mi.Values.Add(new CKeyValuePair(name, new CResourceReference<CHairProfile>(resourceReference.ResourcePath)));
                                continue;

                            default:
                                break;
                        }
                    }

                    if (parameter.Value is ScalarParameter scalar)
                    {
                        mi.Values.Add(new CKeyValuePair(name, (CFloat)scalar.Scalar));
                        continue;
                    }

                    if (parameter.Value is ColorParameter color)
                    {
                        mi.Values.Add(new CKeyValuePair(name, new CColor { Red = color.Red, Green = color.Green, Blue = color.Blue, Alpha = color.Alpha }));
                        continue;
                    }

                    if (parameter.Value is VectorParameter vector)
                    {
                        mi.Values.Add(new CKeyValuePair(name, new Vector4 { X = vector.X, Y = vector.Y, Z = vector.Z, W = vector.W }));
                        continue;
                    }

                    throw new Exception();
                }

                _mesh.LocalMaterialBuffer.Materials.Add(mi);
            }
        }

        foreach (var logicalNode in _modelRoot.LogicalNodes)
        {
            if (logicalNode.IsSkinSkeleton)
            {
                continue;
            }

            if (logicalNode.IsSkinJoint)
            {
                AddBone(logicalNode);
            }
            else if (logicalNode.Mesh != null)
            {
                foreach (var primitive in logicalNode.Mesh.Primitives)
                {
                    var variant = primitive.GetExtension<VariantsPrimitiveExtension>();
                    if (variant != null)
                    {
                        foreach (var mapping in variant.Mappings)
                        {
                            var material = _modelRoot.LogicalMaterials[mapping.Material];
                            foreach (var mappingVariant in mapping.Variants)
                            {
                                _mesh.Appearances[mappingVariant].Chunk!.ChunkMaterials.Add(material.Name);
                            }
                        }
                    }


                }
            }
            else
            {
                
            }
        }
    }

    private void AddBone(Node logicalNode)
    {
        var boneRig = ZUp(RotY(logicalNode.WorldMatrix));
        Matrix4x4.Invert(boneRig, out var inverseBoneRig);

        _mesh.BoneNames.Add(logicalNode.Name);
        _mesh.BoneRigMatrices.Add(inverseBoneRig);

        _blob.Header.BonePositions.Add(new Vector4
        {
            X = logicalNode.LocalTransform.Translation.X,
            Y = -logicalNode.LocalTransform.Translation.Z,
            Z = logicalNode.LocalTransform.Translation.Y,
            W = 1F
        });

        if (!logicalNode.Extras.TryGetValue<float>(out var epsilon, "epsilon"))
        {
            throw new Exception();
        }
        _mesh.BoneVertexEpsilons.Add(epsilon);

        if (!logicalNode.Extras.TryGetValue<byte>(out var lod, "lod"))
        {
            throw new Exception();
        }
        _mesh.LodBoneMask.Add(lod);
    }

    private Matrix4x4 RotY(Matrix4x4 src)
    {
        var axisBaseChange = new Matrix4x4(
            0.0F, 0.0F, -1.0F, 0.0F,
            0.0F, 1.0F, 0.0F, 0.0F,
            1.0F, 0.0F, 0.0F, 0.0F,
            0.0F, 0.0F, 0.0F, 1.0F);

        return Matrix4x4.Multiply(axisBaseChange, src);
    }

    private Matrix4x4 ZUp(Matrix4x4 src)
    {
        return src with
        {
            M12 = -src.M13,
            M13 = src.M12,

            M22 = -src.M23,
            M23 = src.M22,

            M32 = -src.M33,
            M33 = src.M32,

            M42 = -src.M43,
            M43 = src.M42,
            M44 = 1
        };
    }
}