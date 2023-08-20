using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AvaloniaEdit.Document;
using Dock.Model.ReactiveUI.Controls;
using ReactiveUI.Fody.Helpers;
using System;
using System.Runtime.InteropServices;
using System.Text;
using UABEANext3.AssetHandlers.Mesh;
using UABEANext3.AssetWorkspace;

namespace UABEANext3.ViewModels.Tools
{
    public class PreviewerToolViewModel : Tool
    {
        const string TOOL_TITLE = "Previewer";

        [Reactive]
        public Workspace Workspace { get; set; }
        [Reactive]
        public Bitmap? ActiveImage { get; set; }
        [Reactive]
        public TextDocument? ActiveDocument { get; set; }
        [Reactive]
        public MeshToOpenGL? ActiveMesh { get; set; }

        [Reactive]
        public PreviewerToolPreviewType ActivePreviewType { get; set; } = PreviewerToolPreviewType.Mesh;

        const int TEXT_ASSET_MAX_LENGTH = 100000;

        [Obsolete("This is a previewer-only constructor")]
        public PreviewerToolViewModel()
        {
            Workspace = new();
            ActiveImage = null;
            ActiveDocument = new TextDocument();

            Id = TOOL_TITLE.Replace(" ", "");
            Title = TOOL_TITLE;
        }

        public PreviewerToolViewModel(Workspace workspace)
        {
            Workspace = workspace;
            ActiveImage = null;
            ActiveDocument = new TextDocument();

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
            ActivePreviewType = PreviewerToolPreviewType.Image;
            ActiveImage = bitmap;
        }

        public void SetText(byte[] text)
        {
            ActivePreviewType = PreviewerToolPreviewType.Text;
            string trimmedText;
            if (text.Length <= TEXT_ASSET_MAX_LENGTH)
            {
                trimmedText = Encoding.UTF8.GetString(text);
            }
            else
            {
                trimmedText = Encoding.UTF8.GetString(text[..TEXT_ASSET_MAX_LENGTH]) + $"... (and {text.Length - TEXT_ASSET_MAX_LENGTH} bytes more)";
            }
            ActiveDocument = new TextDocument(trimmedText.ToCharArray());
        }

        public async void SetMesh(AssetInst asset)
        {
            ActivePreviewType = PreviewerToolPreviewType.Mesh;
            var baseField = Workspace.GetBaseField(asset);
            if (baseField != null)
            {
                MeshToOpenGL m2ogl = new MeshToOpenGL(Workspace, asset.FileInstance, baseField);
                ActiveMesh = m2ogl;
            }
            //if (SetActiveMesh != null)
            //{
            //    await SetActiveMesh.Handle(new MeshPreviewParams(Workspace, asset));
            //}
        }
    }

    public class MeshPreviewParams
    {
        public Workspace Workspace;
        public AssetInst Asset;

        public MeshPreviewParams(Workspace workspace, AssetInst asset)
        {
            Workspace = workspace;
            Asset = asset;
        }
    }

    public enum PreviewerToolPreviewType
    {
        Image,
        Text,
        Mesh,
    }
}
