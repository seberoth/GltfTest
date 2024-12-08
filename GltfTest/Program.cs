using GltfTest.Extras;
using SharpGLTF.Schema2;
using SharpGLTF.Validation;
using WolvenKit.Common;
using WolvenKit.Common.Services;
using WolvenKit.Core.Interfaces;
using WolvenKit.Core.Services;
using WolvenKit.RED4.Archive.CR2W;
using WolvenKit.RED4.Archive.IO;
using WolvenKit.RED4.CR2W;
using WolvenKit.RED4.CR2W.Archive;

namespace GltfTest
{
    internal class Program
    {
        private const bool Export = true;
        private const bool Import = true;

        private static ILoggerService _loggerService = null!;
        private static IHashService _hashService = null!;
        private static IHookService _hookService = null!;
        private static IProgressService<double> _progressService = null!;
        private static Red4ParserService _parserService = null!;
        private static IArchiveManager _archiveManager = null!;

        static void Main(string[] args)
        {
            Init(@"F:\Games\Steam\steamapps\common\Cyberpunk 2077\bin\x64\Cyberpunk2077.exe");

            //Debug.Test(_archiveManager);

            if (Export)
            {
                var cr2w = _archiveManager.GetCR2WFile(@"base\characters\common\player_base_bodies\player_female_average\arms_hq\a0_000_pwa_base_hq__r.mesh", false, false);
                if (cr2w == null)
                {
                    return;
                }

                var test2 = new GltfConverter(cr2w, _archiveManager, @"C:\Users\Seberoth\AppData\Roaming\REDModding\WolvenKit\Depot");
                test2.SaveGLB(@$"C:\Dev\Debug_new.glb", new WriteSettings { Validation = ValidationMode.Strict });
            }

            if (Import)
            {
                var test3 = new GltfImporter(@$"C:\Dev\Debug_new.glb");
                var mesh = test3.ToMesh();

                using var fs = File.Open(@$"C:\Dev\a0_000_pwa_base_hq__r.mesh", FileMode.OpenOrCreate, FileAccess.Write);
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
                _hookService = new HookService();
                _progressService = new ProgressService<double>();
                _parserService = new Red4ParserService(_hashService, _loggerService, _hookService);
                _archiveManager = new ArchiveManager(_hashService, _parserService, _loggerService, _progressService);
                _archiveManager.LoadGameArchives(new FileInfo(gamePath));
            }
            
            ExtensionsFactory.RegisterExtension<ModelRoot, VariantsRootExtension>("KHR_materials_variants", root => new VariantsRootExtension(root));
            ExtensionsFactory.RegisterExtension<MeshPrimitive, VariantsPrimitiveExtension>("KHR_materials_variants", root => new VariantsPrimitiveExtension(root));

            ExtensionsFactory.RegisterExtension<Material, MaterialInstance>("CP_MaterialInstance", root => new MaterialInstance(root));
        }
    }
}