using SharpGLTF.Schema2;
using SharpGLTF.Memory;
using SharpGLTF.Validation;
using WolvenKit.RED4.Archive.CR2W;
using WolvenKit.RED4.Types;
using static WolvenKit.RED4.Types.Enums;
using Vector4 = WolvenKit.RED4.Types.Vector4;
using System.Numerics;

namespace GltfTest;

public class MeshConverter
{
    private ModelRoot _modelRoot = ModelRoot.CreateModel();

    public void ToGltf(CR2WFile cr2w, string filePath)
    {
        if (cr2w.RootChunk is not CMesh { RenderResourceBlob.Chunk: rendRenderMeshBlob rendBlob } cMesh)
        {
            return;
        }

        var materials = ExtractMaterials(cMesh);
        ExtractSkeleton(cMesh);
        ExtractMeshes(rendBlob, materials);

        _modelRoot.MergeBuffers();
        _modelRoot.SaveGLB(filePath, new WriteSettings { Validation = ValidationMode.Skip });
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

    private void ExtractSkeleton(CMesh mesh)
    {
        for (int i = 0; i < mesh.BoneNames.Count; i++)
        {
            var boneNode = _modelRoot.CreateLogicalNode();
            boneNode.Name = mesh.BoneNames[i];

            var boneRig = mesh.BoneRigMatrices[i];

            var localMatrix = new Matrix4x4();

            localMatrix.M11 = 1;
            localMatrix.M22 = 1;
            localMatrix.M33 = 1;
            localMatrix.M41 = boneRig.W.X;
            localMatrix.M42 = -boneRig.W.Y;
            localMatrix.M43 = boneRig.W.Z;
            localMatrix.M44 = boneRig.W.W;

            boneNode.LocalMatrix = localMatrix;
        }
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

        public DimensionType DimensionType { get; private set; }
        public EncodingType EncodingType { get; private set; }
        public bool Normalized { get; private set; }

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
                    break;
                case GpuWrapApiVertexPackingePackingUsage.PS_Normal:
                    ElementType = ElementType.Main;
                    AttributeKey = "NORMAL";
                    break;
                case GpuWrapApiVertexPackingePackingUsage.PS_Tangent:
                    ElementType = ElementType.Main;
                    AttributeKey = "TANGENT";
                    break;
                case GpuWrapApiVertexPackingePackingUsage.PS_TexCoord:
                    ElementType = ElementType.Main;
                    AttributeKey = $"TEXCOORD_{_element.UsageIndex}";
                    break;
                case GpuWrapApiVertexPackingePackingUsage.PS_Color:
                    ElementType = ElementType.Main;
                    AttributeKey = $"COLOR_{_element.UsageIndex}";
                    break;
                case GpuWrapApiVertexPackingePackingUsage.PS_VehicleDmgNormal:
                    ElementType = ElementType.VehicleDamage;
                    AttributeKey = "NORMAL";
                    break;
                case GpuWrapApiVertexPackingePackingUsage.PS_VehicleDmgPosition:
                    ElementType = ElementType.VehicleDamage;
                    AttributeKey = "POSITION";
                    break;
                case GpuWrapApiVertexPackingePackingUsage.PS_SkinIndices:
                    ElementType = ElementType.Main;
                    AttributeKey = $"JOINTS_{_element.UsageIndex}";
                    break;
                case GpuWrapApiVertexPackingePackingUsage.PS_SkinWeights:
                    ElementType = ElementType.Main;
                    AttributeKey = $"WEIGHTS_{_element.UsageIndex}";
                    break;
                case GpuWrapApiVertexPackingePackingUsage.PS_DestructionIndices:
                    ElementType = ElementType.Todo;
                    AttributeKey = $"???";
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
                    DimensionType = DimensionType.VEC3;
                    EncodingType = EncodingType.FLOAT;
                    Normalized = false;

                    DataSize = 8;
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Float16_2:
                    DimensionType = DimensionType.VEC2;
                    EncodingType = EncodingType.FLOAT;
                    Normalized = false;

                    DataSize = 4;
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Dec4:
                    DimensionType = DimensionType.VEC3;
                    EncodingType = EncodingType.FLOAT;
                    Normalized = false;

                    DataSize = 4;
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Color:
                    DimensionType = DimensionType.VEC4;
                    EncodingType = EncodingType.FLOAT;
                    Normalized = false;

                    DataSize = 4;
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Float4:
                    DimensionType = DimensionType.VEC3;
                    EncodingType = EncodingType.FLOAT;
                    Normalized = false;

                    DataSize = 16;
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_UByte4:
                    DimensionType = DimensionType.VEC4;
                    EncodingType = EncodingType.UNSIGNED_BYTE;
                    Normalized = false;

                    DataSize = 4;
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_UByte4N: // N => Normalized?
                    DimensionType = DimensionType.VEC4;
                    EncodingType = EncodingType.FLOAT;
                    Normalized = false;

                    DataSize = 4;
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_UShort2:
                    DimensionType = DimensionType.VEC2;
                    EncodingType = EncodingType.UNSIGNED_SHORT;
                    Normalized = false;

                    DataSize = 4;
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Float1:
                    DimensionType = DimensionType.SCALAR;
                    EncodingType = EncodingType.FLOAT;
                    Normalized = false;

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

        public void Read(BinaryReader reader, Vector4 quantizationScale, Vector4 quantizationOffset)
        {
            switch ((GpuWrapApiVertexPackingePackingType)_element.Type)
            {
                case GpuWrapApiVertexPackingePackingType.PT_Short4N:
                    var x1 = (reader.ReadInt16() / 32767f * quantizationScale.X) + quantizationOffset.X;
                    var y1 = (reader.ReadInt16() / 32767f * quantizationScale.Y) + quantizationOffset.Y;
                    var z1 = (reader.ReadInt16() / 32767f * quantizationScale.Z) + quantizationOffset.Z;
                    var w1 = (reader.ReadInt16() / 32767f * quantizationScale.W) + quantizationOffset.W;

                    // Z up to Y up and LHCS to RHCS
                    _writer.Write(x1);
                    _writer.Write(z1);
                    _writer.Write(-y1);
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Float16_2:
                    var x2 = (float)BitConverter.ToHalf(reader.ReadBytes(2));
                    var y2 = 1F - (float)BitConverter.ToHalf(reader.ReadBytes(2));

                    _writer.Write(x2);
                    _writer.Write(y2);
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Dec4:
                    var u32 = reader.ReadUInt32();

                    var x3 = Convert.ToSingle(u32 & 0x3ff);
                    var y3 = Convert.ToSingle((u32 >> 10) & 0x3ff);
                    var z3 = Convert.ToSingle((u32 >> 20) & 0x3ff);
                    var dequant = 1f / 1023f;
                    x3 = (x3 * 2 * dequant) - 1f;
                    y3 = (y3 * 2 * dequant) - 1f;
                    z3 = (z3 * 2 * dequant) - 1f;

                    _writer.Write(x3);
                    _writer.Write(z3);
                    _writer.Write(-y3);
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Color:
                    _writer.Write(reader.ReadByte() / 255f);
                    _writer.Write(reader.ReadByte() / 255f);
                    _writer.Write(reader.ReadByte() / 255f);
                    _writer.Write(reader.ReadByte() / 255f);
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Float4:
                    var x5 = reader.ReadSingle() * 1; // 100?
                    var y5 = reader.ReadSingle() * 1; // 100?
                    var z5 = reader.ReadSingle() * 1; // 100?
                    var w5 = reader.ReadSingle() * 100;

                    _writer.Write(x5);
                    _writer.Write(z5);
                    _writer.Write(-y5);
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_UByte4:
                    var x6 = reader.ReadByte();
                    var y6 = reader.ReadByte();
                    var z6 = reader.ReadByte();
                    var w6 = reader.ReadByte();

                    _writer.Write(x6);
                    _writer.Write(y6);
                    _writer.Write(z6);
                    _writer.Write(w6);
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_UByte4N:
                    var x7 = reader.ReadByte() / 255f;
                    var y7 = reader.ReadByte() / 255f;
                    var z7 = reader.ReadByte() / 255f;
                    var w7 = reader.ReadByte() / 255f;

                    _writer.Write(x7);
                    _writer.Write(y7);
                    _writer.Write(z7);
                    _writer.Write(w7);
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_UShort2:
                    var x8 = reader.ReadUInt16();
                    var y8 = reader.ReadUInt16();

                    _writer.Write(x8);
                    _writer.Write(y8);
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Float1:
                    var x9 = reader.ReadSingle();

                    _writer.Write(x9);
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

    private void ExtractMeshes(rendRenderMeshBlob rendMeshBlob, IList<Material> materials)
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
                node = _modelRoot.CreateLogicalNode();
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
                        elementInfo.Read(bufferReader, rendMeshBlob.Header.QuantizationScale, rendMeshBlob.Header.QuantizationOffset);
                    }
                }

                var vehicleDict = new Dictionary<string, Accessor>();
                foreach (var elementInfo in elementInfos)
                {
                    var buffer = elementInfo.GetArray();

                    var acc = _modelRoot.CreateAccessor();
                    var buff = _modelRoot.UseBufferView(buffer, 0, buffer.Length, 0, BufferMode.ARRAY_BUFFER);
                    acc.SetVertexData(buff, 0, renderChunkInfo.NumVertices, elementInfo.DimensionType, elementInfo.EncodingType, elementInfo.Normalized);

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