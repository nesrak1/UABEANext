using AssetsTools.NET;
using Avalonia;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Silk.NET.OpenGL;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using UABEANext3.AssetHandlers.Mesh;
using UABEANext3.AssetWorkspace;

namespace UABEANext3.Models.MeshPreviewer
{
    public class MeshPreviewerControl : OpenGlControlBase
    {
        private Workspace _workspace;
        private AssetTypeValueField _baseField;

        public MeshPreviewerControl()
        {
            Debug.WriteLine("Mesh Previewer started!");

            //this.WhenAnyValue(x => x.ActiveMesh).Subscribe(ActiveMesh_Changed);
            ActiveMeshProperty.Changed.Subscribe(ActiveMesh_Changed);
        }

        private bool _dirtyModel = false;

        private void ActiveMesh_Changed(AvaloniaPropertyChangedEventArgs<MeshToOpenGL?> args)
        {
            var gl = args.NewValue.Value;
            if (gl == null)
                return;

            var vertexCount = gl.Vertices.Length / 3;
            _points = new Vertex[vertexCount];

            _indices = gl.Indices;

            var skip = gl.Normals.Length / vertexCount;
            for (var i = 0; i < vertexCount; i++)
            {
                _points[i] = new Vertex
                {
                    Position = new Vector3(gl.Vertices[i * 3], gl.Vertices[i * 3 + 1], gl.Vertices[i * 3 + 2]),
                    Normal = new Vector3(-gl.Normals[i * skip], gl.Normals[i * skip + 1], gl.Normals[i * skip + 2])
                };
            }

            _dirtyModel = true;
        }

        public static readonly DirectProperty<MeshPreviewerControl, MeshToOpenGL?> ActiveMeshProperty =
            AvaloniaProperty.RegisterDirect<MeshPreviewerControl, MeshToOpenGL?>(
                nameof(ActiveMesh), o => o.ActiveMesh, (o, v) => o.ActiveMesh = v);

        private MeshToOpenGL? _activeMesh;

        public MeshToOpenGL? ActiveMesh
        {
            get => _activeMesh;
            set => SetAndRaise(ActiveMeshProperty, ref _activeMesh, value);
        }

        const string VERTEX_SOURCE = @"#version 300 es
precision mediump float;
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec3 aNormal;
uniform mat4 uModel;
uniform mat4 uProjection;
uniform mat4 uView;
out vec3 FragNormal;
void main()
{
    gl_Position = uProjection * uView * uModel * vec4(aPos, 1.0);
    FragNormal = mat3(transpose(inverse(uModel))) * aNormal;
}";
        const string FRAGMENT_SORUCE = @"#version 300 es
precision mediump float;
in vec3 FragNormal;
out vec4 FragColor;
uniform vec3 uDirectionalLightDir;
uniform vec3 uDirectionalLightColor;
void main()
{
    vec3 normal = normalize(FragNormal);
    vec3 lightDirection = normalize(uDirectionalLightDir);
    
    float diff = max(dot(normal, lightDirection), 0.0);
    vec3 diffuse = diff * uDirectionalLightColor + 0.1;
    
    FragColor = vec4(diffuse, 1.0);
}";

        private uint _vertexShader;
        private uint _fragmentShader;
        private uint _shaderProgram;
        private uint _vertexBufferObject;
        private uint _indexBufferObject;
        private uint _vertexArrayObject;

        private float _yaw, _pitch, _roll;

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct Vertex
        {
            public Vector3 Position;
            public Vector3 Normal;
        }

