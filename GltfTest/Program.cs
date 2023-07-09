using SharpGLTF.Schema2;
using WolvenKit.RED4.Archive.IO;

namespace GltfTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var test1 = ModelRoot.Load(@"C:\Dev\untitled.glb");

            var fs = File.Open(@"C:\Dev\v_sport1_quadra_turbo__ext01_trunk_01.mesh", FileMode.Open, FileAccess.Read, FileShare.Read);
            var reader = new CR2WReader(fs);

            if (reader.ReadFile(out var cr2w) != EFileReadErrorCodes.NoError)
            {
                return;
            }

            var test2 = new MeshConverter();
            test2.ToGltf(cr2w!, @"C:\Dev\v_sport1_quadra_turbo__ext01_trunk_01.glb");
        }
    }
}