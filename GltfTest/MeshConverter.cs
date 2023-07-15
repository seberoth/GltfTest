using SharpGLTF.Schema2;
using SharpGLTF.Validation;
using WolvenKit.RED4.Archive.CR2W;
using WolvenKit.RED4.Types;
using static WolvenKit.RED4.Types.Enums;
using System.Numerics;
using SharpGLTF.Transforms;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;
using SharpGLTF.Memory;

namespace GltfTest;

public class MeshConverter
{
    private ModelRoot _modelRoot = ModelRoot.CreateModel();
    private Node _skeleton;

    public void ToGltf(CR2WFile cr2w, string filePath)
    {
        if (cr2w.RootChunk is not CMesh { RenderResourceBlob.Chunk: rendRenderMeshBlob rendBlob } cMesh)
        {
            return;
        }

        var t = ExtensionsFactory.SupportedExtensions;

        var materials = ExtractMaterials(cMesh);

        if (cMesh.BoneNames.Count > 0)
        {
            _skeleton = _modelRoot.CreateLogicalNode();
            _skeleton.Name = "Skeleton";

            var (skin, bones) = ExtractSkeleton(cMesh, rendBlob);
        }

        var meshes = ExtractMeshes(rendBlob, materials);

        _modelRoot.MergeBuffers();
        _modelRoot.SaveGLB(filePath, new WriteSettings { Validation = ValidationMode.Strict });
    }

    private List<Material> ExtractMaterials(CMesh mesh)
    {
        var result = new List<Material>();
        var dict = new Dictionary<string, uint>();
        foreach (var materialName in mesh.Appearances[0].Chunk!.ChunkMaterials)
        {
            var materialNameStr = materialName.GetResolvedText()!;
            if (!dict.ContainsKey(materialNameStr))
            {
                dict.Add(materialNameStr, 1);
            }
            else
            {
                materialNameStr += $".{dict[materialNameStr]++:D3}";
            }

            result.Add(_modelRoot.CreateMaterial(materialNameStr));
        }

        return result;
    }

    private (Skin skin, List<Node> bones) ExtractSkeleton(CMesh mesh, rendRenderMeshBlob rendRenderMeshBlob)
    {
        var skin = _modelRoot.CreateSkin("Skeleton");

        var offset = rendRenderMeshBlob.Header.QuantizationOffset;
        var scale = rendRenderMeshBlob.Header.QuantizationScale;

        var bones = new List<Node>();
        for (int i = 0; i < mesh.BoneNames.Count; i++)
        {
            var boneNode = _skeleton.CreateNode(mesh.BoneNames[i]);

            var boneRig = mesh.BoneRigMatrices[i];
            

            var localMatrix = new Matrix4x4();

            localMatrix.M11 = boneRig.X.X;
            localMatrix.M12 = boneRig.Z.X;
            localMatrix.M13 = -boneRig.Y.X;
            localMatrix.M14 = 0;

            localMatrix.M21 = boneRig.X.Y;
            localMatrix.M22 = boneRig.Z.Y;
            localMatrix.M23 = -boneRig.Y.Y;
            localMatrix.M24 = 0;

            localMatrix.M31 = boneRig.X.Z;
            localMatrix.M32 = boneRig.Z.Z;
            localMatrix.M33 = -boneRig.Y.Z;
            localMatrix.M34 = 0;

            localMatrix.M41 = boneRig.W.X;
            localMatrix.M42 = boneRig.W.Y;
            localMatrix.M43 = boneRig.W.Z;
            localMatrix.M44 = boneRig.W.W;

            var tmp = (AffineTransform)localMatrix;

            boneNode.LocalMatrix = localMatrix;

            bones.Add(boneNode);
        }

        skin.BindJoints(bones.ToArray());

        return (skin, bones);
    }

    private enum ElementType
    {
        Unknown,
        Todo,
        Main,
        VehicleDamage
    }

    private class VertexElement
    {
        private readonly GpuWrapApiVertexPackingPackingElement _element;
        
        private readonly MemoryStream _stream = new();
        private BinaryWriter _writer;

        public ElementType ElementType { get; private set; } = ElementType.Unknown;
        public string AttributeKey { get; private set; }

        public AttributeFormat DstFormat { get; private set; }

        public byte DataSize { get; private set; }

