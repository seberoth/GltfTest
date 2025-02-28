﻿using SharpGLTF.Schema2;
using WolvenKit.RED4.Archive.CR2W;
using WolvenKit.RED4.Types;
using System.Numerics;
using System.Text.Json;
using GltfTest.Extras;
using SharpGLTF.Memory;
using WolvenKit.Common;
using static WolvenKit.RED4.Types.Enums;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace GltfTest;

public partial class GltfConverter
{
    private readonly IArchiveManager _archiveManager;
    private readonly GameFileWrapper _file;
    private readonly string _depotPath;

    private ModelRoot _modelRoot = ModelRoot.CreateModel();
    private Node? _skeleton;

    public GltfConverter(CR2WFile file, IArchiveManager archiveManager, string depotPath)
    {
        _archiveManager = archiveManager;
        _file = new GameFileWrapper(file, _archiveManager);
        _depotPath = depotPath;
    }

    public void SaveGLB(string filePath, WriteSettings? settings = null)
    {
        if (_file.Resource is CMesh cMesh)
        {
            var model = ToGltf(cMesh);
            model!.SaveGLB(filePath, settings);
        }

        if (_file.Resource is MorphTargetMesh morphTargetMesh)
        {
            var model = ToGltf(morphTargetMesh);
            model!.SaveGLB(filePath, settings);
        }
    }

    private (Skin skin, List<Node> bones) ExtractSkeleton(CMesh mesh, rendRenderMeshBlob rendRenderMeshBlob)
    {
        var skin = _modelRoot.CreateSkin("Skeleton");
        skin.Skeleton = _skeleton!;

        var bones = new List<Node>();
        for (int i = 0; i < mesh.BoneNames.Count; i++)
        {
            var boneNode = _skeleton.CreateNode(mesh.BoneNames[i]);

            var boneRig = mesh.BoneRigMatrices[i];

            Matrix4x4.Invert(boneRig, out var inverseBoneRig);
            inverseBoneRig.M44 = 1;

            // Maybe RotX?
            boneNode.WorldMatrix = RotY(YUp(inverseBoneRig));

            boneNode.Extras = JsonSerializer.SerializeToNode(new { Epsilon = (float)mesh.BoneVertexEpsilons[i], Lod = (byte)mesh.LodBoneMask[i] });

            bones.Add(boneNode);
        }

        skin.BindJoints(bones.ToArray());

        return (skin, bones);
    }

    private Matrix4x4 RotY(Matrix4x4 src)
    {
        var axisBaseChange = new Matrix4x4(
            0.0F, 0.0F, 1.0F, 0.0F,
            0.0F, 1.0F, 0.0F, 0.0F,
            -1.0F, 0.0F, 0.0F, 0.0F,
            0.0F, 0.0F, 0.0F, 1.0F);

        return Matrix4x4.Multiply(axisBaseChange, src);
    }

    private Matrix4x4 YUp(Matrix4x4 src)
    {
        return src with
        {
            M12 = src.M13,
            M13 = -src.M12,

            M22 = src.M23,
            M23 = -src.M22,

            M32 = src.M33,
            M33 = -src.M32,

            M42 = src.M43,
            M43 = -src.M42,
            M44 = 1
        };
    }

    private enum ElementType
    {
        Unknown,
        Todo,
        Main,
        Garment,
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

        public VertexElement(GpuWrapApiVertexPackingPackingElement element, EMaterialVertexFactory vertexFactory)
        {
            _element = element;
            _writer = new BinaryWriter(_stream);

            GetInfo(vertexFactory);
        }

        private void GetInfo(EMaterialVertexFactory vertexFactory)
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

