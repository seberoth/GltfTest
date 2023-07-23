using DynamicData;
using WolvenKit.Common;
using WolvenKit.RED4.Archive.CR2W;
using WolvenKit.RED4.Archive.IO;
using WolvenKit.RED4.Types;
using EFileReadErrorCodes = WolvenKit.RED4.Archive.IO.EFileReadErrorCodes;

namespace GltfTest;

public class GameFileWrapper
{
    protected readonly IArchiveManager _archiveManager;
    private List<CName> _namesList = new();

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

        foreach (var fileEntry in _archiveManager.GetFiles())
        {
            if (fileEntry.Key == depotPath)
            {
                using var ms = new MemoryStream();
                fileEntry.Extract(ms);
                ms.Position = 0;

                using var cr = new CR2WReader(ms);
                if (cr.ReadFile(out var cr2w) == EFileReadErrorCodes.NoError)
                {
                    cr2w!.MetaData.FileName = depotPath.GetResolvedText()!;
                    return new GameFileWrapper(cr2w, _archiveManager);
                }
            }
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