        public BinaryWriter Writer { get; }

        public VertexElement(GpuWrapApiVertexPackingPackingElement element)
        {
            _element = element;
            _writer = new BinaryWriter(_stream);

            GetInfo();
        }

        private void GetInfo()
        {
            switch ((GpuWrapApiVertexPackingePackingUsage)_element.Usage)
            {
                case GpuWrapApiVertexPackingePackingUsage.PS_Position:
                    ElementType = ElementType.Main;
                    AttributeKey = "POSITION";

                    DstFormat = new(DimensionType.VEC3, EncodingType.FLOAT, false);
                    break;
                case GpuWrapApiVertexPackingePackingUsage.PS_Normal:
                    ElementType = ElementType.Main;
                    AttributeKey = "NORMAL";

                    DstFormat = new(DimensionType.VEC3, EncodingType.FLOAT, false);
                    break;
                case GpuWrapApiVertexPackingePackingUsage.PS_Tangent:
                    ElementType = ElementType.Main;
                    AttributeKey = "TANGENT";

                    DstFormat = new(DimensionType.VEC4, EncodingType.FLOAT, false);
                    break;
                case GpuWrapApiVertexPackingePackingUsage.PS_TexCoord:
                    ElementType = ElementType.Main;
                    AttributeKey = $"TEXCOORD_{_element.UsageIndex}";

                    DstFormat = new(DimensionType.VEC2, EncodingType.FLOAT, false);
                    break;
                case GpuWrapApiVertexPackingePackingUsage.PS_Color:
                    ElementType = ElementType.Main;
                    AttributeKey = $"COLOR_{_element.UsageIndex}";

                    DstFormat = new(DimensionType.VEC4, EncodingType.UNSIGNED_SHORT, true);
                    break;
                case GpuWrapApiVertexPackingePackingUsage.PS_VehicleDmgNormal:
                    ElementType = ElementType.VehicleDamage;
                    AttributeKey = "NORMAL";

                    DstFormat = new(DimensionType.VEC3, EncodingType.FLOAT, false);
                    break;
                case GpuWrapApiVertexPackingePackingUsage.PS_VehicleDmgPosition:
                    ElementType = ElementType.VehicleDamage;
                    AttributeKey = "POSITION";

                    DstFormat = new(DimensionType.VEC3, EncodingType.FLOAT, false);
                    break;
                case GpuWrapApiVertexPackingePackingUsage.PS_SkinIndices:
                    ElementType = ElementType.Main;
                    AttributeKey = $"JOINTS_{_element.UsageIndex}";

                    DstFormat = new(DimensionType.VEC4, EncodingType.UNSIGNED_BYTE, false);
                    break;
                case GpuWrapApiVertexPackingePackingUsage.PS_SkinWeights:
                    ElementType = ElementType.Main;
                    AttributeKey = $"WEIGHTS_{_element.UsageIndex}";

                    DstFormat = new(DimensionType.VEC4, EncodingType.FLOAT, false);
                    break;
                case GpuWrapApiVertexPackingePackingUsage.PS_DestructionIndices:
                    ElementType = ElementType.Todo;
                    AttributeKey = $"???";

                    DstFormat = new(DimensionType.VEC2, EncodingType.UNSIGNED_SHORT, false);
                    break;
                case GpuWrapApiVertexPackingePackingUsage.PS_MultilayerPaint:
                    ElementType = ElementType.Todo;
                    AttributeKey = $"???";
                    break;
                case GpuWrapApiVertexPackingePackingUsage.PS_Invalid:
                case GpuWrapApiVertexPackingePackingUsage.PS_SysPosition:
                case GpuWrapApiVertexPackingePackingUsage.PS_Binormal:
                case GpuWrapApiVertexPackingePackingUsage.PS_InstanceTransform:
                case GpuWrapApiVertexPackingePackingUsage.PS_InstanceLODParams:
                case GpuWrapApiVertexPackingePackingUsage.PS_InstanceSkinningData:
                case GpuWrapApiVertexPackingePackingUsage.PS_PatchSize:
                case GpuWrapApiVertexPackingePackingUsage.PS_PatchBias:
                case GpuWrapApiVertexPackingePackingUsage.PS_ExtraData:
                case GpuWrapApiVertexPackingePackingUsage.PS_PositionDelta:
                case GpuWrapApiVertexPackingePackingUsage.PS_LightBlockerIntensity:
                case GpuWrapApiVertexPackingePackingUsage.PS_BoneIndex:
                case GpuWrapApiVertexPackingePackingUsage.PS_Padding:
                case GpuWrapApiVertexPackingePackingUsage.PS_PatchOffset:
                case GpuWrapApiVertexPackingePackingUsage.PS_Max:
                default:
                    throw new ArgumentOutOfRangeException(nameof(_element.Usage), _element.Usage, null);
            }

            switch ((GpuWrapApiVertexPackingePackingType)_element.Type)
            {
                case GpuWrapApiVertexPackingePackingType.PT_Short4N:
                    DataSize = 8;
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Float16_2:
                    DataSize = 4;
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Dec4:
                    DataSize = 4;
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Color:
                    DataSize = 4;
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Float4:
                    DataSize = 16;
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_UByte4:
                    DataSize = 4;
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_UByte4N:
                    DataSize = 4;
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_UShort2:
                    DataSize = 4;
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Float1:
                    DataSize = 4;
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Invalid:
                case GpuWrapApiVertexPackingePackingType.PT_Float2:
                case GpuWrapApiVertexPackingePackingType.PT_Float3:
                case GpuWrapApiVertexPackingePackingType.PT_Float16_4:
                case GpuWrapApiVertexPackingePackingType.PT_UShort1:
                case GpuWrapApiVertexPackingePackingType.PT_UShort4:
                case GpuWrapApiVertexPackingePackingType.PT_UShort4N:
                case GpuWrapApiVertexPackingePackingType.PT_Short1:
                case GpuWrapApiVertexPackingePackingType.PT_Short2:
                case GpuWrapApiVertexPackingePackingType.PT_Short4:
                case GpuWrapApiVertexPackingePackingType.PT_UInt1:
                case GpuWrapApiVertexPackingePackingType.PT_UInt2:
                case GpuWrapApiVertexPackingePackingType.PT_UInt3:
                case GpuWrapApiVertexPackingePackingType.PT_UInt4:
                case GpuWrapApiVertexPackingePackingType.PT_Int1:
                case GpuWrapApiVertexPackingePackingType.PT_Int2:
                case GpuWrapApiVertexPackingePackingType.PT_Int3:
                case GpuWrapApiVertexPackingePackingType.PT_Int4:
                case GpuWrapApiVertexPackingePackingType.PT_UByte1:
                case GpuWrapApiVertexPackingePackingType.PT_UByte1F:
                case GpuWrapApiVertexPackingePackingType.PT_Byte4N:
                case GpuWrapApiVertexPackingePackingType.PT_Index16:
                case GpuWrapApiVertexPackingePackingType.PT_Index32:
                case GpuWrapApiVertexPackingePackingType.PT_Max:
                default:
                    throw new ArgumentOutOfRangeException(nameof(_element.Type), _element.Type, null);
            }
        }

