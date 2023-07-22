using WolvenKit.Common;
using WolvenKit.RED4.Archive.CR2W;
using WolvenKit.RED4.Archive.IO;
using WolvenKit.RED4.Types;
using EFileReadErrorCodes = WolvenKit.RED4.Archive.IO.EFileReadErrorCodes;

namespace GltfTest;

public class GameFileWrapper
{
    protected readonly IArchiveManager _archiveManager;
    protected readonly CR2WFile _file;

    public string? FileName { get; set; }
    public CResource Resource { get; }

    public GameFileWrapper(CR2WFile file, IArchiveManager archiveManager)
    {
        _archiveManager = archiveManager;
        _file = file;

        FileName = _file.MetaData.FileName;
        Resource = (CResource)_file.RootChunk;
    }

    public GameFileWrapper(CR2WFile file, CResource resource, IArchiveManager archiveManager)
    {
        _archiveManager = archiveManager;
        _file = file;

        FileName = _file.MetaData.FileName;
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
            foreach (var embeddedFile in _file.EmbeddedFiles)
            {
                if (embeddedFile.FileName == depotPath)
                {
                    return new GameFileWrapper(_file, (CResource)embeddedFile.Content, _archiveManager);
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
                    return new GameFileWrapper(cr2w!, _archiveManager);
                }
            }
        }

        return null;
    }
}