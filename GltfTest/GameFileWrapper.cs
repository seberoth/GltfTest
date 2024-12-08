using WolvenKit.Common;
using WolvenKit.RED4.Archive.CR2W;
using WolvenKit.RED4.Types;

namespace GltfTest;

public class GameFileWrapper
{
    protected readonly IArchiveManager _archiveManager;

    public string FileName { get; internal set; }
    public CR2WFile File { get; }
    public CResource Resource { get; }
    public bool IsEmbedded { get; private set; }

    public GameFileWrapper(CR2WFile file, IArchiveManager archiveManager)
    {
        _archiveManager = archiveManager;
        
        File = file;
        FileName = File.MetaData.FileName!;
        Resource = (CResource)File.RootChunk;
    }

    public GameFileWrapper(CR2WFile file, CResource resource, IArchiveManager archiveManager)
    {
        _archiveManager = archiveManager;
        
        File = file;
        FileName = File.MetaData.FileName!;
        Resource = resource;
    }

    public GameFileWrapper? GetResource(IRedRef redRef) => GetResource(redRef.DepotPath, redRef.Flags);

    public GameFileWrapper? GetResource(ResourcePath depotPath, InternalEnums.EImportFlags flags)
    {
        if (depotPath == ResourcePath.Empty)
        {
            return null;
        }

        if (flags == InternalEnums.EImportFlags.Embedded)
        {
            foreach (var embeddedFile in File.EmbeddedFiles)
            {
                if (embeddedFile.FileName == depotPath)
                {
                    return new GameFileWrapper(File, (CResource)embeddedFile.Content, _archiveManager) { FileName = embeddedFile.FileName, IsEmbedded = true };
                }
            }
        }

        var cr2w = _archiveManager.GetCR2WFile(depotPath, false, false);
        if (cr2w != null)
        {
            cr2w.MetaData.FileName = depotPath.GetResolvedText()!;
            return new GameFileWrapper(cr2w, _archiveManager);
        }

        return null;
    }

    public List<(ResourcePath, InternalEnums.EImportFlags)> GetImports()
    {
        var result = new List<(ResourcePath, InternalEnums.EImportFlags)>();
        if (!IsEmbedded)
        {
            foreach (var importInfo in File.Info.ImportInfo)
            {
                result.Add((File.Info.StringDict[importInfo.offset], (InternalEnums.EImportFlags)importInfo.flags));
            }
        }
        return result;
    }
}