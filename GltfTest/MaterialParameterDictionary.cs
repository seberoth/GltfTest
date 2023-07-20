using System.Diagnostics.CodeAnalysis;
using WolvenKit.RED4.Types;

namespace GltfTest;

public class MaterialParameterDictionary : Dictionary<CName, IRedType>
{
    public bool TryGetValue<T1>(CName key, [NotNullWhen(true)] out T1? value) where T1 : IRedType
    {
        if (base.TryGetValue(key, out var tmp) && tmp is T1 tmp2)
        {
            value = tmp2;
            return true;
        }

        value = default(T1);
        return false;
    }
}