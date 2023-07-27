using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using System.Diagnostics;
using static Avalonia.OpenGL.GlConsts;

namespace UABEANext3.Models.MeshPreviewer
{
    public class MeshPreviewerControl : OpenGlControlBase
    {
        public MeshPreviewerControl()
        {
            Debug.WriteLine("test");
        }

        private static void CheckError(GlInterface gl)
        {
            int err;
            while ((err = gl.GetError()) != GL_NO_ERROR)
                Debug.WriteLine(err);
        }

        protected override void OnOpenGlInit(GlInterface GL)
        {
            CheckError(GL);
            Debug.WriteLine($"Renderer: {GL.GetString(GL_RENDERER)} Version: {GL.GetString(GL_VERSION)}");
        }

        protected override void OnOpenGlRender(GlInterface gl, int fb)
        {
            gl.ClearColor(0.05f, 0.59f, 0.867f, 0);
            gl.Clear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
            gl.Viewport(0, 0, (int)Bounds.Width, (int)Bounds.Height);
        }
    }
}