        public readonly List<object> Vertices = new();

        public void Read(BinaryReader reader)
        {
            switch ((GpuWrapApiVertexPackingePackingType)_element.Type)
            {
                case GpuWrapApiVertexPackingePackingType.PT_Short4N:
                    Vertices.Add(new Vector4
                    {
                        X = reader.ReadInt16(),
                        Y = reader.ReadInt16(),
                        Z = reader.ReadInt16(),
                        W = reader.ReadInt16()
                    });
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Float16_2:
                    Vertices.Add(new Vector2
                    {
                        X = (float)BitConverter.ToHalf(reader.ReadBytes(2)),
                        Y = (float)BitConverter.ToHalf(reader.ReadBytes(2))
                    });
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Dec4:
                    var u32 = reader.ReadUInt32();

                    var x3 = Convert.ToSingle(u32 & 0x3ff);
                    var y3 = Convert.ToSingle((u32 >> 10) & 0x3ff);
                    var z3 = Convert.ToSingle((u32 >> 20) & 0x3ff);
                    var dequant = 1f / 1023f;

                    Vertices.Add(new Vector4
                    {
                        X = (x3 * 2 * dequant) - 1f,
                        Y = (y3 * 2 * dequant) - 1f,
                        Z = (z3 * 2 * dequant) - 1f,
                        W = 1F
                    });
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Color:
                    Vertices.Add(new Vector4
                    {
                        X = reader.ReadByte(),
                        Y = reader.ReadByte(),
                        Z = reader.ReadByte(),
                        W = reader.ReadByte()
                    });
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Float4:
                    Vertices.Add(new Vector4
                    {
                        X = reader.ReadSingle(),
                        Y = reader.ReadSingle(),
                        Z = reader.ReadSingle(),
                        W = reader.ReadSingle()
                    });
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_UByte4:
                    Vertices.Add(new Vector4
                    {
                        X = reader.ReadByte(),
                        Y = reader.ReadByte(),
                        Z = reader.ReadByte(),
                        W = reader.ReadByte()
                    });
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_UByte4N:
                    Vertices.Add(new Vector4
                    {
                        X = reader.ReadByte(),
                        Y = reader.ReadByte(),
                        Z = reader.ReadByte(),
                        W = reader.ReadByte()
                    });
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_UShort2:
                    Vertices.Add(new Vector2
                    {
                        X = reader.ReadInt16(),
                        Y = reader.ReadInt16()
                    });
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Float1:
                    Vertices.Add(reader.ReadSingle());
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Invalid:
                case GpuWrapApiVertexPackingePackingType.PT_Float2:
                case GpuWrapApiVertexPackingePackingType.PT_Float3:
                case GpuWrapApiVertexPackingePackingType.PT_Float16_4:
                case GpuWrapApiVertexPackingePackingType.PT_UShort1:
                case GpuWrapApiVertexPackingePackingType.PT_UShort4:
                case GpuWrapApiVertexPackingePackingType.PT_UShort4N:
                case GpuWrapApiVertexPackingePackingType.PT_Short1:
                case GpuWrapApiVertexPackingePackingType.PT_Short2:
                case GpuWrapApiVertexPackingePackingType.PT_Short4:
                case GpuWrapApiVertexPackingePackingType.PT_UInt1:
                case GpuWrapApiVertexPackingePackingType.PT_UInt2:
                case GpuWrapApiVertexPackingePackingType.PT_UInt3:
                case GpuWrapApiVertexPackingePackingType.PT_UInt4:
                case GpuWrapApiVertexPackingePackingType.PT_Int1:
                case GpuWrapApiVertexPackingePackingType.PT_Int2:
                case GpuWrapApiVertexPackingePackingType.PT_Int3:
                case GpuWrapApiVertexPackingePackingType.PT_Int4:
                case GpuWrapApiVertexPackingePackingType.PT_UByte1:
                case GpuWrapApiVertexPackingePackingType.PT_UByte1F:
                case GpuWrapApiVertexPackingePackingType.PT_Byte4N:
                case GpuWrapApiVertexPackingePackingType.PT_Index16:
                case GpuWrapApiVertexPackingePackingType.PT_Index32:
                case GpuWrapApiVertexPackingePackingType.PT_Max:
                default:
                    throw new ArgumentOutOfRangeException(nameof(_element.Type), _element.Type, null);
            }
        }

