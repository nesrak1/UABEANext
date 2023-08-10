using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using static Avalonia.OpenGL.GlConsts;

namespace UABEANext3.Models.MeshPreviewer
{
    public class MeshPreviewerControl : OpenGlControlBase
    {
        public MeshPreviewerControl()
        {
            Debug.WriteLine("test");
        }

        const string VERTEX_SOURCE = @"#version 300 es
precision mediump float;
layout (location = 0) in vec3 aPos;
uniform mat4 uModel;
uniform mat4 uProjection;
uniform mat4 uView;
void main()
{
    gl_Position = uProjection * uView * uModel * vec4(aPos, 1.0);
}";
        const string FRAGMENT_SORUCE = @"#version 300 es
precision mediump float;
out vec4 FragColor;
void main()
{
    FragColor = vec4(1.0f, 0.5f, 0.2f, 1.0f);
}";

        private int _vertexShader;
        private int _fragmentShader;
        private int _shaderProgram;
        private int _vertexBufferObject;
        private int _indexBufferObject;
        private int _vertexArrayObject;

        private float _yaw = 0.2f, _pitch, _roll;

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct Vertex
        {
            public Vector3 Position;
            //public Vector3 Normal;
        }

        private readonly Vertex[] _points = new Vertex[]
        {
            // Front face
            new Vertex { Position = new Vector3(-1.0f, -1.0f, 1.0f) },
            new Vertex { Position = new Vector3( 1.0f, -1.0f, 1.0f) },
            new Vertex { Position = new Vector3( 1.0f, 1.0f, 1.0f) },
            new Vertex { Position = new Vector3(-1.0f, 1.0f, 1.0f) },
            // Back face
            new Vertex { Position = new Vector3(-1.0f, -1.0f, -1.0f) },
            new Vertex { Position = new Vector3( 1.0f, -1.0f, -1.0f) },
            new Vertex { Position = new Vector3( 1.0f, 1.0f, -1.0f) },
            new Vertex { Position = new Vector3(-1.0f, 1.0f, -1.0f) },
        };

        // Define the indices that form each face of the cube
        private readonly ushort[] _indices = new ushort[]
        {
            // Front face
            0, 1, 2,
            2, 3, 0,
        
            // Back face
            4, 5, 6,
            6, 7, 4,
        
            // Left face
            0, 3, 7,
            7, 4, 0,
        
            // Right face
            1, 2, 6,
            6, 5, 1,
        
            // Top face
            3, 2, 6,
            6, 7, 3,
        
            // Bottom face
            0, 1, 5,
            5, 4, 0
        };

        private float[] _normals = new float[0];

        private static void CheckError(GlInterface gl)
        {
            int err;
            while ((err = gl.GetError()) != GL_NO_ERROR)
                Debug.WriteLine(err);
        }

        // gpt temporary method
        private float[] CreateNormals()
        {
            var normals = new List<float>();
            for (var i = 0; i < _indices.Length; i += 3)
            {
                var i1 = _indices[i];
                var i2 = _indices[i + 1];
                var i3 = _indices[i + 2];

                var v1 = _points[i1].Position;
                var v2 = _points[i2].Position;
                var v3 = _points[i3].Position;

                var edge1 = new float[] { v2.X - v1.X, v2.Y - v1.Y, v2.Z - v1.Z };
                var edge2 = new float[] { v3.X - v1.X, v3.Y - v1.Y, v3.Z - v1.Z };

                var normal = new float[] {
                    edge1[1] * edge2[2] - edge1[2] * edge2[1],
                    edge1[2] * edge2[0] - edge1[0] * edge2[2],
                    edge1[0] * edge2[1] - edge1[1] * edge2[0],
                };

                var length = (float)Math.Sqrt(normal[0] * normal[0] + normal[1] * normal[1] + normal[2] * normal[2]);
                normal[0] /= length;
                normal[1] /= length;
                normal[2] /= length;

                for (int j = 0; j < 3; j++)
                {
                    normals.Add(normal[0]);
                    normals.Add(normal[1]);
                    normals.Add(normal[2]);
                }
            }

            return normals.ToArray();
        }

