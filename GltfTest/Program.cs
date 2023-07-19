using SharpGLTF.Schema2;
using SharpGLTF.Validation;
using WolvenKit.RED4.Archive.IO;
using WolvenKit.RED4.Types;

namespace GltfTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var fileName = "sentry_gun_blockout";

            var test1 = ModelRoot.Load(@$"C:\Dev\{fileName}.glb");

            var fs = File.Open(@$"C:\Users\Marcel\Documents\CP77Dev\Test\source\archive\base\characters\main_npc\judy\t0_001_wa_body__judy.mesh", FileMode.Open, FileAccess.Read, FileShare.Read);
            //var fs = File.Open(@$"C:\Users\Marcel\Documents\CP77Dev\Test\source\archive\base\characters\head\player_base_heads\player_man_average\h0_000_pma_c__basehead\hb_000_pma_c__basehead.mesh", FileMode.Open, FileAccess.Read, FileShare.Read);
            var reader = new CR2WReader(fs);

            if (reader.ReadFile(out var cr2w) != EFileReadErrorCodes.NoError)
            {
                return;
            }

            var test2 = new GltfConverter();

            if (cr2w!.RootChunk is CMesh cMesh)
            {
                var model = test2.ToGltf(cMesh);
                model!.SaveGLB(@$"C:\Dev\{fileName}_new.glb", new WriteSettings { Validation = ValidationMode.Strict });
            }

            if (cr2w!.RootChunk is MorphTargetMesh morphTargetMesh)
            {
                var model = test2.ToGltf(morphTargetMesh);
                model!.SaveGLB(@$"C:\Dev\{fileName}_new.glb", new WriteSettings { Validation = ValidationMode.Strict });
            }
        }
    }
}