        public void Write(WolvenKit.RED4.Types.Vector4 quantizationScale, WolvenKit.RED4.Types.Vector4 quantizationOffset)
        {
            switch ((GpuWrapApiVertexPackingePackingType)_element.Type)
            {
                case GpuWrapApiVertexPackingePackingType.PT_Short4N:
                    foreach (Vector4 vertex in Vertices)
                    {
                        if (DstFormat.Encoding == EncodingType.FLOAT)
                        {
                            var x = vertex.X / short.MaxValue;
                            var y = vertex.Y / short.MaxValue;
                            var z = vertex.Z / short.MaxValue;

                            if (AttributeKey == "POSITION")
                            {
                                x = x * quantizationScale.X + quantizationOffset.X;
                                y = y * quantizationScale.Y + quantizationOffset.Y;
                                z = z * quantizationScale.Z + quantizationOffset.Z;
                            }

                            if (DstFormat.Dimensions == DimensionType.VEC3)
                            {
                                _writer.Write(x);
                                _writer.Write(z);
                                _writer.Write(-y);
                            }
                            else
                            {
                                throw new NotSupportedException();
                            }
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }
                    }
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Float16_2:
                    foreach (Vector2 vertex in Vertices)
                    {
                        if (DstFormat.Encoding == EncodingType.FLOAT)
                        {
                            if (DstFormat.Dimensions == DimensionType.VEC2)
                            {
                                _writer.Write(vertex.X);
                                _writer.Write(vertex.Y);
                            }
                            else
                            {
                                throw new NotSupportedException();
                            }
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }
                    }
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Dec4:
                    foreach (Vector4 vertex in Vertices)
                    {
                        if (DstFormat.Encoding == EncodingType.FLOAT)
                        {
                            if (DstFormat.Dimensions == DimensionType.VEC3)
                            {
                                _writer.Write(vertex.X);
                                _writer.Write(vertex.Z);
                                _writer.Write(-vertex.Y);
                            }
                            else if (DstFormat.Dimensions == DimensionType.VEC4)
                            {
                                _writer.Write(vertex.X);
                                _writer.Write(vertex.Z);
                                _writer.Write(-vertex.Y);
                                _writer.Write(vertex.W);

                                if (!vertex.IsValidTangent())
                                {
                                    vertex.IsValidTangent();
                                }
                            }
                            else
                            {
                                throw new NotSupportedException();
                            }
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }
                    }
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Color:
                    foreach (Vector4 vertex in Vertices)
                    {
                        if (DstFormat.Encoding == EncodingType.UNSIGNED_SHORT)
                        {
                            if (DstFormat.Dimensions == DimensionType.VEC4)
                            {
                                _writer.Write((ushort)vertex.X);
                                _writer.Write((ushort)vertex.Y);
                                _writer.Write((ushort)vertex.Z);
                                _writer.Write((ushort)vertex.W);
                            }
                            else
                            {
                                throw new NotSupportedException();
                            }
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }
                    }
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Float4:
                    foreach (Vector4 vertex in Vertices)
                    {
                        if (DstFormat.Encoding == EncodingType.FLOAT)
                        {
                            if (DstFormat.Dimensions == DimensionType.VEC3)
                            {
                                _writer.Write(vertex.X / short.MaxValue);
                                _writer.Write(vertex.Z / short.MaxValue);
                                _writer.Write(-vertex.Y / short.MaxValue);
                            }
                            else
                            {
                                throw new NotSupportedException();
                            }
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }
                    }
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_UByte4:
                    foreach (Vector4 vertex in Vertices)
                    {
                        if (DstFormat.Encoding == EncodingType.UNSIGNED_BYTE)
                        {
                            if (DstFormat.Dimensions == DimensionType.VEC4)
                            {
                                _writer.Write((byte)vertex.X);
                                _writer.Write((byte)vertex.Y);
                                _writer.Write((byte)vertex.Z);
                                _writer.Write((byte)vertex.W);
                            }
                            else
                            {
                                throw new NotSupportedException();
                            }
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }
                    }
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_UByte4N:
                    foreach (Vector4 vertex in Vertices)
                    {
                        if (DstFormat.Encoding == EncodingType.FLOAT)
                        {
                            if (DstFormat.Dimensions == DimensionType.VEC4)
                            {
                                _writer.Write(vertex.X / byte.MaxValue);
                                _writer.Write(vertex.Y / byte.MaxValue);
                                _writer.Write(vertex.Z / byte.MaxValue);
                                _writer.Write(vertex.W / byte.MaxValue);
                            }
                            else
                            {
                                throw new NotSupportedException();
                            }
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }
                    }
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_UShort2:
                    foreach (Vector2 vertex in Vertices)
                    {
                        if (DstFormat.Encoding == EncodingType.UNSIGNED_SHORT)
                        {
                            if (DstFormat.Dimensions == DimensionType.VEC2)
                            {
                                _writer.Write((ushort)vertex.X);
                                _writer.Write((ushort)vertex.Y);
                            }
                            else
                            {
                                throw new NotSupportedException();
                            }
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }
                    }
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Float1:
                    foreach (float vertex in Vertices)
                    {
                        _writer.Write(vertex);
                    }
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Invalid:
                case GpuWrapApiVertexPackingePackingType.PT_Float2:
                case GpuWrapApiVertexPackingePackingType.PT_Float3:
                case GpuWrapApiVertexPackingePackingType.PT_Float16_4:
                case GpuWrapApiVertexPackingePackingType.PT_UShort1:
                case GpuWrapApiVertexPackingePackingType.PT_UShort4:
                case GpuWrapApiVertexPackingePackingType.PT_UShort4N:
                case GpuWrapApiVertexPackingePackingType.PT_Short1:
                case GpuWrapApiVertexPackingePackingType.PT_Short2:
                case GpuWrapApiVertexPackingePackingType.PT_Short4:
                case GpuWrapApiVertexPackingePackingType.PT_UInt1:
                case GpuWrapApiVertexPackingePackingType.PT_UInt2:
                case GpuWrapApiVertexPackingePackingType.PT_UInt3:
                case GpuWrapApiVertexPackingePackingType.PT_UInt4:
                case GpuWrapApiVertexPackingePackingType.PT_Int1:
                case GpuWrapApiVertexPackingePackingType.PT_Int2:
                case GpuWrapApiVertexPackingePackingType.PT_Int3:
                case GpuWrapApiVertexPackingePackingType.PT_Int4:
                case GpuWrapApiVertexPackingePackingType.PT_UByte1:
                case GpuWrapApiVertexPackingePackingType.PT_UByte1F:
                case GpuWrapApiVertexPackingePackingType.PT_Byte4N:
                case GpuWrapApiVertexPackingePackingType.PT_Index16:
                case GpuWrapApiVertexPackingePackingType.PT_Index32:
                case GpuWrapApiVertexPackingePackingType.PT_Max:
                default:
                    throw new ArgumentOutOfRangeException(nameof(_element.Type), _element.Type, null);
            }
        }