        protected override unsafe void OnOpenGlInit(GlInterface gl)
        {
            string? error;

            CheckError(gl);
            Debug.WriteLine($"Renderer: {gl.GetString(GL_RENDERER)} Version: {gl.GetString(GL_VERSION)}");

            _vertexShader = gl.CreateShader(GL_VERTEX_SHADER);
            error = gl.CompileShaderAndGetError(_vertexShader, VERTEX_SOURCE);
            Debug.WriteLine(error);

            _fragmentShader = gl.CreateShader(GL_FRAGMENT_SHADER);
            error = gl.CompileShaderAndGetError(_fragmentShader, FRAGMENT_SORUCE);
            Debug.WriteLine(error);

            _shaderProgram = gl.CreateProgram();
            gl.AttachShader(_shaderProgram, _vertexShader);
            gl.AttachShader(_shaderProgram, _fragmentShader);
            const int positionLocation = 0;
            gl.BindAttribLocationString(_shaderProgram, positionLocation, "aPos");
            error = gl.LinkProgramAndGetError(_shaderProgram);
            Debug.WriteLine(error);
            CheckError(gl);

            // Create the vertex buffer object (VBO) for the vertex data.
            _vertexBufferObject = gl.GenBuffer();
            // Bind the VBO and copy the vertex data into it.
            gl.BindBuffer(GL_ARRAY_BUFFER, _vertexBufferObject);
            CheckError(gl);
            var vertexSize = Marshal.SizeOf<Vertex>();
            fixed (void* pdata = _points)
                gl.BufferData(GL_ARRAY_BUFFER, new IntPtr(_points.Length * vertexSize),
                    new IntPtr(pdata), GL_STATIC_DRAW);

            _indexBufferObject = gl.GenBuffer();
            gl.BindBuffer(GL_ELEMENT_ARRAY_BUFFER, _indexBufferObject);
            CheckError(gl);
            fixed (void* pdata = _indices)
                gl.BufferData(GL_ELEMENT_ARRAY_BUFFER, new IntPtr(_indices.Length * sizeof(ushort)), new IntPtr(pdata),
                    GL_STATIC_DRAW);
            CheckError(gl);
            _vertexArrayObject = gl.GenVertexArray();
            gl.BindVertexArray(_vertexArrayObject);
            CheckError(gl);
            gl.VertexAttribPointer(positionLocation, 3, GL_FLOAT,
                0, vertexSize, IntPtr.Zero);
            gl.EnableVertexAttribArray(positionLocation);
            CheckError(gl);
        }

        protected override unsafe void OnOpenGlRender(GlInterface gl, int fb)
        {
            gl.ClearColor(0.05f, 0.59f, 0.867f, 0);
            gl.Clear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
            gl.Viewport(0, 0, (int)Bounds.Width, (int)Bounds.Height);

            gl.BindBuffer(GL_ARRAY_BUFFER, _vertexBufferObject);
            gl.BindBuffer(GL_ELEMENT_ARRAY_BUFFER, _indexBufferObject);
            gl.BindVertexArray(_vertexArrayObject);

            gl.UseProgram(_shaderProgram);
            CheckError(gl);

            var projection = Matrix4x4.CreatePerspectiveFieldOfView(
                (float)(Math.PI / 4), (float)(Bounds.Width / Bounds.Height), 0.01f, 1000);
            var view = Matrix4x4.CreateLookAt(new Vector3(15, 15, 15), new Vector3(), new Vector3(0, 1, 0));
            var model = Matrix4x4.CreateFromYawPitchRoll(_yaw, _pitch, _roll);
            var modelLoc = gl.GetUniformLocationString(_shaderProgram, "uModel");
            var viewLoc = gl.GetUniformLocationString(_shaderProgram, "uView");
            var projectionLoc = gl.GetUniformLocationString(_shaderProgram, "uProjection");
            gl.UniformMatrix4fv(modelLoc, 1, false, &model);
            gl.UniformMatrix4fv(viewLoc, 1, false, &view);
            gl.UniformMatrix4fv(projectionLoc, 1, false, &projection);

            gl.DrawElements(GL_TRIANGLES, _indices.Length, GL_UNSIGNED_SHORT, IntPtr.Zero);

            _yaw += 0.01f;
            //_roll += 0.015f;
            _pitch += 0.02f;
            RequestNextFrameRendering();
        }

        protected override void OnOpenGlDeinit(GlInterface gl)
        {
            // Unbind everything
            gl.BindBuffer(GL_ARRAY_BUFFER, 0);
            gl.BindBuffer(GL_ELEMENT_ARRAY_BUFFER, 0);
            gl.BindVertexArray(0);
            gl.UseProgram(0);

            // Delete all resources.
            gl.DeleteBuffer(_vertexBufferObject);
            gl.DeleteBuffer(_indexBufferObject);
            gl.DeleteVertexArray(_vertexArrayObject);
            gl.DeleteProgram(_shaderProgram);
            gl.DeleteShader(_fragmentShader);
            gl.DeleteShader(_vertexShader);
        }
    }
}