                    DstFormat = new(DimensionType.SCALAR, EncodingType.FLOAT, false);
                    break;
                case GpuWrapApiVertexPackingePackingUsage.PS_ExtraData:
                    if (vertexFactory is
                        EMaterialVertexFactory.MVF_GarmentMeshSkinned or
                        EMaterialVertexFactory.MVF_GarmentMeshExtSkinned or
                        EMaterialVertexFactory.MVF_GarmentMeshSkinnedLightBlockers or
                        EMaterialVertexFactory.MVF_GarmentMeshExtSkinnedLightBlockers)
                    {
                        ElementType = ElementType.Garment;
                        AttributeKey = "POSITION";

                        DstFormat = new(DimensionType.VEC3, EncodingType.FLOAT, false);
                    }
                    else if (vertexFactory == EMaterialVertexFactory.MVF_MeshSpeedTree)
                    {
                        ElementType = ElementType.Todo;
                        AttributeKey = "???";

                        // has index 0, 1, 2
                        switch (_element.UsageIndex)
                        {
                            case 0:
                                // X seems to be byte
                                // DstFormat = new(DimensionType.SCALAR, EncodingType.BYTE, false);
                                DstFormat = new(DimensionType.VEC4, EncodingType.FLOAT, false);
                                break;
                            case 1:
                            case 2:
                                DstFormat = new(DimensionType.VEC4, EncodingType.FLOAT, false);
                                break;
                            default:
                                throw new NotImplementedException();
                        }
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                    break;
                case GpuWrapApiVertexPackingePackingUsage.PS_LightBlockerIntensity:
                    ElementType = ElementType.Main;
                    AttributeKey = "_LIGHTBLOCKERINTENSITY";

                    DstFormat = new(DimensionType.SCALAR, EncodingType.FLOAT, false);
                    break;
                case GpuWrapApiVertexPackingePackingUsage.PS_BoneIndex:
                    ElementType = ElementType.Todo;
                    AttributeKey = $"???";

                    DstFormat = new(DimensionType.SCALAR, EncodingType.UNSIGNED_INT, false);
                    break;
                case GpuWrapApiVertexPackingePackingUsage.PS_Invalid:
                case GpuWrapApiVertexPackingePackingUsage.PS_SysPosition:
                case GpuWrapApiVertexPackingePackingUsage.PS_Binormal:
                case GpuWrapApiVertexPackingePackingUsage.PS_InstanceTransform:
                case GpuWrapApiVertexPackingePackingUsage.PS_InstanceLODParams:
                case GpuWrapApiVertexPackingePackingUsage.PS_InstanceSkinningData:
                case GpuWrapApiVertexPackingePackingUsage.PS_PatchSize:
                case GpuWrapApiVertexPackingePackingUsage.PS_PatchBias:
                case GpuWrapApiVertexPackingePackingUsage.PS_PositionDelta:
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
                case GpuWrapApiVertexPackingePackingType.PT_Float16_4:
                    DataSize = 8;
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_UInt1:
                    DataSize = 4;
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Invalid:
                case GpuWrapApiVertexPackingePackingType.PT_Float2:
                case GpuWrapApiVertexPackingePackingType.PT_Float3:
                case GpuWrapApiVertexPackingePackingType.PT_UShort1:
                case GpuWrapApiVertexPackingePackingType.PT_UShort4:
                case GpuWrapApiVertexPackingePackingType.PT_UShort4N:
                case GpuWrapApiVertexPackingePackingType.PT_Short1:
                case GpuWrapApiVertexPackingePackingType.PT_Short2:
                case GpuWrapApiVertexPackingePackingType.PT_Short4:
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

        public readonly List<Vector4> Vertices = new();

        public void Read(VertexAttributeReader reader)
        {
            switch ((GpuWrapApiVertexPackingePackingType)_element.Type)
            {
                case GpuWrapApiVertexPackingePackingType.PT_Short4N:
                    Vertices.Add(reader.ReadShortN4());
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Float16_2:
                    Vertices.Add(reader.ReadHalf2());
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Dec4:
                    Vertices.Add(reader.ReadWKitDec4());
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Color:
                    Vertices.Add(reader.ReadColor());
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Float4:
                    Vertices.Add(reader.ReadFloat4());
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_UByte4:
                    Vertices.Add(reader.ReadUByte4());
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_UByte4N:
                    Vertices.Add(reader.ReadUByteN4());
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_UShort2:
                    Vertices.Add(reader.ReadUShort2());
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Float1:
                    Vertices.Add(reader.ReadFloat());
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Float16_4:
                    Vertices.Add(reader.ReadHalf4());
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_UInt1:
                    Vertices.Add(reader.ReadUInt());
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Invalid:
                case GpuWrapApiVertexPackingePackingType.PT_Float2:
                case GpuWrapApiVertexPackingePackingType.PT_Float3:
                case GpuWrapApiVertexPackingePackingType.PT_UShort1:
                case GpuWrapApiVertexPackingePackingType.PT_UShort4:
                case GpuWrapApiVertexPackingePackingType.PT_UShort4N:
                case GpuWrapApiVertexPackingePackingType.PT_Short1:
                case GpuWrapApiVertexPackingePackingType.PT_Short2:
                case GpuWrapApiVertexPackingePackingType.PT_Short4:
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

        private Vector4 YUp(Vector4 src)
        {
            return src with { Y = src.Z, Z = -src.Y };
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
                            var v = vertex;

                            if (AttributeKey == "POSITION")
                            {
                                v = DirectXMeshHelper.MultiplyAdd(v, quantizationScale, quantizationOffset);
                            }

                            v = YUp(v);

                            if (DstFormat.Dimensions == DimensionType.VEC3)
                            {
                                _writer.Write(v.X);
                                _writer.Write(v.Y);
                                _writer.Write(v.Z);
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
                    foreach (Vector4 vertex in Vertices)
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
                            var v = YUp(vertex);

                            if (DstFormat.Dimensions == DimensionType.VEC3)
                            {
                                _writer.Write(v.X);
                                _writer.Write(v.Y);
                                _writer.Write(v.Z);
                            }
                            else if (DstFormat.Dimensions == DimensionType.VEC4)
                            {
                                _writer.Write(v.X);
                                _writer.Write(v.Y);
                                _writer.Write(v.Z);
                                _writer.Write(v.W);

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
                                var v = vertex / short.MaxValue;

                                // Debug
                                if (ElementType == ElementType.VehicleDamage && AttributeKey == "POSITION")
                                {
                                    v = v * 100f;
                                }

                                v = YUp(v);

                                _writer.Write(v.X);
                                _writer.Write(v.Y);
                                _writer.Write(v.Z);
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
                                _writer.Write(vertex.X);
                                _writer.Write(vertex.Y);
                                _writer.Write(vertex.Z);
                                _writer.Write(vertex.W);
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
                    foreach (Vector4 vertex in Vertices)
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
                    foreach (Vector4 vertex in Vertices)
                    {
                        if (DstFormat.Encoding == EncodingType.FLOAT)
                        {
                            if (DstFormat.Dimensions == DimensionType.SCALAR)
                            {
                                _writer.Write(vertex.X);
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
                case GpuWrapApiVertexPackingePackingType.PT_Float16_4:
                    foreach (Vector4 vertex in Vertices)
                    {
                        var v = YUp(vertex);

                        if (DstFormat.Encoding == EncodingType.FLOAT)
                        {
                            if (DstFormat.Dimensions == DimensionType.VEC3)
                            {
                                _writer.Write(v.X);
                                _writer.Write(v.Y);
                                _writer.Write(v.Z);
                            }
                            else if (DstFormat.Dimensions == DimensionType.VEC4)
                            {
                                _writer.Write(v.X);
                                _writer.Write(v.Y);
                                _writer.Write(v.Z);
                                _writer.Write(v.W);
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
                case GpuWrapApiVertexPackingePackingType.PT_UInt1:
                    foreach (Vector4 vertex in Vertices)
                    {
                        if (DstFormat.Encoding == EncodingType.UNSIGNED_INT)
                        {
                            if (DstFormat.Dimensions == DimensionType.SCALAR)
                            {
                                _writer.Write((uint)vertex.X);
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
                case GpuWrapApiVertexPackingePackingType.PT_Invalid:
                case GpuWrapApiVertexPackingePackingType.PT_Float2:
                case GpuWrapApiVertexPackingePackingType.PT_Float3:
                case GpuWrapApiVertexPackingePackingType.PT_UShort1:
                case GpuWrapApiVertexPackingePackingType.PT_UShort4:
                case GpuWrapApiVertexPackingePackingType.PT_UShort4N:
                case GpuWrapApiVertexPackingePackingType.PT_Short1:
                case GpuWrapApiVertexPackingePackingType.PT_Short2:
                case GpuWrapApiVertexPackingePackingType.PT_Short4:
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

    private Dictionary<byte, Node> ExtractMeshes(rendRenderMeshBlob rendMeshBlob, List<VariantsRootEntry> variants, Dictionary<string, Material> materials)
    {
        var ms = new MemoryStream(rendMeshBlob.RenderBuffer.Buffer.GetBytes());
        var bufferReader = new VertexAttributeReader(ms);

        var globalMorphTargets = new Dictionary<string, List<string>>();

        var result = new Dictionary<byte, Node>();
        for (var i = 0; i < rendMeshBlob.Header.RenderChunkInfos.Count; i++)
        {
            var renderChunkInfo = rendMeshBlob.Header.RenderChunkInfos[i];

            if (!result.TryGetValue(renderChunkInfo.LodMask, out var node))
            {
                node = _skeleton != null ? _skeleton.CreateNode() : _modelRoot.UseScene(0).CreateNode();
                node.Name = $"Mesh_lod{renderChunkInfo.LodMask}";
                node.Mesh = _modelRoot.CreateMesh();

                result.Add(renderChunkInfo.LodMask, node);
            }

            if (!globalMorphTargets.TryGetValue(node.Name, out var morphTargets))
            {
                morphTargets = new List<string>();
                globalMorphTargets.Add(node.Name, morphTargets);
            }

            var mesh = node.Mesh;
            var primitive = mesh.CreatePrimitive();

            primitive.Material = materials[variants[0].Materials[i]];
            if (variants.Count > 1)
            {
                var variantList = primitive.UseExtension<VariantsPrimitiveExtension>();

                var tmpDict = new Dictionary<int, VariantsPrimitiveEntry>();
                for (int j = 0; j < variants.Count; j++)
                {
                    var material = materials[variants[j].Materials[i]];

                    if (!tmpDict.ContainsKey(material.LogicalIndex))
                    {
                        var variant = new VariantsPrimitiveEntry { Material = material.LogicalIndex };
                        
                        variantList.Mappings.Add(variant);
                        tmpDict.Add(material.LogicalIndex, variant);
                    }
                    tmpDict[material.LogicalIndex].Variants.Add(j);
                }
            }

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

                lst.Add(new VertexElement(vertexLayoutElement, (EMaterialVertexFactory)(byte)renderChunkInfo.VertexFactory));
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

                var padding = (-bufferReader.BaseStream.Position) & 15;
                if (padding > 0)
                {
                    bufferReader.BaseStream.Position += padding;
                }

                for (var j = 0; j < renderChunkInfo.NumVertices; j++)
                {
                    if (bufferReader.BaseStream.Position != offset + totalSize * j)
                    {
                        throw new Exception();
                    }

                    foreach (var elementInfo in elementInfos)
                    {
                        elementInfo.Read(bufferReader);
                    }
                }

                TransformData(elementInfos);

                var garmentDict = new Dictionary<string, Accessor>();
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
                        case ElementType.Garment:
                            garmentDict.Add(elementInfo.AttributeKey, acc);
                            break;
                        case ElementType.Unknown:
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                if (garmentDict.Count > 0)
                {
                    var morphTargetIdx = morphTargets.IndexOf("GarmentSupport");
                    if (morphTargetIdx == -1)
                    {
                        morphTargets.Add("GarmentSupport");
                        morphTargetIdx = morphTargets.Count - 1;
                    }

                    primitive.SetMorphTargetAccessors(morphTargetIdx, garmentDict);
                }

                if (vehicleDict.Count > 0)
                {
                    var morphTargetIdx = morphTargets.IndexOf("VehicleDamageSupport");
                    if (morphTargetIdx == -1)
                    {
                        morphTargets.Add("VehicleDamageSupport");
                        morphTargetIdx = morphTargets.Count - 1;
                    }

                    primitive.SetMorphTargetAccessors(morphTargetIdx, vehicleDict);
                }
            }

            var oldPos = bufferReader.BaseStream.Position;
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

            bufferReader.BaseStream.Position = oldPos;

            var buffer1 = ms1.ToArray();

            var acc1 = _modelRoot.CreateAccessor();
            var buff1 = _modelRoot.UseBufferView(buffer1, 0, buffer1.Length, 0, BufferMode.ELEMENT_ARRAY_BUFFER);
            acc1.SetIndexData(buff1, 0, (int)(uint)renderChunkInfo.NumIndices, IndexEncodingType.UNSIGNED_SHORT);
            primitive.SetIndexAccessor(acc1);
        }

        foreach (var (_, node) in result)
        {
            node.Mesh.Extras = JsonSerializer.SerializeToNode(new { targetNames = globalMorphTargets[node.Name].ToArray() });
        }

        return result;
    }

    private void TransformData(List<VertexElement> elementInfos)
    {
        var normals = elementInfos.FirstOrDefault(x => x.AttributeKey == "NORMAL");
        if (normals != null)
        {
            for (int i = 0; i < normals.Vertices.Count; i++)
            {
                var v = normals.Vertices[i];
                normals.Vertices[i] = new Vector4(v.X / 511f, v.Y / 511f, v.Z / 511f, -1f);
            }
        }

        var tangents = elementInfos.FirstOrDefault(x => x.AttributeKey == "TANGENT");
        if (tangents != null)
        {
            for (int i = 0; i < tangents.Vertices.Count; i++)
            {
                var v = tangents.Vertices[i];

                v = new Vector4(v.X / 511f, v.Y / 511f, v.Z / 511f, v.W);
                switch (v.W)
                {
                    case 1:
                        v.W = -1f;
                        break;

                    case 2:
                        v.W = 1f;
                        break;

                    default:
                        throw new NotSupportedException();
                }

                if (!v.IsValidTangent())
                {
                    v = v.SanitizeTangent();
                }

                tangents.Vertices[i] = v;
            }
        }

        var weights0 = elementInfos.FirstOrDefault(x => x.AttributeKey == "WEIGHTS_0");
        var weights1 = elementInfos.FirstOrDefault(x => x.AttributeKey == "WEIGHTS_1");

        if (weights0 != null && weights1 != null)
        {
            for (int j = 0; j < weights0.Vertices.Count; j++)
            {
                var vertex0 = weights0.Vertices[j];
                var vertex1 = weights1.Vertices[j];

                var sum = vertex0.X + vertex0.Y + vertex0.Z + vertex0.W + vertex1.X + vertex1.Y + vertex1.Z + vertex1.W;
                if (sum != 1F)
                {
                    weights0.Vertices[j] = vertex0 / sum;
                    weights1.Vertices[j] = vertex1 / sum;
                }
            }
        }
        else if (weights0 != null)
        {
            for (int j = 0; j < weights0.Vertices.Count; j++)
            {
                var vertex0 = weights0.Vertices[j];

                var sum = vertex0.X + vertex0.Y + vertex0.Z + vertex0.W;
                if (sum != 1F)
                {
                    weights0.Vertices[j] = vertex0 / sum;
                }
            }
        }
    }
}