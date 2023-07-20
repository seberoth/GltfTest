using System.Text.Json;
using SharpGLTF.Schema2;
using SharpGLTF.Validation;
using WolvenKit.Common;
using WolvenKit.Common.Services;
using WolvenKit.Core.Interfaces;
using WolvenKit.RED4.Archive.IO;
using WolvenKit.RED4.CR2W;
using WolvenKit.RED4.CR2W.Archive;
using WolvenKit.RED4.Types;
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

            var fileName = "sentry_gun_blockout";

            var test1 = ModelRoot.Load(@$"C:\Dev\{fileName}.glb");

            var fs = File.Open(@$"C:\Users\Marcel\Documents\CP77Dev\Test\source\archive\base\characters\main_npc\judy\t0_001_wa_body__judy.mesh", FileMode.Open, FileAccess.Read, FileShare.Read);
            //var fs = File.Open(@$"C:\Users\Marcel\Documents\CP77Dev\Test\source\archive\base\characters\head\player_base_heads\player_man_average\h0_000_pma_c__basehead\hb_000_pma_c__basehead.mesh", FileMode.Open, FileAccess.Read, FileShare.Read);
            var reader = new CR2WReader(fs);

            if (reader.ReadFile(out var cr2w) != EFileReadErrorCodes.NoError)
            {
                return;
            }

            var test2 = new GltfConverter(cr2w!, _archiveManager);
            test2.SaveGLB(@$"C:\Dev\{fileName}_new.glb", new WriteSettings { Validation = ValidationMode.Strict });
        }

        private static void Init(string gamePath)
        {
            _loggerService = new Logger();
            _hashService = new HashService();
            _parserService = new Red4ParserService(_hashService, _loggerService);
            _archiveManager = new ArchiveManager(_hashService, _parserService, _loggerService);
            _archiveManager.LoadGameArchives(new FileInfo(gamePath), false);
        }
    }
}