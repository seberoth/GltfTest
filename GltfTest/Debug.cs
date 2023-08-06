using System.Text.Json;
using WolvenKit.Common;
using static WolvenKit.RED4.Types.Enums;
using WolvenKit.RED4.Archive.IO;
using WolvenKit.RED4.Archive;
using WolvenKit.RED4.Types;
using EFileReadErrorCodes = WolvenKit.RED4.Archive.IO.EFileReadErrorCodes;

namespace GltfTest;

public static class Debug
{
    public static void Test(IArchiveManager archiveManager)
    {
        var dict = new Dictionary<EMaterialVertexFactory, HashSet<string>>();
        foreach (var archive in archiveManager.Archives.Items)
        {
            if (archive is not Archive ar)
            {
                continue;
            }

            foreach (var gameFile in archive.Files.Values)
            {
                if (gameFile is not FileEntry fileEntry)
                {
                    continue;
                }

                if (fileEntry.Extension != ".mesh")
                {
                    continue;
                }

                using var ms = new MemoryStream();
                ar.ExtractFile(fileEntry, ms);
                ms.Position = 0;

                using var reader = new CR2WReader(ms);
                if (reader.ReadFile(out var cr2w) != EFileReadErrorCodes.NoError)
                {
                    continue;
                }

                if (cr2w!.RootChunk is not CMesh mesh || mesh.RenderResourceBlob?.Chunk is not rendRenderMeshBlob rendBlob)
                {
                    continue;
                }

                foreach (var renderChunkInfo in rendBlob.Header.RenderChunkInfos)
                {
                    var enm = (EMaterialVertexFactory)(byte)renderChunkInfo.VertexFactory;

                    if (!dict.ContainsKey(enm))
                    {
                        dict.Add(enm, new HashSet<string>());
                    }
                    dict[enm].Add(fileEntry.FileName);
                }
            }

            ar.ReleaseFileHandle();
        }

        File.WriteAllText(@"C:\Dev\VertexFactory.json", JsonSerializer.Serialize(dict));
    }
}