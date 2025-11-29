using Avalonia.Controls;
using UABEANext4.Controls.SceneView;
using UABEANext4.ViewModels.Tools;

namespace UABEANext4.Views.Tools;

public partial class SceneViewToolView : UserControl
{
    public SceneViewToolView()
    {
        InitializeComponent();

        // Connect scene view control events to view model
        sceneViewControl.SelectionChanged += OnSceneSelectionChanged;
    }

    private void OnSceneSelectionChanged(object? sender, Logic.Scene.SceneObject? obj)
    {
        if (DataContext is SceneViewToolViewModel vm)
        {
            vm.OnObjectSelected(obj);
        }
    }
}
