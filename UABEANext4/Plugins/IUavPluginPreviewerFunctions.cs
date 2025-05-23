using Avalonia.Media.Imaging;
using AvaloniaEdit.Document;
using System.Threading.Tasks;
using UABEANext4.Logic.Mesh;

namespace UABEANext4.Plugins;
public interface IUavPluginPreviewerFunctions
{
    public Task SetPreviewText(TextDocument document);
    public Task SetPreviewImage(Bitmap image);
    public Task SetPreviewMesh(MeshObj mesh);
}
