using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AvaloniaEdit.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using UABEANext4.AssetWorkspace;
using UABEANext4.Interfaces;
using UABEANext4.Util;

namespace UABEANext4.ViewModels.Dialogs;

public partial class AddAssetViewModel : ViewModelBaseValidator, IDialogAware<AddAssetResult>
{
    private Workspace _workspace;
    private Dictionary<AssetsFileInstance, List<string>> _scriptLookup;

    [ObservableProperty]
    public List<AssetsFileInstance> _files = new();
    [ObservableProperty]
    public ObservableCollection<string> _scripts = new();

    [ObservableProperty]
    public AssetsFileInstance? _selectedFile;
    [ObservableProperty]
    [CustomValidation(typeof(AddAssetViewModel), nameof(ValidatePathId))]
    public string _pathIdString = "";
    [ObservableProperty]
    [CustomValidation(typeof(AddAssetViewModel), nameof(ValidateTypeNameOrId))]
    public string _typeNameOrId = "";
    [ObservableProperty]
    public int _selectedScriptIndex = 0;

    [ObservableProperty]
    public bool _isScript = false;

    public string Title => "Add Asset";
    public int Width => 300;
    public int Height => 170;
    public event Action<AddAssetResult?>? RequestClose;

    public AddAssetViewModel(Workspace workspace, List<AssetsFileInstance> fileInsts)
    {
        _workspace = workspace;
        _scriptLookup = new();

        foreach (var fileInst in fileInsts)
        {
            Files.Add(fileInst);
            var scriptList = new List<string>();
            // this will skip script references that won't match. that's probably fine
            // since later analysis requires we know what we have loaded.
            var scriptInfos = AssetHelper.GetAssetsFileScriptInfos(workspace.Manager, fileInst);
            foreach (var scriptInfo in scriptInfos)
            {
                scriptList.Add($"{scriptInfo.Key} - {GetTypeRefFullName(scriptInfo.Value!)}");
            }
            _scriptLookup[fileInst] = scriptList;
        }

        if (fileInsts.Count > 0)
        {
            SelectedFile = fileInsts.First();
        }
    }

    public static ValidationResult? ValidatePathId(string pathIdStr, ValidationContext context)
    {
        if (context.ObjectInstance is not AddAssetViewModel vm)
        {
            throw new Exception("View model not found");
        }

        if (!long.TryParse(pathIdStr, out var pathId))
        {
            return new("Path ID must be a long");
        }

        if (pathId == 0)
        {
            return new("Zero is not a valid path ID");
        }

        var info = vm.SelectedFile?.file.GetAssetInfo(pathId);
        if (info != null)
        {
            return new("Path ID already exists in file");
        }

        return ValidationResult.Success;
    }

    public static ValidationResult? ValidateTypeNameOrId(string typeStr, ValidationContext context)
    {
        if (context.ObjectInstance is not AddAssetViewModel vm)
        {
            throw new Exception("View model not found");
        }

        if (!vm.TryParseTypeId(typeStr, false, out _, out _))
        {
            return new("Class ID must be an int or a valid class name");
        }

        return ValidationResult.Success;
    }

    // yes, we double parse to enable/disable the script field. oh well.
    partial void OnTypeNameOrIdChanged(string value)
    {
        if (TryParseTypeId(value, false, out _, out var typeId))
        {
            IsScript = typeId < 0 || typeId == (int)AssetClassID.MonoBehaviour;
        }
        else
        {
            IsScript = false;
        }
    }

    partial void OnSelectedFileChanged(AssetsFileInstance? value)
    {
        if (value == null)
        {
            return;
        }

        Scripts.Clear();
        if (_scriptLookup.TryGetValue(value, out var scripts))
        {
            Scripts.AddRange(scripts);
            SelectedScriptIndex = -1;
            SelectedScriptIndex = 0;
        }
    }