        public byte[] GetArray() => _stream.ToArray();
    }

    private Dictionary<byte, Node> ExtractMeshes(rendRenderMeshBlob rendMeshBlob, IList<Material> materials)
    {
        var ms = new MemoryStream(rendMeshBlob.RenderBuffer.Buffer.GetBytes());
        var bufferReader = new BinaryReader(ms);

        var result = new Dictionary<byte, Node>();
        for (var i = 0; i < rendMeshBlob.Header.RenderChunkInfos.Count; i++)
        {
            var renderChunkInfo = rendMeshBlob.Header.RenderChunkInfos[i];
            var material = materials[i];

            if (!result.TryGetValue(renderChunkInfo.LodMask, out var node))
            {
                node = _skeleton != null ? _skeleton.CreateNode() : _modelRoot.CreateLogicalNode();
                node.Name = $"Mesh_lod{renderChunkInfo.LodMask}";
                node.Mesh = _modelRoot.CreateMesh();

                result.Add(renderChunkInfo.LodMask, node);
            }
            var mesh = node.Mesh;
            var primitive = mesh.CreatePrimitive();
            primitive.Material = material;

            using var ms1 = new MemoryStream();
            using var bw1 = new BinaryWriter(ms1);

            var dict = new Dictionary<int, List<VertexElement>>();

            foreach (var vertexLayoutElement in renderChunkInfo.ChunkVertices.VertexLayout.Elements)
            {
                if (vertexLayoutElement.Usage == GpuWrapApiVertexPackingePackingUsage.PS_Invalid)
                {
                    continue;
                }

                if (vertexLayoutElement.StreamIndex > 4)
                {
                    continue;
                }

                if (!dict.TryGetValue(vertexLayoutElement.StreamIndex, out var lst))
                {
                    lst = new List<VertexElement>();
                    dict.Add(vertexLayoutElement.StreamIndex, lst);
                }

                lst.Add(new VertexElement(vertexLayoutElement));
            }

            foreach (var (index, elementInfos) in dict)
            {
                var offset = renderChunkInfo.ChunkVertices.ByteOffsets[index];

                var sizes = new List<uint>();
                byte totalSize = 0;
                foreach (var elementInfo in elementInfos)
                {
                    sizes.Add(elementInfo.DataSize);
                    totalSize += elementInfo.DataSize;
                }

                if (renderChunkInfo.ChunkVertices.VertexLayout.SlotStrides[index] != totalSize)
                {
                    throw new Exception();
                }

                for (var j = 0; j < renderChunkInfo.NumVertices; j++)
                {
                    var nextOffset = offset + totalSize * j;
                    if (bufferReader.BaseStream.Position != nextOffset)
                    {
                        if (nextOffset > bufferReader.BaseStream.Position)
                        {
                            var diff = nextOffset - bufferReader.BaseStream.Position;
                            var tmp = bufferReader.ReadBytes((int)diff); // always 0, padding?
                        }
                        else
                        {
                            bufferReader.BaseStream.Position = nextOffset;
                        }
                    }

                    foreach (var elementInfo in elementInfos)
                    {
                        elementInfo.Read(bufferReader);
                    }
                }

                DoChecks(elementInfos);

                var vehicleDict = new Dictionary<string, Accessor>();
                foreach (var elementInfo in elementInfos)
                {
                    elementInfo.Write(rendMeshBlob.Header.QuantizationScale, rendMeshBlob.Header.QuantizationOffset);
                    var buffer = elementInfo.GetArray();

                    var acc = _modelRoot.CreateAccessor();
                    var buff = _modelRoot.UseBufferView(buffer, 0, buffer.Length, 0, BufferMode.ARRAY_BUFFER);
                    acc.SetVertexData(buff, 0, renderChunkInfo.NumVertices, elementInfo.DstFormat.Dimensions, elementInfo.DstFormat.Encoding, elementInfo.DstFormat.Normalized);

                    switch (elementInfo.ElementType)
                    {
                        case ElementType.Todo:
                            break;
                        case ElementType.Main:
                            primitive.SetVertexAccessor(elementInfo.AttributeKey, acc);
                            break;
                        case ElementType.VehicleDamage:
                            vehicleDict.Add(elementInfo.AttributeKey, acc);
                            break;
                        case ElementType.Unknown:
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                if (vehicleDict.Count > 0)
                {
                    primitive.SetMorphTargetAccessors(0, vehicleDict);
                }
            }

            bufferReader.BaseStream.Position = rendMeshBlob.Header.IndexBufferOffset + renderChunkInfo.ChunkIndices.TeOffset;
            for (var j = 0; j < renderChunkInfo.NumIndices; j+=3)
            {
                switch ((GpuWrapApieIndexBufferChunkType)renderChunkInfo.ChunkIndices.Pe)
                {
                    case GpuWrapApieIndexBufferChunkType.IBCT_IndexUShort:
                        var t1 = bufferReader.ReadUInt16();
                        var t2 = bufferReader.ReadUInt16();
                        var t3 = bufferReader.ReadUInt16();

                        bw1.Write(t3);
                        bw1.Write(t2);
                        bw1.Write(t1);
                        break;
                    case GpuWrapApieIndexBufferChunkType.IBCT_IndexUInt:
                    case GpuWrapApieIndexBufferChunkType.IBCT_Max:
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            var buffer1 = ms1.ToArray();

            var acc1 = _modelRoot.CreateAccessor();
            var buff1 = _modelRoot.UseBufferView(buffer1, 0, buffer1.Length, 0, BufferMode.ELEMENT_ARRAY_BUFFER);
            acc1.SetIndexData(buff1, 0, (int)(uint)renderChunkInfo.NumIndices, IndexEncodingType.UNSIGNED_SHORT);
            primitive.SetIndexAccessor(acc1);
        }

        return result;
    }

    private void DoChecks(List<VertexElement> elementInfos)
    {
        var tangents = elementInfos.FirstOrDefault(x => x.AttributeKey == "TANGENT");
        if (tangents != null)
        {
            for (int i = 0; i < tangents.Vertices.Count; i++)
            {
                var tangent = (Vector4)tangents.Vertices[i];
                if (!tangent.IsValidTangent())
                {
                    tangents.Vertices[i] = tangent.SanitizeTangent();
                }
            }
        }

        var weights0 = elementInfos.FirstOrDefault(x => x.AttributeKey == "WEIGHTS_0");
        var weights1 = elementInfos.FirstOrDefault(x => x.AttributeKey == "WEIGHTS_1");

        if (weights0 != null && weights1 != null)
        {
            for (int j = 0; j < weights0.Vertices.Count; j++)
            {
                var vertex0 = (Vector4)weights0.Vertices[j];
                var vertex1 = (Vector4)weights1.Vertices[j];

                var sum = vertex0.X + vertex0.Y + vertex0.Z + vertex0.W + vertex1.X + vertex1.Y + vertex1.Z + vertex1.W;
                if (sum != 255)
                {
                    weights0.Vertices[j] = vertex0 with { X = vertex0.X + (byte)(255 - sum) };
                }
            }
        }
        else if (weights0 != null)
        {
            for (int j = 0; j < weights0.Vertices.Count; j++)
            {
                var vertex0 = (Vector4)weights0.Vertices[j];

                var sum = vertex0.X + vertex0.Y + vertex0.Z + vertex0.W;
                if (sum != 255)
                {
                    weights0.Vertices[j] = vertex0 with { X = vertex0.X + (byte)(255 - sum) };
                }
            }
        }
    }

    public static byte GetSize(GpuWrapApiVertexPackingePackingType type)
    {
        switch (type)
        {
            case GpuWrapApiVertexPackingePackingType.PT_Short4N:
                return 8;
            case GpuWrapApiVertexPackingePackingType.PT_Float16_2:
                return 4;
            case GpuWrapApiVertexPackingePackingType.PT_Dec4:
                return 4;
            case GpuWrapApiVertexPackingePackingType.PT_Color:
                return 4;
            case GpuWrapApiVertexPackingePackingType.PT_Float4:
                return 16;
            case GpuWrapApiVertexPackingePackingType.PT_Invalid:
            case GpuWrapApiVertexPackingePackingType.PT_Float1:
            case GpuWrapApiVertexPackingePackingType.PT_Float2:
            case GpuWrapApiVertexPackingePackingType.PT_Float3:
            case GpuWrapApiVertexPackingePackingType.PT_Float16_4:
            case GpuWrapApiVertexPackingePackingType.PT_UShort1:
            case GpuWrapApiVertexPackingePackingType.PT_UShort2:
            case GpuWrapApiVertexPackingePackingType.PT_UShort4:
            case GpuWrapApiVertexPackingePackingType.PT_UShort4N:
            case GpuWrapApiVertexPackingePackingType.PT_Short1:
            case GpuWrapApiVertexPackingePackingType.PT_Short2:
            case GpuWrapApiVertexPackingePackingType.PT_Short4:
            case GpuWrapApiVertexPackingePackingType.PT_UInt1:
            case GpuWrapApiVertexPackingePackingType.PT_UInt2:
            case GpuWrapApiVertexPackingePackingType.PT_UInt3:
            case GpuWrapApiVertexPackingePackingType.PT_UInt4:
            case GpuWrapApiVertexPackingePackingType.PT_Int1:
            case GpuWrapApiVertexPackingePackingType.PT_Int2:
            case GpuWrapApiVertexPackingePackingType.PT_Int3:
            case GpuWrapApiVertexPackingePackingType.PT_Int4:
            case GpuWrapApiVertexPackingePackingType.PT_UByte1:
            case GpuWrapApiVertexPackingePackingType.PT_UByte1F:
            case GpuWrapApiVertexPackingePackingType.PT_UByte4:
            case GpuWrapApiVertexPackingePackingType.PT_UByte4N:
            case GpuWrapApiVertexPackingePackingType.PT_Byte4N:
            case GpuWrapApiVertexPackingePackingType.PT_Index16:
            case GpuWrapApiVertexPackingePackingType.PT_Index32:
            case GpuWrapApiVertexPackingePackingType.PT_Max:
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    private void CreatePrimitive(byte[] buffer, MeshPrimitive primitive, string key, DimensionType dimensionType, EncodingType encodingType, bool normalized, int itemCount, int bpe)
    {
        var acc = _modelRoot.CreateAccessor();
        var buff = _modelRoot.UseBufferView(buffer, 0, buffer.Length, 0, BufferMode.ARRAY_BUFFER);
        acc.SetVertexData(buff, 0, itemCount, dimensionType, encodingType, normalized);
        primitive.SetVertexAccessor(key, acc);
    }

    private void ExtractPosition(BinaryReader bufferReader, GpuWrapApiVertexPackingePackingType packingType, Mesh mesh)
    {
        bufferReader.BaseStream.Position = 0;

        var acc = _modelRoot.CreateAccessor();
        //acc.SetVertexData();

        var primitive = mesh.CreatePrimitive();
        primitive.SetVertexAccessor("POSITION", acc);
    }
}