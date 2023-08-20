using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;
using Avalonia.ReactiveUI;
using System;
using System.Collections.Generic;
using UABEANext3.ViewModels.Tools;

namespace UABEANext3.Views.Tools
{
    public partial class PreviewerToolView : ReactiveUserControl<PreviewerToolViewModel>
    {
        public PreviewerToolView()
        {
            InitializeComponent();
        }
    }

    public class PreviewerTemplateSelector : IDataTemplate
    {
        [Content]
        public Dictionary<string, IDataTemplate> AvailableTemplates { get; } = new Dictionary<string, IDataTemplate>();

        public Control Build(object? param)
        {
            var key = param?.ToString();
            if (key is null)
            {
                throw new ArgumentNullException(nameof(param));
            }
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
}