        private Vertex[] _points = new Vertex[]
        {
            // Front face
            new Vertex { Position = new Vector3(-1.0f, -1.0f, 1.0f), Normal = new Vector3(1.0f, 1.0f, 1.0f) },
            new Vertex { Position = new Vector3( 1.0f, -1.0f, 1.0f), Normal = new Vector3(1.0f, 1.0f, 1.0f) },
            new Vertex { Position = new Vector3( 1.0f, 1.0f, 1.0f), Normal = new Vector3(1.0f, 1.0f, 1.0f) },
            new Vertex { Position = new Vector3(-1.0f, 1.0f, 1.0f), Normal = new Vector3(1.0f, 1.0f, 1.0f) },
            // Back face
            new Vertex { Position = new Vector3(-1.0f, -1.0f, -1.0f), Normal = new Vector3(1.0f, 1.0f, 1.0f) },
            new Vertex { Position = new Vector3( 1.0f, -1.0f, -1.0f), Normal = new Vector3(1.0f, 1.0f, 1.0f) },
            new Vertex { Position = new Vector3( 1.0f, 1.0f, -1.0f), Normal = new Vector3(1.0f, 1.0f, 1.0f) },
            new Vertex { Position = new Vector3(-1.0f, 1.0f, -1.0f), Normal = new Vector3(1.0f, 1.0f, 1.0f) },
        };

        // Define the indices that form each face of the cube
        private ushort[] _indices = new ushort[]
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

        const int positionLocation = 0;
        const int normalLocation = 1;

        private GL _gl;
        private bool _loaded = false;

        private void CheckError(int id)
        {
            GLEnum err;
            while ((err = _gl.GetError()) != GLEnum.NoError)
            {
                Debug.WriteLine($"OGL Error {err} @ {id}");
            }
        }

        protected uint LoadShader(ShaderType shaderType, string content)
        {
            var shaderHnd = _gl.CreateShader(shaderType);
            _gl.ShaderSource(shaderHnd, content);
            _gl.CompileShader(shaderHnd);
            string infoLog = _gl.GetShaderInfoLog(shaderHnd);
            if (!string.IsNullOrWhiteSpace(infoLog))
            {
                throw new Exception($"Error compiling shader of type {shaderType}, failed with error {infoLog}");
            }
            return shaderHnd;
        }

        protected override unsafe void OnOpenGlInit(GlInterface glInterface)
        {
            if (_loaded)
                return;

            _loaded = true;
            base.OnOpenGlInit(glInterface);

            _gl = GL.GetApi(glInterface.GetProcAddress);
            _gl.Enable(EnableCap.DepthTest);

            Debug.WriteLine($"Renderer: {_gl.GetStringS(GLEnum.Renderer)} Version: {_gl.GetStringS(GLEnum.Version)}");

            _vertexShader = LoadShader(ShaderType.VertexShader, VERTEX_SOURCE);
            _fragmentShader = LoadShader(ShaderType.FragmentShader, FRAGMENT_SORUCE);
            CheckError(0);

            _shaderProgram = _gl.CreateProgram();
            _gl.AttachShader(_shaderProgram, _vertexShader);
            _gl.AttachShader(_shaderProgram, _fragmentShader);
            _gl.BindAttribLocation(_shaderProgram, positionLocation, "aPos");
            _gl.BindAttribLocation(_shaderProgram, normalLocation, "aNormal");
            _gl.LinkProgram(_shaderProgram);
            CheckError(1);

            BuildMesh();
        }

        private unsafe void BuildMesh()
        {
            _vertexBufferObject = _gl.GenBuffer();
            _gl.BindBuffer(GLEnum.ArrayBuffer, _vertexBufferObject);
            var vertexSize = Marshal.SizeOf<Vertex>();
            fixed (void* pdata = _points)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(_points.Length * vertexSize), pdata, BufferUsageARB.StaticDraw);
            }
            CheckError(2);

