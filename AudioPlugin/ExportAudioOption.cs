using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia.Platform.Storage;
using Fmod5Sharp;
using Fmod5Sharp.FmodTypes;
using System.Text;
using UABEANext4.AssetWorkspace;
using UABEANext4.Plugins;
using UABEANext4.Util;

namespace AudioPlugin;

public class ExportAudioOption : IUavPluginOption
{
    public string Name => "Export AudioClip";

    public string Description => "Exports AudioClips to their respective format";

    public UavPluginMode Options => UavPluginMode.Export;

    public Task<bool> SupportsSelection(Workspace workspace, UavPluginMode mode, IList<AssetInst> selection)
    {
        if (mode != UavPluginMode.Export)
        {
            return Task.FromResult(false);
        }

        var typeId = (int)AssetClassID.AudioClip;
        return Task.FromResult(selection.All(a => a.TypeId == typeId));
    }

    public async Task<bool> Execute(Workspace workspace, IUavPluginFunctions funcs, UavPluginMode mode, IList<AssetInst> selection)
    {
        if (selection.Count > 1)
        {
            return await BatchExport(workspace, funcs, selection);
        }
        else
        {
            return await SingleExport(workspace, funcs, selection);
        }
    }

    public async Task<bool> BatchExport(Workspace workspace, IUavPluginFunctions funcs, IList<AssetInst> selection)
    {
        var dir = await funcs.ShowOpenFolderDialog(new FolderPickerOpenOptions()
        {
            Title = "Select export directory"
        });

        if (dir == null)
        {
            return false;
        }

        StringBuilder errorBuilder = new StringBuilder();
        foreach (AssetInst asset in selection)
        {
            string errorAssetName = $"{Path.GetFileName(asset.FileInstance.path)}/{asset.PathId}";
            AssetTypeValueField? baseField = workspace.GetBaseField(asset);
            if (baseField == null)
            {
                errorBuilder.AppendLine($"[{errorAssetName}]: failed to read");
                continue;
            }

            string name = baseField["m_Name"].AsString;
            name = PathUtils.ReplaceInvalidPathChars(name);

            CompressionFormat compressionFormat = (CompressionFormat)baseField["m_CompressionFormat"].AsInt;
            string extension = GetExtension(compressionFormat);
            string file = Path.Combine(dir, $"{name}-{Path.GetFileName(asset.FileInstance.path)}-{asset.PathId}.{extension}");

            string ResourceSource = baseField["m_Resource.m_Source"].AsString;
            ulong ResourceOffset = baseField["m_Resource.m_Offset"].AsULong;
            ulong ResourceSize = baseField["m_Resource.m_Size"].AsULong;

            if (!GetAudioBytes(asset, ResourceSource, ResourceOffset, ResourceSize, out byte[] resourceData))
            {
                continue;
            }

            if (!FsbLoader.TryLoadFsbFromByteArray(resourceData, out FmodSoundBank? bank) || bank == null)
            {
                continue;
            }

            List<FmodSample> samples = bank.Samples;
            samples[0].RebuildAsStandardFileFormat(out byte[]? sampleData, out string? sampleExtension);
            if (sampleData == null)
            {
                continue;
            }

            if (sampleExtension?.ToLowerInvariant() == "wav")
            {
                // since fmod5sharp gives us malformed wav data, we have to correct it
                FixWAV(ref sampleData);
            }

            File.WriteAllBytes(file, sampleData);
        }

        if (errorBuilder.Length > 0)
        {
            string[] firstLines = errorBuilder.ToString().Split('\n').Take(20).ToArray();
            string firstLinesStr = string.Join('\n', firstLines);
            await funcs.ShowMessageDialog("Error", firstLinesStr);
        }

        return true;
    }

    public async Task<bool> SingleExport(Workspace workspace, IUavPluginFunctions funcs, IList<AssetInst> selection)
    {
        AssetInst asset = selection[0];
        AssetTypeValueField? baseField = workspace.GetBaseField(asset);
        if (baseField == null)
        {
            await funcs.ShowMessageDialog("Error", "Failed to read AudioClip");
            return false;
        }

        CompressionFormat compressionFormat = (CompressionFormat)baseField["m_CompressionFormat"].AsInt;

        string assetName = PathUtils.ReplaceInvalidPathChars(baseField["m_Name"].AsString);
        string extension = GetExtension(compressionFormat);
        var filePath = await funcs.ShowSaveFileDialog(new FilePickerSaveOptions()
        {
            Title = "Save audioclip",
            FileTypeChoices = new List<FilePickerFileType>()
            {
                new FilePickerFileType($"{extension.ToUpper()} file (*.{extension})") { Patterns = new List<string>() { "*." + extension } }
            },
            SuggestedFileName = AssetNameUtils.GetAssetFileName(asset, assetName, string.Empty),
            DefaultExtension = extension
        });

        if (filePath == null)
        {
            return false;
        }

        string resourceSource = baseField["m_Resource.m_Source"].AsString;
        ulong resourceOffset = baseField["m_Resource.m_Offset"].AsULong;
        ulong resourceSize = baseField["m_Resource.m_Size"].AsULong;

        if (!GetAudioBytes(asset, resourceSource, resourceOffset, resourceSize, out byte[] resourceData))
        {
            return false;
        }

        if (!FsbLoader.TryLoadFsbFromByteArray(resourceData, out FmodSoundBank? bank) || bank == null)
        {
            return false;
        }

        List<FmodSample> samples = bank.Samples;
        samples[0].RebuildAsStandardFileFormat(out byte[]? sampleData, out string? sampleExtension);
        if (sampleData == null)
        {
            return false;
        }

        if (sampleExtension?.ToLowerInvariant() == "wav")
        {
            // since fmod5sharp gives us malformed wav data, we have to correct it
            FixWAV(ref sampleData);
        }

        File.WriteAllBytes(filePath, sampleData);

        return true;
    }

