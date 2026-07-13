using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia.Collections;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Dock.Model.Mvvm.Controls;
using DynamicData;
using DynamicData.Binding;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UABEANext4.AssetWorkspace;
using UABEANext4.Logic;
using UABEANext4.Logic.Configuration;

namespace UABEANext4.ViewModels.Documents;

public record ReferenceSearchResult(string ReferenceFieldPath, AssetInst Asset);

public partial class ReferencesDocumentViewModel : Document
{
    const string TOOL_TITLE = "References Document";

    public Workspace Workspace { get; }
    public bool LoadContainers { get; }

    public List<AssetInst> SelectedItems { get; set; } = [];

    public AssetInst? Referencee { get; set; } = null;
    public ReadOnlyObservableCollection<ReferenceSearchResult> Items { get; set; } = new([]);

    [ObservableProperty]
    private DataGridCollectionView _collectionView = new(new List<object>());

    [ObservableProperty]
    private bool _isBusy;

    public event Action<List<AssetInst>>? SetSelectedItemsAction;

    private IDisposable? _disposableLastList;
    private CancellationTokenSource? _loadCtSrc;

    [Obsolete("This constructor is for designer use only and should not be used directly", true)]
    public ReferencesDocumentViewModel()
    {
        Workspace = new();
        LoadContainers = true;

        Id = TOOL_TITLE.Replace(" ", "");
        Title = TOOL_TITLE;
    }

    public ReferencesDocumentViewModel(Workspace workspace, bool loadContainers)
    {
        Workspace = workspace;
        LoadContainers = loadContainers;

        Id = TOOL_TITLE.Replace(" ", "");
        Title = TOOL_TITLE;

        WeakReferenceMessenger.Default.Register<WorkspaceClosingMessage>(this, (r, m) => _ = OnWorkspaceClosing(r, m));
    }

    public void OnAssetOpened(List<AssetInst> assets)
    {
        if (assets.Count > 0)
        {
            WeakReferenceMessenger.Default.Send(new AssetsSelectedMessage([assets[0]]));
        }

        SelectedItems = assets;
    }

    public async Task Load(AssetInst referencee)
    {
        if (Workspace == null)
            return;

        _disposableLastList?.Dispose();

        var sourceList = new SourceList<ReferenceSearchResult>();

        _loadCtSrc?.Cancel();
        _loadCtSrc = new CancellationTokenSource();
        var loadCt = _loadCtSrc.Token;
        IsBusy = true;
        try
        {
            await Task.Run(() =>
            {
                var contToolMan = new ContainerToolManager(Workspace);
                var referencingFiles = Workspace.Manager.Files
                    .Where(wsfile => wsfile.file.Metadata.Externals.Any(ext => Path.GetFileName(ext.PathName) == referencee.FileName))
                    .ToList();
                foreach (var refFile in referencingFiles)
                {
                    foreach (var asset in refFile.file.AssetInfos)
                    {
                        loadCt.ThrowIfCancellationRequested();

                        var fieldsToProcess = new Queue<(string fullPath, AssetTypeValueField field)>();
                        fieldsToProcess.Enqueue(("Base", Workspace.Manager.GetBaseField(refFile, asset)));
                        while (fieldsToProcess.TryDequeue(out var fieldWithPath))
                        {
                            if (fieldWithPath.field.TypeName.StartsWith("PPtr<"))
                            {
                                var pptr = AssetPPtr.FromField(fieldWithPath.field);
                                pptr.SetFilePathFromFile(Workspace.Manager, refFile);

                                if (Path.GetFileName(pptr.FilePath) == referencee.FileName && pptr.PathId == referencee.PathId)
                                {
                                    var inst = new AssetInst(refFile, asset);
                                    inst.AssetName = Workspace.Namer.GetAssetName(inst, true, ConfigurationManager.Settings.ListingNameLength);
                                    if (LoadContainers && contToolMan.TryGetContainerTool(refFile, out var contTool))
                                    {
                                        var assetPtr = new AssetPPtr(refFile.path, inst.PathId);
                                        var path = contTool.GetContainerPath(assetPtr);
                                        if (path is not null)
                                        {
                                            inst.DisplayContainer = path;
                                        }
                                    }
                                    sourceList.Add(new ReferenceSearchResult(fieldWithPath.fullPath, inst));
                                }
                            }
                            else if (fieldWithPath.field.Children.Count > 0)
                            {
                                var val = fieldWithPath.field.Value;
                                var children = fieldWithPath.field.Children;
                                for (int i = 0; i < children.Count; i++)
                                {
                                    AssetTypeValueField child = children[i];
                                    string nextPathName;
                                    if (val != null && val.ValueType == AssetValueType.Array)
                                    {
                                        nextPathName = $"{fieldWithPath.fullPath}[{i}]";
                                    }
                                    else
                                    {
                                        nextPathName = $"{fieldWithPath.fullPath}.{child.FieldName}";
                                    }
                                    fieldsToProcess.Enqueue((nextPathName, child));
                                }
                            }
                        }
                    }
                }
            }, loadCt);

            var observableList = sourceList
                .Connect();
            _disposableLastList = observableList.Bind(out var items).Subscribe();
            Items = items;
            Referencee = referencee;

            CollectionView = new DataGridCollectionView(Items);
        } 
        catch (OperationCanceledException)
        {
            sourceList.Clear();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task Unload()
    {
        _disposableLastList?.Dispose();
        _loadCtSrc?.Cancel();
    }

    private async Task OnWorkspaceClosing(object recipient, WorkspaceClosingMessage message)
    {
        await Unload();
    }
}
