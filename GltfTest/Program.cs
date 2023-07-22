using System.Text.Json;
using GltfTest.Extras;
using SharpGLTF.Schema2;
using SharpGLTF.Validation;
using WolvenKit.Common;
using WolvenKit.Common.Services;
using WolvenKit.Core.Interfaces;
using WolvenKit.RED4.Archive;
using WolvenKit.RED4.Archive.CR2W;
using WolvenKit.RED4.Archive.IO;
using WolvenKit.RED4.CR2W;
using WolvenKit.RED4.CR2W.Archive;
using WolvenKit.RED4.Types;
using static System.Net.Mime.MediaTypeNames;
using static WolvenKit.RED4.Types.Enums;
using EFileReadErrorCodes = WolvenKit.RED4.Archive.IO.EFileReadErrorCodes;

namespace GltfTest
{
    internal class Program
    {
        private static ILoggerService _loggerService = null!;
        private static IHashService _hashService = null!;
        private static Red4ParserService _parserService = null!;
        private static IArchiveManager _archiveManager = null!;

        static void Main(string[] args)
        {
            Init(@"C:\Games\Steam\steamapps\common\Cyberpunk 2077\bin\x64\Cyberpunk2077.exe");

            var cr2w = GetFile("base\\environment\\decoration\\unique\\side_quest\\sq021\\bbpod_a.mesh");
            if (cr2w == null)
            {
                return;
            }

            var test2 = new GltfConverter(cr2w, _archiveManager);
            test2.SaveGLB(@$"C:\Dev\Debug_new.glb", new WriteSettings { Validation = ValidationMode.Strict });
        }

        private static void Init(string gamePath)
        {
            _loggerService = new Logger();
            _hashService = new HashService();
            _parserService = new Red4ParserService(_hashService, _loggerService);
            _archiveManager = new ArchiveManager(_hashService, _parserService, _loggerService);
            _archiveManager.LoadGameArchives(new FileInfo(gamePath), false);
            
            ExtensionsFactory.RegisterExtension<Material, MaterialInstance>("CP_MaterialInstance");
        }

        private static CR2WFile? GetFile(ResourcePath path)
        {
            foreach (var fileEntry in _archiveManager.GetFiles())
            {
                if (fileEntry.Key != path)
                {
                    continue;
                }

                using var ms = new MemoryStream();
                fileEntry.Extract(ms);
                ms.Position = 0;

                using var reader = new CR2WReader(ms);
                if (reader.ReadFile(out var cr2w) != EFileReadErrorCodes.NoError)
                {
                    continue;
                }

                cr2w!.MetaData.FileName = path.GetResolvedText()!;

                return cr2w;
            }

            return null;
        }

        private static void Test()
        {
            var dict = new Dictionary<CName, string>();
            foreach (var archive in _archiveManager.Archives.Items)
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

                    if (fileEntry.Extension != ".mt")
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

                    if (cr2w!.RootChunk is CMaterialTemplate materialTemplate)
                    {
                        var materialType = ((ERenderMaterialType)materialTemplate.MaterialType).ToString();

                        foreach (var handle in materialTemplate.Parameters[2])
                        {
                            if (handle.Chunk is not { } parameter)
                            {
                                continue;
                            }

                            var typeName = parameter.GetType().Name;

                            if (dict.TryGetValue(parameter.ParameterName, out var val))
                            {
                                if (val != typeName)
                                {
                                    throw new Exception();
                                }
                            }
                            else
                            {
                                dict.Add(parameter.ParameterName, typeName);
                            }
                        }
                    }
                }

                ar.ReleaseFileHandle();
            }

            File.WriteAllText(@"C:\Dev\Parameters.json", JsonSerializer.Serialize(dict));
        }
    }
}