    private static void FixWAV(ref byte[] wavData)
    {
        int origLength = wavData.Length;
        // remove ExtraParamSize field from fmt subchunk
        for (int i = 36; i < origLength - 2; i++)
        {
            wavData[i] = wavData[i + 2];
        }
        Array.Resize(ref wavData, origLength - 2);
        // write ChunkSize to RIFF chunk
        byte[] riffHeaderChunkSize = BitConverter.GetBytes(wavData.Length - 8);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(riffHeaderChunkSize);
        }
        riffHeaderChunkSize.CopyTo(wavData, 4);
        // write ChunkSize to fmt chunk
        byte[] fmtHeaderChunkSize = BitConverter.GetBytes(16); // it is always 16 for pcm data, which this always
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(fmtHeaderChunkSize);
        }
        fmtHeaderChunkSize.CopyTo(wavData, 16);
        // write ChunkSize to data chunk
        byte[] dataHeaderChunkSize = BitConverter.GetBytes(wavData.Length - 44);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(dataHeaderChunkSize);
        }
        dataHeaderChunkSize.CopyTo(wavData, 40);
    }

    private static string GetExtension(CompressionFormat format)
    {
        return format switch
        {
            CompressionFormat.PCM => "wav",
            CompressionFormat.Vorbis => "ogg",
            CompressionFormat.ADPCM => "wav",
            CompressionFormat.MP3 => "mp3",
            CompressionFormat.VAG => "dat", // proprietary
            CompressionFormat.HEVAG => "dat", // proprietary
            CompressionFormat.XMA => "dat", // proprietary
            CompressionFormat.AAC => "aac",
            CompressionFormat.GCADPCM => "wav", // nintendo adpcm
            CompressionFormat.ATRAC9 => "dat", // proprietary
            _ => ""
        };
    }

    private bool GetAudioBytes(AssetInst asset, string filepath, ulong offset, ulong size, out byte[] audioData)
    {
        if (string.IsNullOrEmpty(filepath))
        {
            audioData = Array.Empty<byte>();
            return false;
        }

        if (asset.FileInstance.parentBundle != null)
        {
            // read from parent bundle archive
            // some versions apparently don't use archive:/
            string searchPath = filepath;
            if (searchPath.StartsWith("archive:/"))
                searchPath = searchPath.Substring(9);

            searchPath = Path.GetFileName(searchPath);

            AssetBundleFile bundle = asset.FileInstance.parentBundle.file;

            AssetsFileReader reader = bundle.DataReader;
            List<AssetBundleDirectoryInfo> dirInf = bundle.BlockAndDirInfo.DirectoryInfos;
            for (int i = 0; i < dirInf.Count; i++)
            {
                AssetBundleDirectoryInfo info = dirInf[i];
                if (info.Name == searchPath)
                {
                    lock (bundle.DataReader)
                    {
                        reader.Position = info.Offset + (long)offset;
                        audioData = reader.ReadBytes((int)size);
                    }
                    return true;
                }
            }
        }

        string assetsFileDirectory = Path.GetDirectoryName(asset.FileInstance.path)!;
        if (asset.FileInstance.parentBundle != null)
        {
            // inside of bundles, the directory contains the bundle path. let's get rid of that.
            assetsFileDirectory = Path.GetDirectoryName(assetsFileDirectory)!;
        }

        string resourceFilePath = Path.Combine(assetsFileDirectory, filepath);

        if (File.Exists(resourceFilePath))
        {
            // read from file
            AssetsFileReader reader = new AssetsFileReader(resourceFilePath);
            reader.Position = (long)offset;
            audioData = reader.ReadBytes((int)size);
            return true;
        }

        // if that fails, check current directory
        string resourceFileName = Path.Combine(assetsFileDirectory, Path.GetFileName(filepath));

        if (File.Exists(resourceFileName))
        {
            // read from file
            AssetsFileReader reader = new AssetsFileReader(resourceFileName);
            reader.Position = (long)offset;
            audioData = reader.ReadBytes((int)size);
            return true;
        }

        audioData = Array.Empty<byte>();
        return false;
    }
}
