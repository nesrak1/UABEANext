using Avalonia.Media.Imaging;
using Avalonia;
using Dock.Model.ReactiveUI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UABEANext3.AssetWorkspace;
using ReactiveUI;
using Avalonia.Platform;

namespace UABEANext3.ViewModels.Tools
{
    internal class PreviewerToolViewModel : Tool
    {
        const string TOOL_TITLE = "Previewer";

        private Workspace _workspace;
        public Workspace Workspace
        {
            get => _workspace;
            set => this.RaiseAndSetIfChanged(ref _workspace, value);
        }

        private Bitmap? _activeImage;
        public Bitmap? ActiveImage
        {
            get => _activeImage;
            set => this.RaiseAndSetIfChanged(ref _activeImage, value);
        }

        // preview only
        public PreviewerToolViewModel()
        {
            Workspace = new();
            ActiveImage = null;

            Id = TOOL_TITLE.Replace(" ", "");
            Title = TOOL_TITLE;
        }

        public PreviewerToolViewModel(Workspace workspace)
        {
            Workspace = workspace;
            ActiveImage = null;

            Id = TOOL_TITLE.Replace(" ", "");
            Title = TOOL_TITLE;
        }

        public void SetImage(byte[] data, int width, int height)
        {
            if (ActiveImage != null)
            {
                ActiveImage.Dispose();
            }

            var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Rgba8888);
            using (var frameBuffer = bitmap.Lock())
            {
                Marshal.Copy(data, 0, frameBuffer.Address, data.Length);
            }
            ActiveImage = bitmap;
        }
    }
}
