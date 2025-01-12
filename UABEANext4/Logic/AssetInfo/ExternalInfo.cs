using AssetsTools.NET;
using AssetsTools.NET.Extra;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using DynamicData;
using DynamicData.Binding;
using System;
using System.Collections.ObjectModel;
using UABEANext4.AssetWorkspace;
using UABEANext4.Services;
using UABEANext4.ViewModels;
using UABEANext4.ViewModels.Dialogs;

namespace UABEANext4.Logic.AssetInfo;
public partial class ExternalInfo : ViewModelBase
{
    public ObservableCollection<AssetsFileExternal> Externals { get; set; } = [];
    public ReadOnlyObservableCollection<string> ExternalsDisplay { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAssetSelected))]
    [NotifyPropertyChangedFor(nameof(CanSelectedAssetMoveUp))]
    [NotifyPropertyChangedFor(nameof(CanSelectedAssetMoveDown))]
    public int _selectedExternIndex = -1;

    public bool IsAssetSelected => SelectedExtern != null;
    public bool CanSelectedAssetMoveUp => SelectedExternIndex != 0;
    public bool CanSelectedAssetMoveDown => SelectedExternIndex != Externals.Count - 1;
    public AssetsFileExternal? SelectedExtern => SelectedExternIndex != -1 ? Externals[SelectedExternIndex] : null;

    public ExternalInfo(Workspace workspace, AssetsFileInstance fileInst)
    {
        var externals = fileInst.file.Metadata.Externals;
        foreach (var external in externals)
        {
            Externals.Add(external);
        }

        Externals
            .ToObservableChangeSet()
            .Transform(ExternalsNameTransFac)
            .Bind(out var externalsItems)
            .DisposeMany()
            .Subscribe();

        ExternalsDisplay = externalsItems!;
    }

    private static string ExternalsNameTransFac(AssetsFileExternal dep, int idx)
    {
        if (dep.PathName != string.Empty)
            return $"{idx} - {dep.PathName}";
        else
            return $"{idx} - {dep.Guid}";
    }

    public async void Add_Click()
    {
        var dialogService = Ioc.Default.GetRequiredService<IDialogService>();
        var newExtern = await dialogService.ShowDialog(new AddExternalViewModel(null));
        if (newExtern == null)
        {
            return;
        }

        Externals.Add(newExtern);
    }

    public async void Edit_Click()
    {
        if (SelectedExtern == null)
        {
            return;
        }

        var dialogService = Ioc.Default.GetRequiredService<IDialogService>();
        var newExtern = await dialogService.ShowDialog(new AddExternalViewModel(SelectedExtern));
        if (newExtern == null)
        {
            return;
        }

        int oldSelectedIndex = SelectedExternIndex;
        Externals[SelectedExternIndex] = newExtern;
        SelectedExternIndex = oldSelectedIndex;
    }

    public void Remove_Click()
    {
        Externals.RemoveAt(SelectedExternIndex);
    }

    // we can't use .Move() because it doesn't update indices.
    // swapping only two items is fine because moving only
    // once up or down means no other indices update.
    public void MoveUp_Click()
    {
        if (SelectedExternIndex != -1 && SelectedExternIndex > 0)
        {
            int index = SelectedExternIndex;
            AssetsFileExternal newExternal = Externals[index - 1];
            AssetsFileExternal oldExternal = Externals[index];
            Externals[index - 1] = oldExternal;
            Externals[index] = newExternal;
            SelectedExternIndex = index - 1;
        }
    }

    public void MoveDown_Click()
    {
        if (SelectedExternIndex != -1 && SelectedExternIndex < Externals.Count - 1)
        {
            int index = SelectedExternIndex;
            AssetsFileExternal newExternal = Externals[index + 1];
            AssetsFileExternal oldExternal = Externals[index];
            Externals[index + 1] = oldExternal;
            Externals[index] = newExternal;
            SelectedExternIndex = index + 1;
        }
    }

    private ExternalInfo()
    {
        ExternalsDisplay = new ReadOnlyObservableCollection<string>(new ObservableCollection<string>());
    }

    public static ExternalInfo Empty { get; } = new()
    {
        Externals = [],
        ExternalsDisplay = new ReadOnlyObservableCollection<string>(new ObservableCollection<string>())
    };
}
