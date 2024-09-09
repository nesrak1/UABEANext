using CommunityToolkit.Mvvm.Messaging.Messages;
using System.Collections.Generic;
using UABEANext4.AssetWorkspace;

namespace UABEANext4.Logic;
public class AssetsSelectedMessage(List<AssetInst> value)
    : ValueChangedMessage<List<AssetInst>>(value)
{
}

public class RequestEditAssetMessage(AssetInst value)
    : ValueChangedMessage<AssetInst>(value)
{
}

public class RequestVisitAssetMessage(AssetInst value)
    : ValueChangedMessage<AssetInst>(value)
{
}

public class RequestSceneViewMessage(AssetInst value)
    : ValueChangedMessage<AssetInst>(value)
{
}

public class AssetsUpdatedMessage(AssetInst value)
    : ValueChangedMessage<AssetInst>(value)
{
}

public class SelectedWorkspaceItemChangedMessage(List<WorkspaceItem> value)
    : ValueChangedMessage<List<WorkspaceItem>>(value)
{
}

public class WorkspaceClosingMessage()
    : ValueChangedMessage<bool>(false)
{
}