using SharpGLTF.Schema2;
using System;
using System.Linq.Expressions;
using System.Numerics;
using SharpGLTF.Memory;
using WolvenKit.RED4.Archive.CR2W;
using WolvenKit.RED4.Archive.IO;
using WolvenKit.RED4.Types;
using static WolvenKit.RED4.Types.Enums;
using Vector2 = System.Numerics.Vector2;

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

        File.WriteAllBytes(@"C:\Dev\blob.bin", rendBlob.RenderBuffer.Buffer.GetBytes());

        var materials = ExtractMaterials(cMesh);
        ExtractMeshes(rendBlob, materials);

        _modelRoot.MergeBuffers();
        _modelRoot.SaveGLB(filePath);
    }

    private List<Material> ExtractMaterials(CMesh mesh)
    {
        var image1 = _modelRoot.CreateImage("vehicles_stickers_d01");
        image1.Content = new MemoryImage(@"C:\Users\Marcel\AppData\Roaming\REDModding\WolvenKit\Depot\base\vehicles\common\textures\vehicles_stickers_d01.png");

        var image2 = _modelRoot.CreateImage("vehicles_stickers_n01");
        image2.Content = new MemoryImage(@"C:\Users\Marcel\AppData\Roaming\REDModding\WolvenKit\Depot\base\vehicles\common\textures\vehicles_stickers_n01.png");

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

            var material = _modelRoot.CreateMaterial(materialNameStr);

            if (materialNameStr == "stickers")
            {
                material.InitializePBRSpecularGlossiness();
                var diffuse = material.FindChannel("Diffuse");
                if (diffuse.HasValue)
                {
                    diffuse.Value.SetTexture(0, image1);
                }

                var normal = material.FindChannel("Normal");
                if (normal.HasValue)
                {
                    normal.Value.SetTexture(0, image2);
                }
            }

            result.Add(material);
        }

        return result;
    }

    private class ElementInfo
    {
        private MemoryStream _stream = new MemoryStream();

        public string AttributeKey { get; }
        public GpuWrapApiVertexPackingePackingType Type { get; }
        public byte Size { get; }

        public DimensionType DimensionType { get; set; }
        public EncodingType EncodingType { get; set; }
        public bool Normalized { get; set; }

        public BinaryWriter Writer { get; }

        public ElementInfo(string attributeKey, GpuWrapApiVertexPackingePackingType type, byte size)
        {
            AttributeKey = attributeKey;
            Type = type;
            Size = GetSize(Type);
            Writer = new BinaryWriter(_stream);

            GetSettings();
        }

        public byte[] GetArray()
        {
            return _stream.ToArray();
        }

        private void GetSettings()
        {
            switch (Type)
            {
                case GpuWrapApiVertexPackingePackingType.PT_Short4N:
                    DimensionType = DimensionType.VEC3;
                    EncodingType = EncodingType.FLOAT;
                    Normalized = false;
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Float16_2:
                    DimensionType = DimensionType.VEC2;
                    EncodingType = EncodingType.FLOAT;
                    Normalized = false;
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Dec4:
                    DimensionType = DimensionType.VEC3;
                    EncodingType = EncodingType.FLOAT;
                    Normalized = false;
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Color:
                    DimensionType = DimensionType.VEC4;
                    EncodingType = EncodingType.FLOAT;
                    Normalized = false;
                    break;
                case GpuWrapApiVertexPackingePackingType.PT_Float4:
                    DimensionType = DimensionType.VEC3;
                    EncodingType = EncodingType.FLOAT;
                    Normalized = false;
                    break;
                default:
                    throw new Exception();
            }
        }
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

            var dict = new Dictionary<int, List<ElementInfo>>();

            var attrList = new List<string>();
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
                    lst = new List<ElementInfo>();
                    dict.Add(vertexLayoutElement.StreamIndex, lst);
                }

                string? attribute = null;
                switch ((GpuWrapApiVertexPackingePackingUsage)vertexLayoutElement.Usage)
                {
                    case GpuWrapApiVertexPackingePackingUsage.PS_Position:
                        attribute = "POSITION";
                        break;
                    case GpuWrapApiVertexPackingePackingUsage.PS_Normal:
                        attribute = "NORMAL";
                        break;
                    case GpuWrapApiVertexPackingePackingUsage.PS_Tangent:
                        attribute = "TANGENT";
                        break;
                    case GpuWrapApiVertexPackingePackingUsage.PS_TexCoord:
                        attribute = !attrList.Contains("TEXCOORD_0") ? "TEXCOORD_0" : "TEXCOORD_1";
                        break;
                    case GpuWrapApiVertexPackingePackingUsage.PS_Color:
                        attribute = !attrList.Contains("COLOR_0") ? "COLOR_0" : "COLOR_1";
                        break;
                    case GpuWrapApiVertexPackingePackingUsage.PS_VehicleDmgNormal:
                        attribute = "_VEHICLE_DAMAGE_NORMAL";
                        break;
                    case GpuWrapApiVertexPackingePackingUsage.PS_VehicleDmgPosition:
                        attribute = "_VEHICLE_DAMAGE_POSITION";
                        break;
                    case GpuWrapApiVertexPackingePackingUsage.PS_Invalid:
                    case GpuWrapApiVertexPackingePackingUsage.PS_SysPosition:
                    case GpuWrapApiVertexPackingePackingUsage.PS_Binormal:
                    case GpuWrapApiVertexPackingePackingUsage.PS_SkinIndices:
                    case GpuWrapApiVertexPackingePackingUsage.PS_SkinWeights:
                    case GpuWrapApiVertexPackingePackingUsage.PS_DestructionIndices:
                    case GpuWrapApiVertexPackingePackingUsage.PS_MultilayerPaint:
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
                        continue;
                }

                attrList.Add(attribute);
                lst.Add(new ElementInfo(attribute, vertexLayoutElement.Type, GetSize(vertexLayoutElement.Type)));
            }

            foreach (var (index, elementInfos) in dict)
            {
                var offset = renderChunkInfo.ChunkVertices.ByteOffsets[index];

                var sizes = new List<uint>();
                byte totalSize = 0;
                foreach (var elementInfo in elementInfos)
                {
                    sizes.Add(elementInfo.Size);
                    totalSize += elementInfo.Size;
                }

                if (renderChunkInfo.ChunkVertices.VertexLayout.SlotStrides[index] != totalSize)
                {
                    throw new Exception();
                }

                for (var j = 0; j < renderChunkInfo.NumVertices; j++)
                {
                    if (bufferReader.BaseStream.Position != offset + totalSize * j)
                    {
                        bufferReader.BaseStream.Position = offset + totalSize * j;
                    }

                    foreach (var elementInfo in elementInfos)
                    {
                        switch (elementInfo.Type)
                        {
                            case GpuWrapApiVertexPackingePackingType.PT_Short4N:
                                var x1 = (bufferReader.ReadInt16() / 32767f * rendMeshBlob.Header.QuantizationScale.X) + rendMeshBlob.Header.QuantizationOffset.X;
                                var y1 = (bufferReader.ReadInt16() / 32767f * rendMeshBlob.Header.QuantizationScale.Y) + rendMeshBlob.Header.QuantizationOffset.Y;
                                var z1 = (bufferReader.ReadInt16() / 32767f * rendMeshBlob.Header.QuantizationScale.Z) + rendMeshBlob.Header.QuantizationOffset.Z;
                                var w1 = (bufferReader.ReadInt16() / 32767f * rendMeshBlob.Header.QuantizationScale.W) + rendMeshBlob.Header.QuantizationOffset.W;

                                // Z up to Y up and LHCS to RHCS
                                elementInfo.Writer.Write(x1);
                                elementInfo.Writer.Write(z1);
                                elementInfo.Writer.Write(-y1);

                                break;
                            case GpuWrapApiVertexPackingePackingType.PT_Float16_2:
                                var x2 = (float)BitConverter.ToHalf(bufferReader.ReadBytes(2));
                                var y2 = 1F - (float)BitConverter.ToHalf(bufferReader.ReadBytes(2));

                                elementInfo.Writer.Write(x2);
                                elementInfo.Writer.Write(y2);

                                break;
                            case GpuWrapApiVertexPackingePackingType.PT_Dec4:
                                var u32 = bufferReader.ReadUInt32();

                                var x3 = Convert.ToSingle(u32 & 0x3ff);
                                var y3 = Convert.ToSingle((u32 >> 10) & 0x3ff);
                                var z3 = Convert.ToSingle((u32 >> 20) & 0x3ff);
                                var dequant = 1f / 1023f;
                                x3 = (x3 * 2 * dequant) - 1f;
                                y3 = (y3 * 2 * dequant) - 1f;
                                z3 = (z3 * 2 * dequant) - 1f;

                                elementInfo.Writer.Write(x3);
                                elementInfo.Writer.Write(z3);
                                elementInfo.Writer.Write(-y3);

                                break;
                            case GpuWrapApiVertexPackingePackingType.PT_Color:
                                elementInfo.Writer.Write(bufferReader.ReadByte() / 255f);
                                elementInfo.Writer.Write(bufferReader.ReadByte() / 255f);
                                elementInfo.Writer.Write(bufferReader.ReadByte() / 255f);
                                elementInfo.Writer.Write(bufferReader.ReadByte() / 255f);
                                break;
                            case GpuWrapApiVertexPackingePackingType.PT_Float4:
                                var x5 = bufferReader.ReadSingle() * 1; // 100?
                                var y5 = bufferReader.ReadSingle() * 1; // 100?
                                var z5 = bufferReader.ReadSingle() * 1; // 100?
                                var w5 = bufferReader.ReadSingle() * 100;
                                
                                elementInfo.Writer.Write(x5);
                                elementInfo.Writer.Write(z5);
                                elementInfo.Writer.Write(-y5);
                                //elementInfo.Writer.Write(bufferReader.ReadSingle());
                                break;
                            default:
                                throw new Exception();
                        }
                    }
                }

                var vehicleDict = new Dictionary<string, Accessor>();
                foreach (var elementInfo in elementInfos)
                {
                    var buffer = elementInfo.GetArray();

                    if (elementInfo.AttributeKey.StartsWith("_VEHICLE_DAMAGE_"))
                    {
                        var name = elementInfo.AttributeKey[16..];
                        var acc = _modelRoot.CreateAccessor();
                        var buff = _modelRoot.UseBufferView(buffer, 0, buffer.Length, 0, BufferMode.ARRAY_BUFFER);
                        acc.SetVertexData(buff, 0, renderChunkInfo.NumVertices, elementInfo.DimensionType, elementInfo.EncodingType, elementInfo.Normalized);
                        vehicleDict.Add(name, acc);
                    }
                    else
                    {
                        var acc = _modelRoot.CreateAccessor();
                        var buff = _modelRoot.UseBufferView(buffer, 0, buffer.Length, 0, BufferMode.ARRAY_BUFFER);
                        acc.SetVertexData(buff, 0, renderChunkInfo.NumVertices, elementInfo.DimensionType, elementInfo.EncodingType, elementInfo.Normalized);
                        primitive.SetVertexAccessor(elementInfo.AttributeKey, acc);
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