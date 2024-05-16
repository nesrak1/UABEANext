using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;
using System;
using System.Collections.Generic;
using UABEANext4.ViewModels.Tools;

namespace UABEANext4.Views.Tools;
public partial class PreviewerToolView : UserControl
{
    public PreviewerToolView()
    {
        InitializeComponent();
    }
}

public class PreviewerTemplateSelector : IDataTemplate
{
    [Content]
    public Dictionary<string, IDataTemplate> AvailableTemplates { get; } = new();

    public Control Build(object? param)
    {
        var key = param?.ToString() ?? throw new ArgumentNullException(nameof(param));
        return AvailableTemplates[key].Build(param)!;
    }

    public bool Match(object? data)
    {
        var key = data?.ToString();

        return data is PreviewerToolPreviewType
                && !string.IsNullOrEmpty(key)
                && AvailableTemplates.ContainsKey(key);
    }
}