    private bool TryParseTypeId(string typeIdText, bool creating, out AssetTypeTemplateField? tempField, out int typeId)
    {
        if (SelectedFile == null)
        {
            tempField = null;
            typeId = -1;
            return false;
        }

        if (SelectedFile.file.Metadata.TypeTreeEnabled)
        {
            if (!TryParseTypeIdByTypeTree(SelectedFile, typeIdText, creating, out tempField, out typeId))
            {
                if (!TryParseTypeIdByClassDatabase(typeIdText, creating, out tempField, out typeId))
                {
                    return false;
                }
            }
        }
        else
        {
            if (!TryParseTypeIdByClassDatabase(typeIdText, creating, out tempField, out typeId))
            {
                tempField = null;
                typeId = -1;
                return false;
            }
        }

        return true;
    }

    private bool TryParseTypeIdByClassDatabase(string typeIdText, bool creating, out AssetTypeTemplateField? tempField, out int typeId)
    {
        tempField = null;

        ClassDatabaseFile cldb = _workspace.Manager.ClassDatabase;
        ClassDatabaseType cldbType;
        bool needsTypeId;
        if (int.TryParse(typeIdText, out typeId))
        {
            cldbType = cldb.FindAssetClassByID(typeId);
            needsTypeId = false;
        }
        else
        {
            cldbType = cldb.FindAssetClassByName(typeIdText);
            needsTypeId = true;
        }

        if (cldbType == null)
        {
            return false;
        }

        if (needsTypeId)
        {
            typeId = cldbType.ClassId;
        }

        if (creating)
        {
            tempField = new AssetTypeTemplateField();
            tempField.FromClassDatabase(cldb, cldbType);
        }
        return true;
    }

    private bool TryParseTypeIdByTypeTree(AssetsFileInstance file, string typeIdText, bool creating, out AssetTypeTemplateField? tempField, out int typeId)
    {
        tempField = null;

        AssetsFileMetadata meta = file.file.Metadata;
        TypeTreeType ttType;
        bool needsTypeId;
        if (int.TryParse(typeIdText, out typeId))
        {
            ttType = meta.FindTypeTreeTypeByID(typeId);
            needsTypeId = false;
        }
        else
        {
            ttType = meta.FindTypeTreeTypeByName(typeIdText);
            needsTypeId = true;
        }

        if (ttType == null)
        {
            return false;
        }

        if (needsTypeId)
        {
            typeId = ttType.TypeId;
        }

        if (creating)
        {
            tempField = new AssetTypeTemplateField();
            tempField.FromTypeTree(ttType);
        }
        return true;
    }

    private static string GetTypeRefFullName(AssetTypeReference typeRef)
    {
        var nameSpace = typeRef.Namespace;
        var className = typeRef.ClassName;

        if (nameSpace != "")
        {
            return $"{nameSpace}.{className}";
        }
        else
        {
            return className;
        }
    }

    public async void BtnOk_Click()
    {
        if (SelectedFile == null)
        {
            await ShowInvalidOptionsBox();
            return;
        }

        var pathIdSuccess = long.TryParse(PathIdString, out var pathId);
        if (!pathIdSuccess)
        {
            await ShowInvalidOptionsBox();
            return;
        }

        var typeIdSuccess = TryParseTypeId(TypeNameOrId, true, out var tempField, out var typeId);
        if (!typeIdSuccess)
        {
            await ShowInvalidOptionsBox();
            return;
        }

        var scriptIndex = typeId < 0 || typeId == (int)AssetClassID.MonoBehaviour ? (ushort)SelectedScriptIndex : ushort.MaxValue;
        var result = new AddAssetResult(SelectedFile, pathId, typeId, scriptIndex, tempField);
        RequestClose?.Invoke(result);
    }

    private async Task ShowInvalidOptionsBox()
    {
        await MessageBoxUtil.ShowDialog("Error", "Invalid options provided.");
    }

    public void BtnCancel_Click()
    {
        RequestClose?.Invoke(null);
    }
}

public class AddAssetResult
{
    public AssetsFileInstance File { get; }
    public AssetTypeTemplateField? TempField { get; }
    public long PathId { get; }
    public int TypeId { get; }
    public ushort ScriptIndex { get; }

    public AddAssetResult(
        AssetsFileInstance file, long pathId, int typeId,
        ushort scriptIndex, AssetTypeTemplateField? tempField = null)
    {
        File = file;
        PathId = pathId;
        TypeId = typeId;
        ScriptIndex = scriptIndex;
        TempField = tempField;
    }
}