            _indexBufferObject = _gl.GenBuffer();
            _gl.BindBuffer(GLEnum.ElementArrayBuffer, _indexBufferObject);
            fixed (void* pdata = _indices)
            {
                _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(_indices.Length * sizeof(ushort)), pdata, BufferUsageARB.StaticDraw);
            }
            CheckError(3);

            _vertexArrayObject = _gl.GenVertexArray();
            _gl.BindVertexArray(_vertexArrayObject);
            CheckError(4);

            _gl.VertexAttribPointer(positionLocation, 3, GLEnum.Float, false, (uint)vertexSize, (void*)0);
            _gl.VertexAttribPointer(normalLocation, 3, GLEnum.Float, false, (uint)vertexSize, (void*)12);
            CheckError(5);

            _gl.EnableVertexAttribArray(positionLocation);
            _gl.EnableVertexAttribArray(normalLocation);
            CheckError(6);
        }

        protected override unsafe void OnOpenGlRender(GlInterface glInterface, int fb)
        {
            var gl = GL.GetApi(glInterface.GetProcAddress);

            if (_dirtyModel)
            {
                _dirtyModel = false;
                BuildMesh();
            }

            gl.ClearColor(0.05f, 0.59f, 0.867f, 0);
            gl.Clear((uint)(GLEnum.ColorBufferBit | GLEnum.DepthBufferBit));
            gl.Viewport(0, 0, (uint)Bounds.Width, (uint)Bounds.Height);
            CheckError(7);

            gl.BindBuffer(GLEnum.ArrayBuffer, _vertexBufferObject);
            gl.BindBuffer(GLEnum.ElementArrayBuffer, _indexBufferObject);
            gl.BindVertexArray(_vertexArrayObject);
            CheckError(8);

            gl.UseProgram(_shaderProgram);
            CheckError(9);

            var projection = Matrix4x4.CreatePerspectiveFieldOfView(
                (float)(Math.PI / 4), (float)(Bounds.Width / Bounds.Height), 0.01f, 1000);

            var view = Matrix4x4.CreateLookAt(new Vector3(15, 15, 15), new Vector3(), new Vector3(0, 1, 0));
            var model = Matrix4x4.CreateFromYawPitchRoll(_yaw, _pitch, _roll);
            var modelLoc = gl.GetUniformLocation(_shaderProgram, "uModel");
            var viewLoc = gl.GetUniformLocation(_shaderProgram, "uView");
            var projectionLoc = gl.GetUniformLocation(_shaderProgram, "uProjection");
            gl.UniformMatrix4(modelLoc, 1, false, &model.M11);
            gl.UniformMatrix4(viewLoc, 1, false, &view.M11);
            gl.UniformMatrix4(projectionLoc, 1, false, &projection.M11);
            CheckError(10);

            var directionalLightDirLoc = gl.GetUniformLocation(_shaderProgram, "uDirectionalLightDir");
            var directionalLightColorLoc = gl.GetUniformLocation(_shaderProgram, "uDirectionalLightColor");
            gl.Uniform3(directionalLightDirLoc, -1.0f, -1.0f, -0.7f);
            gl.Uniform3(directionalLightColorLoc, 1.0f, 1.0f, 1.0f);
            CheckError(11);
            gl.DrawElements(PrimitiveType.Triangles, (uint)_indices.Length, DrawElementsType.UnsignedShort, (void*)0);
            CheckError(12);

            _yaw += 0.01f;
            //_roll += 0.02f;
            RequestNextFrameRendering();
        }

        protected override void OnOpenGlDeinit(GlInterface glInterface)
        {
            var gl = GL.GetApi(glInterface.GetProcAddress);

            gl.BindBuffer(GLEnum.ArrayBuffer, 0);
            gl.BindBuffer(GLEnum.ElementArrayBuffer, 0);
            gl.BindVertexArray(0);
            gl.UseProgram(0);

            gl.DeleteBuffer(_vertexBufferObject);
            gl.DeleteBuffer(_indexBufferObject);
            gl.DeleteVertexArray(_vertexArrayObject);
            gl.DeleteProgram(_shaderProgram);
            gl.DeleteShader(_fragmentShader);
            gl.DeleteShader(_vertexShader);
        }
    }
}
