using SharpGLTF.Schema2;
using WolvenKit.RED4.Archive.IO;

namespace GltfTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var fileName = "sentry_gun_blockout";

            // var test1 = ModelRoot.Load(@$"C:\Dev\{fileName}_org.glb");

            var fs = File.Open(@$"C:\Dev\{fileName}.mesh", FileMode.Open, FileAccess.Read, FileShare.Read);
            var reader = new CR2WReader(fs);

            if (reader.ReadFile(out var cr2w) != EFileReadErrorCodes.NoError)
            {
                return;
            }

            var test2 = new MeshConverter();
            test2.ToGltf(cr2w!, @$"C:\Dev\{fileName}_new.glb");
        }
    }
}