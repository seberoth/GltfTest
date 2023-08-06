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
using static WolvenKit.RED4.Types.Enums;
using EFileReadErrorCodes = WolvenKit.RED4.Archive.IO.EFileReadErrorCodes;

namespace GltfTest
{
    internal class Program
    {
        private const bool Export = false;
        private const bool Import = true;

        private static ILoggerService _loggerService = null!;
        private static IHashService _hashService = null!;
        private static Red4ParserService _parserService = null!;
        private static IArchiveManager _archiveManager = null!;

        static void Main(string[] args)
        {
            Init(@"C:\Games\Steam\steamapps\common\Cyberpunk 2077\bin\x64\Cyberpunk2077.exe");

            //Debug.Test(_archiveManager);

            if (Export)
            {
                var cr2w = GetFile(@"base\characters\head\player_base_heads\player_female_average\h0_002_pwa_a__young_096\h0_002_pwa_a__young_096.mesh");
                if (cr2w == null)
                {
                    return;
                }

                var test2 = new GltfConverter(cr2w, _archiveManager, @"C:\Users\Marcel\AppData\Roaming\REDModding\WolvenKit\Depot");
                test2.SaveGLB(@$"C:\Dev\Debug_new.glb", new WriteSettings { Validation = ValidationMode.Strict });
            }

            if (Import)
            {
                var test3 = new GltfImporter(@$"C:\Dev\Debug_new.glb");
                var mesh = test3.ToMesh();

                using var fs = File.Open(@$"C:\Dev\Debug_new.mesh", FileMode.OpenOrCreate, FileAccess.Write);
                using var cw = new CR2WWriter(fs);

                cw.WriteFile(new CR2WFile
                {
                    RootChunk = mesh
                });
            }
        }

        private static void Init(string gamePath)
        {
            _loggerService = new Logger();
            
            if (Export)
            {
                _hashService = new HashService();
                _parserService = new Red4ParserService(_hashService, _loggerService);
                _archiveManager = new ArchiveManager(_hashService, _parserService, _loggerService);
                _archiveManager.LoadGameArchives(new FileInfo(gamePath), false);
            }
            
            ExtensionsFactory.RegisterExtension<ModelRoot, VariantsRootExtension>("KHR_materials_variants");
            ExtensionsFactory.RegisterExtension<MeshPrimitive, VariantsPrimitiveExtension>("KHR_materials_variants");

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

        
    }
}