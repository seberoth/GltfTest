using DynamicData.Tests;
using GltfTest.Extras;
using SharpDX.DXGI;
using SharpGLTF.Schema2;
using SharpGLTF.Validation;
using System.Numerics;
using SharpGLTF.IO;
using WolvenKit.RED4.Types;
using static WolvenKit.RED4.Types.Enums;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using System.Runtime.Intrinsics;

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

    public CMesh ToMesh()
    {
        _mesh.RenderResourceBlob = _blob;

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

                            case "SkinParameters":
                                mi.Values.Add(new CKeyValuePair(name, new CResourceReference<CSkinProfile>(resourceReference.ResourcePath)));
                                continue;

                            case "MultilayerMask":
                                mi.Values.Add(new CKeyValuePair(name, new CResourceReference<Multilayer_Mask>(resourceReference.ResourcePath)));
                                continue;

                            case "MultilayerSetup":
                                mi.Values.Add(new CKeyValuePair(name, new CResourceReference<Multilayer_Setup>(resourceReference.ResourcePath)));
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
                        mi.Values.Add(new CKeyValuePair(name, new WolvenKit.RED4.Types.Vector4 { X = vector.X, Y = vector.Y, Z = vector.Z, W = vector.W }));
                        continue;
                    }

                    if (parameter.Value is StructBufferParameter structBuffer)
                    {
                        continue;
                    }

                    throw new Exception();
                }

                _mesh.LocalMaterialBuffer.Materials.Add(mi);
            }
        }

        using var ms = new MemoryStream();
        using var vd = new VertexAttributeWriter(ms);

        var min = new Vector3(Single.MaxValue, Single.MaxValue, Single.MaxValue);
        var max = new Vector3(Single.MinValue, Single.MinValue, Single.MinValue);

        foreach (var mesh in _modelRoot.LogicalMeshes)
        {
            foreach (var primitive in mesh.Primitives)
            {
                var positions = primitive.GetVertexAccessor("POSITION").AsVector3Array();

                foreach (var position in positions)
                {
                    min.X = MathF.Min(min.X, position.X);
                    min.Y = MathF.Min(min.Y, -position.Z);
                    min.Z = MathF.Min(min.Z, position.Y);

                    max.X = MathF.Max(max.X, position.X);
                    max.Y = MathF.Max(max.Y, -position.Z);
                    max.Z = MathF.Max(max.Z, position.Y);
                }
            }
        }

        _mesh.BoundingBox = new Box
        {
            Min = new WolvenKit.RED4.Types.Vector4
            {
                X = min.X,
                Y = min.Y,
                Z = min.Z,
                W = 1F
            },
            Max = new WolvenKit.RED4.Types.Vector4
            {
                X = max.X,
                Y = max.Y,
                Z = max.Z,
                W = 1F
            }
        };

        _blob.Header.QuantizationOffset = new WolvenKit.RED4.Types.Vector4
        {
            X = (max.X + min.X) / 2,
            Y = (max.Y + min.Y) / 2,
            Z = (max.Z + min.Z) / 2,
            W = 0
        };
        
        _blob.Header.QuantizationScale = new WolvenKit.RED4.Types.Vector4
        {
            X = (max.X - min.X) / 2,
            Y = (max.Y - min.Y) / 2,
            Z = (max.Z - min.Z) / 2,
            W = 0
        };

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
                if (!logicalNode.Name.StartsWith("Mesh_lod"))
                {
                    throw new Exception();
                }
                var lod = byte.Parse(logicalNode.Name[8..]);

                _blob.Header.RenderLODs.Add(0);
                _mesh.LodLevelInfo.Add(0);

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

                    var rendChunk = AddPrimitive(primitive, vd);
                    rendChunk.LodMask = lod;

                    _blob.Header.Topology.Add(new rendTopologyData());
                }
            }
            else
            {
                
            }
        }

        _blob.Header.VertexBufferSize = (CUInt32)vd.BaseStream.Position;
        _blob.Header.IndexBufferOffset = (CUInt32)vd.BaseStream.Position;

        var index = 0;
        foreach (var mesh in _modelRoot.LogicalMeshes)
        {
            foreach (var primitive in mesh.Primitives)
            {
                var indices = primitive.GetIndices();

                var renderChunkInfo = _blob.Header.RenderChunkInfos[index];

                renderChunkInfo.ChunkIndices.Pe = GpuWrapApieIndexBufferChunkType.IBCT_IndexUShort;
                renderChunkInfo.ChunkIndices.TeOffset = (CUInt32)(vd.BaseStream.Position - _blob.Header.IndexBufferOffset);

                renderChunkInfo.NumIndices = (uint)indices.Count;

                for (int j = 0; j < indices.Count; j += 3)
                {
                    vd.Write((UInt16)indices[j + 2]);
                    vd.Write((UInt16)indices[j + 1]);
                    vd.Write((UInt16)indices[j + 0]);
                }

                index++;
            }
        }

        _blob.Header.IndexBufferSize = (CUInt32)vd.BaseStream.Position - _blob.Header.IndexBufferOffset;
        _blob.Header.DataProcessing = 1;
        _blob.Header.Version = 20;

        _blob.RenderBuffer = new DataBuffer(ms.ToArray());

        return _mesh;
    }

    private void AddBone(Node logicalNode)
    {
        var boneRig = ZUp(RotY(logicalNode.WorldMatrix));
        Matrix4x4.Invert(boneRig, out var inverseBoneRig);

        _mesh.BoneNames.Add(logicalNode.Name);
        _mesh.BoneRigMatrices.Add(inverseBoneRig);

        _blob.Header.BonePositions.Add(new WolvenKit.RED4.Types.Vector4
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

    private List<string>? GetTargetNames(JsonContent extras)
    {
        try
        {
            return extras.GetNode("targetNames").Deserialize<List<string>>();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private rendChunk AddPrimitive(MeshPrimitive primitive, VertexAttributeWriter vertexAttributeWriter)
    {
        var renderChunkInfo = new rendChunk
        {
            RenderMask = EMeshChunkFlags.MCF_RenderInScene | EMeshChunkFlags.MCF_RenderInShadows,
        };

        var vertexCount = primitive.VertexAccessors["POSITION"].Count;
        if (vertexCount >= ushort.MaxValue)
        {
            throw new Exception();
        }
        renderChunkInfo.NumVertices = (CUInt16)vertexCount;

        var morphTargets = new Dictionary<string, IReadOnlyDictionary<string, Accessor>>();
        var targetNames = GetTargetNames(primitive.LogicalParent.Extras);
        for (int i = 0; i < primitive.MorphTargetsCount; i++)
        {
            var name = targetNames != null ? targetNames[i] : $"target_{i}";
            morphTargets.Add(name, primitive.GetMorphTargetAccessors(i));
        }

        var layoutData = GetLayoutData(primitive.VertexAccessors, morphTargets);

        // Test to replace data types, Quantization is hardcoded for POSITION, need to make sure its "disabled"
        //
        //var layoutData = GetLayoutData2(primitive.VertexAccessors, morphTargets);
        //_blob.Header.QuantizationOffset = new WolvenKit.RED4.Types.Vector4
        //{
        //    X = 0,
        //    Y = 0,
        //    Z = 0,
        //    W = 0
        //};
        //
        //_blob.Header.QuantizationScale = new WolvenKit.RED4.Types.Vector4
        //{
        //    X = 1,
        //    Y = 1,
        //    Z = 1,
        //    W = 0
        //};

        renderChunkInfo.ChunkVertices.VertexLayout.Elements = new CStatic<GpuWrapApiVertexPackingPackingElement>(32);
        renderChunkInfo.ChunkVertices.VertexLayout.SlotStrides = new CStatic<CUInt8>(8);
        for (var j = 0; j < 8; j++)
        {
            renderChunkInfo.ChunkVertices.VertexLayout.SlotStrides.Add(0);
        }

        foreach (var attributeInfo in layoutData)
        {
            renderChunkInfo.ChunkVertices.VertexLayout.Elements.Add(attributeInfo.ElementInfo);
            renderChunkInfo.ChunkVertices.VertexLayout.SlotStrides[attributeInfo.ElementInfo.StreamIndex] += attributeInfo.TargetSize;
            renderChunkInfo.ChunkVertices.VertexLayout.Hash = 0;
        }

        var slotMask = 0;
        for (var j = 0; j < renderChunkInfo.ChunkVertices.VertexLayout.SlotStrides.Count; j++)
        {
            if (renderChunkInfo.ChunkVertices.VertexLayout.SlotStrides[j] > 0)
            {
                slotMask |= (1 << j);
            }
        }
        renderChunkInfo.ChunkVertices.VertexLayout.SlotMask = (uint)slotMask;
        renderChunkInfo.ChunkVertices.ByteOffsets = new CStatic<CUInt32>(5);

        for (int i = 0; i < 5; i++)
        {
            var padding = vertexAttributeWriter.BaseStream.Position % 16;
            if (padding > 0)
            {
                vertexAttributeWriter.Write(new byte[padding]);
            }

            var elements = layoutData.Where(x => x.ElementInfo.StreamIndex == i).ToList();
            if (elements.Count == 0)
            {
                renderChunkInfo.ChunkVertices.ByteOffsets.Add(0);
                continue;
            }

            renderChunkInfo.ChunkVertices.ByteOffsets.Add((CUInt32)vertexAttributeWriter.BaseStream.Position);

            for (int j = 0; j < renderChunkInfo.NumVertices; j++)
            {
                foreach (var attributeInfo in elements)
                {
                    if (attributeInfo.ElementInfo.StreamType == GpuWrapApiVertexPackingEStreamType.ST_Invalid)
                    {
                        break;
                    }

                    attributeInfo.WriteElement(vertexAttributeWriter, j, _blob);
                }
            }
        }

        #region VertexFactory

        var tmpElements = renderChunkInfo.ChunkVertices.VertexLayout.Elements
            .Where(x => x.StreamType == GpuWrapApiVertexPackingEStreamType.ST_PerVertex)
            .ToList();

        renderChunkInfo.VertexFactory = (byte)GetVertexFactory(tmpElements);

        #endregion VertexFactory

        _blob.Header.RenderChunkInfos.Add(renderChunkInfo);

        return renderChunkInfo;
    }

    private EMaterialVertexFactory GetVertexFactory(List<GpuWrapApiVertexPackingPackingElement> elements)
    {
        // TODO: MVF_MeshProcedural, MVF_MeshProxy, MVF_MeshWindowProxy

        if (elements.Count == 1 && elements[0].Usage == GpuWrapApiVertexPackingePackingUsage.PS_Position)
        {
            return EMaterialVertexFactory.MVF_Terrain;
        }

        var isSkinnedSingleBone = elements.Any(x => x.Usage == GpuWrapApiVertexPackingePackingUsage.PS_BoneIndex);
        if (isSkinnedSingleBone)
        {
            return EMaterialVertexFactory.MVF_MeshSkinnedSingleBone;
        }

        var isSpeedTree = elements.Any(x => x.Usage == GpuWrapApiVertexPackingePackingUsage.PS_ExtraData && x.UsageIndex == 2);
        if (isSpeedTree)
        {
            return EMaterialVertexFactory.MVF_MeshSpeedTree;
        }

        var isSkinned = elements.Any(x => x.Usage == GpuWrapApiVertexPackingePackingUsage.PS_SkinIndices);
        var isDestruction = elements.Any(x => x.Usage == GpuWrapApiVertexPackingePackingUsage.PS_DestructionIndices);
        if (isDestruction)
        {
            if (isSkinned)
            {
                return EMaterialVertexFactory.MVF_MeshDestructibleSkinned;
            }
            else
            {
                return EMaterialVertexFactory.MVF_MeshDestructible;
            }
        }

        var isVehicle = elements.Any(x => x.Usage == GpuWrapApiVertexPackingePackingUsage.PS_VehicleDmgPosition);
        var isSkinnedExt = elements.Any(x => x.Usage == GpuWrapApiVertexPackingePackingUsage.PS_SkinIndices && x.UsageIndex == 1);
        var isGarment = elements.Any(x => x.Usage == GpuWrapApiVertexPackingePackingUsage.PS_ExtraData && x.Type == GpuWrapApiVertexPackingePackingType.PT_Float16_4);
        var isLightBlocker = elements.Any(x => x.Usage == GpuWrapApiVertexPackingePackingUsage.PS_LightBlockerIntensity);

        if (isVehicle)
        {
            if (isSkinned)
            {
                return EMaterialVertexFactory.MVF_MeshSkinnedVehicle;
            }

            return EMaterialVertexFactory.MVF_MeshStaticVehicle;
        }
        else
        {
            if (isLightBlocker)
            {
                if (isGarment)
                {
                    if (isSkinnedExt)
                    {
                        return EMaterialVertexFactory.MVF_GarmentMeshExtSkinnedLightBlockers;
                    }

                    if (isSkinned)
                    {
                        return EMaterialVertexFactory.MVF_GarmentMeshSkinnedLightBlockers;
                    }
                }
                else
                {
                    if (isSkinnedExt)
                    {
                        return EMaterialVertexFactory.MVF_MeshExtSkinnedLightBlockers;
                    }

                    if (isSkinned)
                    {
                        return EMaterialVertexFactory.MVF_MeshSkinnedLightBlockers;
                    }
                }
            }
            else
            {
                if (isGarment)
                {
                    if (isSkinnedExt)
                    {
                        return EMaterialVertexFactory.MVF_GarmentMeshExtSkinned;
                    }

                    if (isSkinned)
                    {
                        return EMaterialVertexFactory.MVF_GarmentMeshSkinned;
                    }
                }
                else
                {
                    if (isSkinnedExt)
                    {
                        return EMaterialVertexFactory.MVF_MeshExtSkinned;
                    }

                    if (isSkinned)
                    {
                        return EMaterialVertexFactory.MVF_MeshSkinned;
                    }
                }
            }
        }

        return EMaterialVertexFactory.MVF_MeshStatic;
    }

    private class AttributeInfo
    {
        public GpuWrapApiVertexPackingPackingElement ElementInfo { get; set; }
        public IList<object> DataArray { get; set; }
        public byte TargetSize { get; set; }

        public void WriteElement(VertexAttributeWriter writer, int index, rendRenderMeshBlob rendRenderMeshBlob)
        {
            switch ((GpuWrapApiVertexPackingePackingType)ElementInfo.Type)
            {
                case GpuWrapApiVertexPackingePackingType.PT_Invalid:
                    throw new Exception();
                case GpuWrapApiVertexPackingePackingType.PT_Float1:
                {
                    if (DataArray[index] is float scalar)
                    {
                        writer.Write(scalar);
                        return;
                    }
                    throw new Exception();
                }
                case GpuWrapApiVertexPackingePackingType.PT_Float2:
                    throw new Exception();
                case GpuWrapApiVertexPackingePackingType.PT_Float3:
                    throw new Exception();
                case GpuWrapApiVertexPackingePackingType.PT_Float4:
                {
                    if (DataArray[index] is Vector3 vector3)
                    {
                        writer.WriteFloat4(vector3);
                        return;
                    }
                    throw new Exception();
                }
                case GpuWrapApiVertexPackingePackingType.PT_Float16_2:
                {
                    if (DataArray[index] is Vector2 vector2)
                    {
                        writer.WriteFloat16_2(vector2);
                        return;
                    }
                    throw new Exception();
                }
                case GpuWrapApiVertexPackingePackingType.PT_Float16_4:
                {
                    if (DataArray[index] is Vector3 vector3)
                    {
                        writer.WriteFloat16_4(vector3);
                        return;
                    }
                    throw new Exception();
                }
                case GpuWrapApiVertexPackingePackingType.PT_UShort1:
                    throw new Exception();
                case GpuWrapApiVertexPackingePackingType.PT_UShort2:
                    throw new Exception();
                case GpuWrapApiVertexPackingePackingType.PT_UShort4:
                    throw new Exception();
                case GpuWrapApiVertexPackingePackingType.PT_UShort4N:
                    throw new Exception();
                case GpuWrapApiVertexPackingePackingType.PT_Short1:
                    throw new Exception();
                case GpuWrapApiVertexPackingePackingType.PT_Short2:
                    throw new Exception();
                case GpuWrapApiVertexPackingePackingType.PT_Short4:
                    throw new Exception();
                case GpuWrapApiVertexPackingePackingType.PT_Short4N:
                {
                    if (DataArray[index] is Vector3 vector3)
                    {
                        writer.WriteShort4N(vector3);
                        return;
                    }
                    throw new Exception();
                }
                case GpuWrapApiVertexPackingePackingType.PT_UInt1:
                    throw new Exception();
                case GpuWrapApiVertexPackingePackingType.PT_UInt2:
                    throw new Exception();
                case GpuWrapApiVertexPackingePackingType.PT_UInt3:
                    throw new Exception();
                case GpuWrapApiVertexPackingePackingType.PT_UInt4:
                    throw new Exception();
                case GpuWrapApiVertexPackingePackingType.PT_Int1:
                    throw new Exception();
                case GpuWrapApiVertexPackingePackingType.PT_Int2:
                    throw new Exception();
                case GpuWrapApiVertexPackingePackingType.PT_Int3:
                    throw new Exception();
                case GpuWrapApiVertexPackingePackingType.PT_Int4:
                    throw new Exception();
                case GpuWrapApiVertexPackingePackingType.PT_Color:
                {
                    if (DataArray[index] is Vector4 vector4)
                    {
                        writer.WriteColor(vector4);
                        return;
                    }
                    throw new Exception();
                }
                case GpuWrapApiVertexPackingePackingType.PT_UByte1:
                    throw new Exception();
                case GpuWrapApiVertexPackingePackingType.PT_UByte1F:
                    throw new Exception();
                case GpuWrapApiVertexPackingePackingType.PT_UByte4:
                {
                    if (DataArray[index] is Vector4 vector4)
                    {
                        writer.WriteUByte4(vector4);
                        return;
                    }
                    throw new Exception();
                }
                case GpuWrapApiVertexPackingePackingType.PT_UByte4N:
                {
                    if (DataArray[index] is Vector4 vector4)
                    {
                        writer.WriteUByte4N(vector4);
                        return;
                    }
                    throw new Exception();
                }
                case GpuWrapApiVertexPackingePackingType.PT_Byte4N:
                    throw new Exception();
                case GpuWrapApiVertexPackingePackingType.PT_Dec4:
                {
                    if (DataArray[index] is Vector3 vector3)
                    {
                        writer.WriteDec4(vector3);
                        return;
                    }

                    if (DataArray[index] is Vector4 vector4)
                    {
                        writer.WriteDec4(vector4);
                        return;
                    }
                    throw new Exception();
                }
                case GpuWrapApiVertexPackingePackingType.PT_Index16:
                    throw new Exception();
                case GpuWrapApiVertexPackingePackingType.PT_Index32:
                    throw new Exception();
                case GpuWrapApiVertexPackingePackingType.PT_Max:
                    throw new Exception();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private List<AttributeInfo> GetLayoutData(IReadOnlyDictionary<string, Accessor> vertexAccessors, Dictionary<string, IReadOnlyDictionary<string, Accessor>> morphTargets)
    {
        var list = new List<AttributeInfo>();

        byte streamIndex = 0;
        var hasSkin = false;
        var groupUsed = false;

        if (vertexAccessors.ContainsKey("POSITION"))
        {
            list.Add(new AttributeInfo
            {
                ElementInfo = new GpuWrapApiVertexPackingPackingElement
                {
                    Type = GpuWrapApiVertexPackingePackingType.PT_Short4N,
                    Usage = GpuWrapApiVertexPackingePackingUsage.PS_Position,
                    UsageIndex = 0,
                    StreamIndex = streamIndex,
                    StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerVertex
                },
                DataArray = new List<object>(),
                TargetSize = 8
            });

            foreach (var vector in vertexAccessors["POSITION"].AsVector3Array())
            {
                var x = (vector.X - _blob.Header.QuantizationOffset.X) / _blob.Header.QuantizationScale.X;
                var y = (-vector.Z - _blob.Header.QuantizationOffset.Y) / _blob.Header.QuantizationScale.Y;
                var z = (vector.Y - _blob.Header.QuantizationOffset.Z) / _blob.Header.QuantizationScale.Z;
                
                list[^1].DataArray.Add(new Vector3(x, y, z));
            }

            groupUsed = true;
        }

        // TODO
        if (vertexAccessors.ContainsKey("BoneIndex"))
        {
            hasSkin = true;
            groupUsed = true;
        }

        if (vertexAccessors.ContainsKey("JOINTS_0"))
        {
            list.Add(new AttributeInfo
            {
                ElementInfo = new GpuWrapApiVertexPackingPackingElement
                {
                    Type = GpuWrapApiVertexPackingePackingType.PT_UByte4,
                    Usage = GpuWrapApiVertexPackingePackingUsage.PS_SkinIndices,
                    UsageIndex = 0,
                    StreamIndex = streamIndex,
                    StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerVertex
                },
                DataArray = vertexAccessors["JOINTS_0"].AsVector4Array().Cast<object>().ToList(),
                TargetSize = 4
            });

            hasSkin = true;
            groupUsed = true;
        }

        if (vertexAccessors.ContainsKey("JOINTS_1"))
        {
            list.Add(new AttributeInfo
            {
                ElementInfo = new GpuWrapApiVertexPackingPackingElement
                {
                    Type = GpuWrapApiVertexPackingePackingType.PT_UByte4,
                    Usage = GpuWrapApiVertexPackingePackingUsage.PS_SkinIndices,
                    UsageIndex = 1,
                    StreamIndex = streamIndex,
                    StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerVertex
                },
                DataArray = vertexAccessors["JOINTS_1"].AsVector4Array().Cast<object>().ToList(),
                TargetSize = 4
            });

            hasSkin = true;
            groupUsed = true;
        }

        if (vertexAccessors.ContainsKey("WEIGHTS_0"))
        {
            list.Add(new AttributeInfo
            {
                ElementInfo = new GpuWrapApiVertexPackingPackingElement
                {
                    Type = GpuWrapApiVertexPackingePackingType.PT_UByte4N,
                    Usage = GpuWrapApiVertexPackingePackingUsage.PS_SkinWeights,
                    UsageIndex = 0,
                    StreamIndex = streamIndex,
                    StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerVertex
                },
                DataArray = vertexAccessors["WEIGHTS_0"].AsVector4Array().Cast<object>().ToList(),
                TargetSize = 4
            });

            hasSkin = true;
            groupUsed = true;
        }

        if (vertexAccessors.ContainsKey("WEIGHTS_1"))
        {
            list.Add(new AttributeInfo
            {
                ElementInfo = new GpuWrapApiVertexPackingPackingElement
                {
                    Type = GpuWrapApiVertexPackingePackingType.PT_UByte4N,
                    Usage = GpuWrapApiVertexPackingePackingUsage.PS_SkinWeights,
                    UsageIndex = 1,
                    StreamIndex = streamIndex,
                    StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerVertex
                },
                DataArray = vertexAccessors["WEIGHTS_1"].AsVector4Array().Cast<object>().ToList(),
                TargetSize = 4
            });

            hasSkin = true;
            groupUsed = true;
        }

        if (groupUsed)
        {
            streamIndex++;
            groupUsed = false;
        }

        if (vertexAccessors.ContainsKey("TEXCOORD_0"))
        {
            list.Add(new AttributeInfo
            {
                ElementInfo = new GpuWrapApiVertexPackingPackingElement
                {
                    Type = GpuWrapApiVertexPackingePackingType.PT_Float16_2,
                    Usage = GpuWrapApiVertexPackingePackingUsage.PS_TexCoord,
                    UsageIndex = 0,
                    StreamIndex = streamIndex,
                    StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerVertex
                },
                DataArray = vertexAccessors["TEXCOORD_0"].AsVector2Array().Cast<object>().ToList(),
                TargetSize = 4
            });

            groupUsed = true;
        }

        if (groupUsed)
        {
            streamIndex++;
            groupUsed = false;
        }

        if (vertexAccessors.ContainsKey("NORMAL"))
        {
            list.Add(new AttributeInfo
            {
                ElementInfo = new GpuWrapApiVertexPackingPackingElement
                {
                    Type = GpuWrapApiVertexPackingePackingType.PT_Dec4,
                    Usage = GpuWrapApiVertexPackingePackingUsage.PS_Normal,
                    UsageIndex = 0,
                    StreamIndex = streamIndex,
                    StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerVertex
                },
                DataArray = new List<object>(),
                TargetSize = 4
            });

            foreach (var vector in vertexAccessors["NORMAL"].AsVector3Array())
            {
                list[^1].DataArray.Add(new Vector3(vector.X, -vector.Z, vector.Y));
            }

            groupUsed = true;
        }

        if (vertexAccessors.ContainsKey("TANGENT"))
        {
            list.Add(new AttributeInfo
            {
                ElementInfo = new GpuWrapApiVertexPackingPackingElement
                {
                    Type = GpuWrapApiVertexPackingePackingType.PT_Dec4,
                    Usage = GpuWrapApiVertexPackingePackingUsage.PS_Tangent,
                    UsageIndex = 0,
                    StreamIndex = streamIndex,
                    StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerVertex
                },
                DataArray = new List<object>(),
                TargetSize = 4
            });

            foreach (var vector in vertexAccessors["TANGENT"].AsVector4Array())
            {
                list[^1].DataArray.Add(new Vector4(vector.X, -vector.Z, vector.Y, vector.W));
            }

            groupUsed = true;
        }

        if (groupUsed)
        {
            streamIndex++;
            groupUsed = false;
        }

        if (vertexAccessors.ContainsKey("COLOR_0"))
        {
            list.Add(new AttributeInfo
            {
                ElementInfo = new GpuWrapApiVertexPackingPackingElement
                {
                    Type = GpuWrapApiVertexPackingePackingType.PT_Color,
                    Usage = GpuWrapApiVertexPackingePackingUsage.PS_Color,
                    UsageIndex = 0,
                    StreamIndex = streamIndex,
                    StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerVertex
                },
                DataArray = vertexAccessors["COLOR_0"].AsVector4Array().Cast<object>().ToList(),
                TargetSize = 4
            });

            groupUsed = true;
        }

        if (vertexAccessors.ContainsKey("TEXCOORD_1"))
        {
            list.Add(new AttributeInfo
            {
                ElementInfo = new GpuWrapApiVertexPackingPackingElement
                {
                    Type = GpuWrapApiVertexPackingePackingType.PT_Float16_2,
                    Usage = GpuWrapApiVertexPackingePackingUsage.PS_TexCoord,
                    UsageIndex = 1,
                    StreamIndex = streamIndex,
                    StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerVertex
                },
                DataArray = vertexAccessors["TEXCOORD_1"].AsVector2Array().Cast<object>().ToList(),
                TargetSize = 4
            });

            groupUsed = true;
        }

        if (groupUsed)
        {
            streamIndex++;
            groupUsed = false;
        }

        // TODO
        if (vertexAccessors.ContainsKey("DestructionIndices"))
        {
            groupUsed = true;
        }

        // TODO
        if (vertexAccessors.ContainsKey("MultilayerPaint"))
        {
            groupUsed = true;
        }

        if (morphTargets.ContainsKey("GarmentSupport"))
        {
            list.Add(new AttributeInfo
            {
                ElementInfo = new GpuWrapApiVertexPackingPackingElement
                {
                    Type = GpuWrapApiVertexPackingePackingType.PT_Float16_4,
                    Usage = GpuWrapApiVertexPackingePackingUsage.PS_ExtraData,
                    UsageIndex = 0,
                    StreamIndex = 0,
                    StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerVertex
                },
                DataArray = morphTargets["GarmentSupport"]["POSITION"].AsVector3Array().Cast<object>().ToList(),
                TargetSize = 8
            });

            groupUsed = true;
        }

        // TODO
        if (vertexAccessors.ContainsKey("ExtraData_0"))
        {
            groupUsed = true;
        }

        // TODO
        if (vertexAccessors.ContainsKey("ExtraData_1"))
        {
            groupUsed = true;
        }

        // TODO
        if (vertexAccessors.ContainsKey("ExtraData_2"))
        {
            groupUsed = true;
        }

        if (groupUsed)
        {
            groupUsed = false;
        }

        list.Add(new AttributeInfo
        {
            ElementInfo = new GpuWrapApiVertexPackingPackingElement
            {
                Type = GpuWrapApiVertexPackingePackingType.PT_Float4,
                Usage = GpuWrapApiVertexPackingePackingUsage.PS_InstanceTransform,
                UsageIndex = 0,
                StreamIndex = 7,
                StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerInstance
            },
            TargetSize = 16
        });

        list.Add(new AttributeInfo
        {
            ElementInfo = new GpuWrapApiVertexPackingPackingElement
            {
                Type = GpuWrapApiVertexPackingePackingType.PT_Float4,
                Usage = GpuWrapApiVertexPackingePackingUsage.PS_InstanceTransform,
                UsageIndex = 1,
                StreamIndex = 7,
                StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerInstance
            },
            TargetSize = 16
        });

        list.Add(new AttributeInfo
        {
            ElementInfo = new GpuWrapApiVertexPackingPackingElement
            {
                Type = GpuWrapApiVertexPackingePackingType.PT_Float4,
                Usage = GpuWrapApiVertexPackingePackingUsage.PS_InstanceTransform,
                UsageIndex = 2,
                StreamIndex = 7,
                StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerInstance
            },
            TargetSize = 16
        });

        if (hasSkin)
        {
            list.Add(new AttributeInfo
            {
                ElementInfo = new GpuWrapApiVertexPackingPackingElement
                {
                    Type = GpuWrapApiVertexPackingePackingType.PT_UInt4,
                    Usage = GpuWrapApiVertexPackingePackingUsage.PS_InstanceSkinningData,
                    UsageIndex = 0,
                    StreamIndex = 7,
                    StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerInstance
                },
                TargetSize = 16
            });
        }

        if (groupUsed)
        {
            groupUsed = false;
        }

        // TODO
        if (vertexAccessors.ContainsKey("VehicleDmgNormal_0"))
        {
            groupUsed = true;
        }

        // TODO
        if (vertexAccessors.ContainsKey("VehicleDmgNormal_1"))
        {
            groupUsed = true;
        }

        // TODO
        if (vertexAccessors.ContainsKey("VehicleDmgPosition_0"))
        {
            groupUsed = true;
        }

        // TODO
        if (vertexAccessors.ContainsKey("VehicleDmgPosition_1"))
        {
            groupUsed = true;
        }

        if (vertexAccessors.ContainsKey("_LIGHTBLOCKERINTENSITY"))
        {
            list.Add(new AttributeInfo
            {
                ElementInfo = new GpuWrapApiVertexPackingPackingElement
                {
                    Type = GpuWrapApiVertexPackingePackingType.PT_Float1,
                    Usage = GpuWrapApiVertexPackingePackingUsage.PS_LightBlockerIntensity,
                    UsageIndex = 0,
                    StreamIndex = streamIndex,
                    StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerVertex
                },
                DataArray = vertexAccessors["_LIGHTBLOCKERINTENSITY"].AsScalarArray().Cast<object>().ToList(),
                TargetSize = 4
            });

            groupUsed = true;
        }

        list.Add(new AttributeInfo
        {
            ElementInfo = new GpuWrapApiVertexPackingPackingElement
            {
                Type = GpuWrapApiVertexPackingePackingType.PT_Invalid,
                Usage = GpuWrapApiVertexPackingePackingUsage.PS_Invalid,
                UsageIndex = 0,
                StreamType = GpuWrapApiVertexPackingEStreamType.ST_Invalid
            }
        });

        return list;
    }

    private List<AttributeInfo> GetLayoutData2(IReadOnlyDictionary<string, Accessor> vertexAccessors, Dictionary<string, IReadOnlyDictionary<string, Accessor>> morphTargets)
    {
        var list = new List<AttributeInfo>();

        byte streamIndex = 0;
        var hasSkin = false;
        var groupUsed = false;

        if (vertexAccessors.ContainsKey("POSITION"))
        {
            list.Add(new AttributeInfo
            {
                ElementInfo = new GpuWrapApiVertexPackingPackingElement
                {
                    Type = GpuWrapApiVertexPackingePackingType.PT_Float4,
                    Usage = GpuWrapApiVertexPackingePackingUsage.PS_Position,
                    UsageIndex = 0,
                    StreamIndex = streamIndex,
                    StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerVertex
                },
                DataArray = new List<object>(),
                TargetSize = 16
            });

            foreach (var vector in vertexAccessors["POSITION"].AsVector3Array())
            {
                //var x = (vector.X - _blob.Header.QuantizationOffset.X) / _blob.Header.QuantizationScale.X;
                //var y = (-vector.Z - _blob.Header.QuantizationOffset.Y) / _blob.Header.QuantizationScale.Y;
                //var z = (vector.Y - _blob.Header.QuantizationOffset.Z) / _blob.Header.QuantizationScale.Z;

                list[^1].DataArray.Add(new Vector3(vector.X, -vector.Z, vector.Y));
            }

            groupUsed = true;
        }

        // TODO
        if (vertexAccessors.ContainsKey("BoneIndex"))
        {
            hasSkin = true;
            groupUsed = true;
        }

        if (vertexAccessors.ContainsKey("JOINTS_0"))
        {
            list.Add(new AttributeInfo
            {
                ElementInfo = new GpuWrapApiVertexPackingPackingElement
                {
                    Type = GpuWrapApiVertexPackingePackingType.PT_UByte4,
                    Usage = GpuWrapApiVertexPackingePackingUsage.PS_SkinIndices,
                    UsageIndex = 0,
                    StreamIndex = streamIndex,
                    StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerVertex
                },
                DataArray = vertexAccessors["JOINTS_0"].AsVector4Array().Cast<object>().ToList(),
                TargetSize = 4
            });

            hasSkin = true;
            groupUsed = true;
        }

        if (vertexAccessors.ContainsKey("JOINTS_1"))
        {
            list.Add(new AttributeInfo
            {
                ElementInfo = new GpuWrapApiVertexPackingPackingElement
                {
                    Type = GpuWrapApiVertexPackingePackingType.PT_UByte4,
                    Usage = GpuWrapApiVertexPackingePackingUsage.PS_SkinIndices,
                    UsageIndex = 1,
                    StreamIndex = streamIndex,
                    StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerVertex
                },
                DataArray = vertexAccessors["JOINTS_1"].AsVector4Array().Cast<object>().ToList(),
                TargetSize = 4
            });

            hasSkin = true;
            groupUsed = true;
        }

        if (vertexAccessors.ContainsKey("WEIGHTS_0"))
        {
            list.Add(new AttributeInfo
            {
                ElementInfo = new GpuWrapApiVertexPackingPackingElement
                {
                    Type = GpuWrapApiVertexPackingePackingType.PT_UByte4N,
                    Usage = GpuWrapApiVertexPackingePackingUsage.PS_SkinWeights,
                    UsageIndex = 0,
                    StreamIndex = streamIndex,
                    StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerVertex
                },
                DataArray = vertexAccessors["WEIGHTS_0"].AsVector4Array().Cast<object>().ToList(),
                TargetSize = 4
            });

            hasSkin = true;
            groupUsed = true;
        }

        if (vertexAccessors.ContainsKey("WEIGHTS_1"))
        {
            list.Add(new AttributeInfo
            {
                ElementInfo = new GpuWrapApiVertexPackingPackingElement
                {
                    Type = GpuWrapApiVertexPackingePackingType.PT_UByte4N,
                    Usage = GpuWrapApiVertexPackingePackingUsage.PS_SkinWeights,
                    UsageIndex = 1,
                    StreamIndex = streamIndex,
                    StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerVertex
                },
                DataArray = vertexAccessors["WEIGHTS_1"].AsVector4Array().Cast<object>().ToList(),
                TargetSize = 4
            });

            hasSkin = true;
            groupUsed = true;
        }

        if (groupUsed)
        {
            streamIndex++;
            groupUsed = false;
        }

        if (vertexAccessors.ContainsKey("TEXCOORD_0"))
        {
            list.Add(new AttributeInfo
            {
                ElementInfo = new GpuWrapApiVertexPackingPackingElement
                {
                    Type = GpuWrapApiVertexPackingePackingType.PT_Float16_2,
                    Usage = GpuWrapApiVertexPackingePackingUsage.PS_TexCoord,
                    UsageIndex = 0,
                    StreamIndex = streamIndex,
                    StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerVertex
                },
                DataArray = vertexAccessors["TEXCOORD_0"].AsVector2Array().Cast<object>().ToList(),
                TargetSize = 4
            });

            groupUsed = true;
        }

        if (groupUsed)
        {
            streamIndex++;
            groupUsed = false;
        }

        if (vertexAccessors.ContainsKey("NORMAL"))
        {
            list.Add(new AttributeInfo
            {
                ElementInfo = new GpuWrapApiVertexPackingPackingElement
                {
                    Type = GpuWrapApiVertexPackingePackingType.PT_Dec4,
                    Usage = GpuWrapApiVertexPackingePackingUsage.PS_Normal,
                    UsageIndex = 0,
                    StreamIndex = streamIndex,
                    StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerVertex
                },
                DataArray = new List<object>(),
                TargetSize = 4
            });

            foreach (var vector in vertexAccessors["NORMAL"].AsVector3Array())
            {
                list[^1].DataArray.Add(new Vector3(vector.X, -vector.Z, vector.Y));
            }

            groupUsed = true;
        }

        if (vertexAccessors.ContainsKey("TANGENT"))
        {
            list.Add(new AttributeInfo
            {
                ElementInfo = new GpuWrapApiVertexPackingPackingElement
                {
                    Type = GpuWrapApiVertexPackingePackingType.PT_Dec4,
                    Usage = GpuWrapApiVertexPackingePackingUsage.PS_Tangent,
                    UsageIndex = 0,
                    StreamIndex = streamIndex,
                    StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerVertex
                },
                DataArray = new List<object>(),
                TargetSize = 4
            });

            foreach (var vector in vertexAccessors["TANGENT"].AsVector4Array())
            {
                list[^1].DataArray.Add(new Vector4(vector.X, -vector.Z, vector.Y, vector.W));
            }

            groupUsed = true;
        }

        if (groupUsed)
        {
            streamIndex++;
            groupUsed = false;
        }

        if (vertexAccessors.ContainsKey("COLOR_0"))
        {
            list.Add(new AttributeInfo
            {
                ElementInfo = new GpuWrapApiVertexPackingPackingElement
                {
                    Type = GpuWrapApiVertexPackingePackingType.PT_Color,
                    Usage = GpuWrapApiVertexPackingePackingUsage.PS_Color,
                    UsageIndex = 0,
                    StreamIndex = streamIndex,
                    StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerVertex
                },
                DataArray = vertexAccessors["COLOR_0"].AsVector4Array().Cast<object>().ToList(),
                TargetSize = 4
            });

            groupUsed = true;
        }

        if (vertexAccessors.ContainsKey("TEXCOORD_1"))
        {
            list.Add(new AttributeInfo
            {
                ElementInfo = new GpuWrapApiVertexPackingPackingElement
                {
                    Type = GpuWrapApiVertexPackingePackingType.PT_Float16_2,
                    Usage = GpuWrapApiVertexPackingePackingUsage.PS_TexCoord,
                    UsageIndex = 1,
                    StreamIndex = streamIndex,
                    StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerVertex
                },
                DataArray = vertexAccessors["TEXCOORD_1"].AsVector2Array().Cast<object>().ToList(),
                TargetSize = 4
            });

            groupUsed = true;
        }

        if (groupUsed)
        {
            streamIndex++;
            groupUsed = false;
        }

        // TODO
        if (vertexAccessors.ContainsKey("DestructionIndices"))
        {
            groupUsed = true;
        }

        // TODO
        if (vertexAccessors.ContainsKey("MultilayerPaint"))
        {
            groupUsed = true;
        }

        if (morphTargets.ContainsKey("GarmentSupport"))
        {
            list.Add(new AttributeInfo
            {
                ElementInfo = new GpuWrapApiVertexPackingPackingElement
                {
                    Type = GpuWrapApiVertexPackingePackingType.PT_Float16_4,
                    Usage = GpuWrapApiVertexPackingePackingUsage.PS_ExtraData,
                    UsageIndex = 0,
                    StreamIndex = 0,
                    StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerVertex
                },
                DataArray = morphTargets["GarmentSupport"]["POSITION"].AsVector3Array().Cast<object>().ToList(),
                TargetSize = 8
            });

            groupUsed = true;
        }

        // TODO
        if (vertexAccessors.ContainsKey("ExtraData_0"))
        {
            groupUsed = true;
        }

        // TODO
        if (vertexAccessors.ContainsKey("ExtraData_1"))
        {
            groupUsed = true;
        }

        // TODO
        if (vertexAccessors.ContainsKey("ExtraData_2"))
        {
            groupUsed = true;
        }

        if (groupUsed)
        {
            groupUsed = false;
        }

        list.Add(new AttributeInfo
        {
            ElementInfo = new GpuWrapApiVertexPackingPackingElement
            {
                Type = GpuWrapApiVertexPackingePackingType.PT_Float4,
                Usage = GpuWrapApiVertexPackingePackingUsage.PS_InstanceTransform,
                UsageIndex = 0,
                StreamIndex = 7,
                StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerInstance
            },
            TargetSize = 16
        });

        list.Add(new AttributeInfo
        {
            ElementInfo = new GpuWrapApiVertexPackingPackingElement
            {
                Type = GpuWrapApiVertexPackingePackingType.PT_Float4,
                Usage = GpuWrapApiVertexPackingePackingUsage.PS_InstanceTransform,
                UsageIndex = 1,
                StreamIndex = 7,
                StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerInstance
            },
            TargetSize = 16
        });

        list.Add(new AttributeInfo
        {
            ElementInfo = new GpuWrapApiVertexPackingPackingElement
            {
                Type = GpuWrapApiVertexPackingePackingType.PT_Float4,
                Usage = GpuWrapApiVertexPackingePackingUsage.PS_InstanceTransform,
                UsageIndex = 2,
                StreamIndex = 7,
                StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerInstance
            },
            TargetSize = 16
        });

        if (hasSkin)
        {
            list.Add(new AttributeInfo
            {
                ElementInfo = new GpuWrapApiVertexPackingPackingElement
                {
                    Type = GpuWrapApiVertexPackingePackingType.PT_UInt4,
                    Usage = GpuWrapApiVertexPackingePackingUsage.PS_InstanceSkinningData,
                    UsageIndex = 0,
                    StreamIndex = 7,
                    StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerInstance
                },
                TargetSize = 16
            });
        }

        if (groupUsed)
        {
            groupUsed = false;
        }

        // TODO
        if (vertexAccessors.ContainsKey("VehicleDmgNormal_0"))
        {
            groupUsed = true;
        }

        // TODO
        if (vertexAccessors.ContainsKey("VehicleDmgNormal_1"))
        {
            groupUsed = true;
        }

        // TODO
        if (vertexAccessors.ContainsKey("VehicleDmgPosition_0"))
        {
            groupUsed = true;
        }

        // TODO
        if (vertexAccessors.ContainsKey("VehicleDmgPosition_1"))
        {
            groupUsed = true;
        }

        if (vertexAccessors.ContainsKey("_LIGHTBLOCKERINTENSITY"))
        {
            list.Add(new AttributeInfo
            {
                ElementInfo = new GpuWrapApiVertexPackingPackingElement
                {
                    Type = GpuWrapApiVertexPackingePackingType.PT_Float1,
                    Usage = GpuWrapApiVertexPackingePackingUsage.PS_LightBlockerIntensity,
                    UsageIndex = 0,
                    StreamIndex = streamIndex,
                    StreamType = GpuWrapApiVertexPackingEStreamType.ST_PerVertex
                },
                DataArray = vertexAccessors["_LIGHTBLOCKERINTENSITY"].AsScalarArray().Cast<object>().ToList(),
                TargetSize = 4
            });

            groupUsed = true;
        }

        list.Add(new AttributeInfo
        {
            ElementInfo = new GpuWrapApiVertexPackingPackingElement
            {
                Type = GpuWrapApiVertexPackingePackingType.PT_Invalid,
                Usage = GpuWrapApiVertexPackingePackingUsage.PS_Invalid,
                UsageIndex = 0,
                StreamType = GpuWrapApiVertexPackingEStreamType.ST_Invalid
            }
        });

        return list;
    }
}