using Avalonia;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Rendering;
using Silk.NET.OpenGL;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using UABEANext4.Logic.Mesh;

namespace UABEANext4.Controls.MeshPreviewer;
public class MeshPreviewerControl : OpenGlControlBase, ICustomHitTest
{
    private GL? _gl;
    private bool _loaded = false;
    private bool _dirtyModel = false;

    private uint _vertexShader;
    private uint _fragmentShader;
    private uint _shaderProgram;
    private uint _vertexBufferObject;
    private uint _indexBufferObject;
    private uint _vertexArrayObject;

    private Vector3 _cameraPos = new(15f, 0f, 0f);
    private Vector2 _cameraPos2D = new(0f, 0f);
    private float _cameraZoom = 15f;
    private Vector2 _lastPos = new(-1f, -1f);

    const float PIH_MINUS_EPSILON = (MathF.PI / 2) - 0.0001f;

    public MeshPreviewerControl()
    {
        ActiveMeshProperty.Changed.Subscribe(ActiveMesh_Changed);

        PointerPressed += MeshPreviewerControl_PointerPressed;
        PointerReleased += MeshPreviewerControl_PointerReleased;
        PointerMoved += MeshPreviewerControl_PointerMoved;
        PointerWheelChanged += MeshPreviewerControl_PointerWheelChanged;

        RecalculateCamera();
    }

    private MeshObj? _activeMesh;

    public static readonly DirectProperty<MeshPreviewerControl, MeshObj?> ActiveMeshProperty =
        AvaloniaProperty.RegisterDirect<MeshPreviewerControl, MeshObj?>(
            nameof(ActiveMesh), o => o.ActiveMesh, (o, v) => o.ActiveMesh = v);

    public MeshObj? ActiveMesh
    {
        get => _activeMesh;
        set => SetAndRaise(ActiveMeshProperty, ref _activeMesh, value);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct Vertex
    {
        public Vector3 Position;
        public Vector3 Normal;
    }

    private Vertex[] _points =
    [
        new Vertex { Position = new Vector3(-1.0f, -1.0f, 1.0f), Normal = new Vector3(1.0f, 1.0f, 1.0f) },
        new Vertex { Position = new Vector3( 1.0f, -1.0f, 1.0f), Normal = new Vector3(1.0f, 1.0f, 1.0f) },
        new Vertex { Position = new Vector3( 1.0f, 1.0f, 1.0f), Normal = new Vector3(1.0f, 1.0f, 1.0f) },
        new Vertex { Position = new Vector3(-1.0f, 1.0f, 1.0f), Normal = new Vector3(1.0f, 1.0f, 1.0f) },
        new Vertex { Position = new Vector3(-1.0f, -1.0f, -1.0f), Normal = new Vector3(1.0f, 1.0f, 1.0f) },
        new Vertex { Position = new Vector3( 1.0f, -1.0f, -1.0f), Normal = new Vector3(1.0f, 1.0f, 1.0f) },
        new Vertex { Position = new Vector3( 1.0f, 1.0f, -1.0f), Normal = new Vector3(1.0f, 1.0f, 1.0f) },
        new Vertex { Position = new Vector3(-1.0f, 1.0f, -1.0f), Normal = new Vector3(1.0f, 1.0f, 1.0f) },
    ];

    private ushort[] _indices =
    [
        // face 1
        0, 1, 2,
        2, 3, 0,
        
        // face 2
        4, 5, 6,
        6, 7, 4,
        
        // face 3
        0, 3, 7,
        7, 4, 0,
        
        // face 4
        1, 2, 6,
        6, 5, 1,
        
        // face 5
        3, 2, 6,
        6, 7, 3,
        
        // face 6
        0, 1, 5,
        5, 4, 0
    ];

    private void ActiveMesh_Changed(AvaloniaPropertyChangedEventArgs<MeshObj?> args)
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

    private void MeshPreviewerControl_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        var curPos = e.GetPosition(this);
        _lastPos.X = (float)curPos.X;
        _lastPos.Y = (float)curPos.Y;
    }

    private void MeshPreviewerControl_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Left)
        {
            _lastPos.X = -1f;
            _lastPos.Y = -1f;
        }
    }

    private void MeshPreviewerControl_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (_lastPos.X == -1f && _lastPos.Y == -1f)
            return;

        var curPos = e.GetPosition(this);
        var curPosX = (float)curPos.X;
        var curPosY = (float)curPos.Y;

        _cameraPos2D.X += (_lastPos.X - curPosX) * 0.006f;
        _cameraPos2D.Y += (curPosY - _lastPos.Y) * 0.006f;
        _cameraPos2D.Y = MathF.Max(MathF.Min(_cameraPos2D.Y, PIH_MINUS_EPSILON), -PIH_MINUS_EPSILON);

        RecalculateCamera();

        _lastPos.X = curPosX;
        _lastPos.Y = curPosY;
    }

    private void MeshPreviewerControl_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        _cameraZoom *= 1f - (float)e.Delta.Y / 10f;
        RecalculateCamera();
    }

    private void RecalculateCamera()
    {
        _cameraPos.X = _cameraZoom * MathF.Cos(_cameraPos2D.Y) * MathF.Sin(_cameraPos2D.X);
        _cameraPos.Y = _cameraZoom * MathF.Sin(_cameraPos2D.Y);
        _cameraPos.Z = _cameraZoom * MathF.Cos(_cameraPos2D.Y) * MathF.Cos(_cameraPos2D.X);
    }

    public bool HitTest(Point point)
    {
        return true;
    }

    // /////////////

    private void CheckError(int id)
    {
        if (_gl is null || !_loaded)
            return;

        GLEnum err;
        while ((err = _gl.GetError()) != GLEnum.NoError)
        {
            Debug.WriteLine($"OGL Error {err} @ {id}");
        }
    }

    protected uint LoadShader(ShaderType shaderType, string content)
    {
        if (_gl is null || !_loaded)
            return uint.MaxValue;

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

        _vertexShader = LoadShader(ShaderType.VertexShader, MeshPreviewerShaders.VERTEX_SOURCE);
        _fragmentShader = LoadShader(ShaderType.FragmentShader, MeshPreviewerShaders.FRAGMENT_SORUCE);
        CheckError(0);

        _shaderProgram = _gl.CreateProgram();
        _gl.AttachShader(_shaderProgram, _vertexShader);
        _gl.AttachShader(_shaderProgram, _fragmentShader);
        _gl.BindAttribLocation(_shaderProgram, MeshPreviewerShaders.POSITION_LOC, "aPos");
        _gl.BindAttribLocation(_shaderProgram, MeshPreviewerShaders.NORMAL_LOC, "aNormal");
        _gl.LinkProgram(_shaderProgram);
        CheckError(1);

        BuildMesh(_gl);
    }

    private unsafe void BuildMesh(GL gl)
    {
        _vertexBufferObject = gl.GenBuffer();
        gl.BindBuffer(GLEnum.ArrayBuffer, _vertexBufferObject);
        var vertexSize = Marshal.SizeOf<Vertex>();
        fixed (void* pdata = _points)
        {
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(_points.Length * vertexSize), pdata, BufferUsageARB.StaticDraw);
        }
        CheckError(2);

        _indexBufferObject = gl.GenBuffer();
        gl.BindBuffer(GLEnum.ElementArrayBuffer, _indexBufferObject);
        fixed (void* pdata = _indices)
        {
            gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(_indices.Length * sizeof(ushort)), pdata, BufferUsageARB.StaticDraw);
        }
        CheckError(3);

        _vertexArrayObject = gl.GenVertexArray();
        gl.BindVertexArray(_vertexArrayObject);
        CheckError(4);

        gl.VertexAttribPointer(MeshPreviewerShaders.POSITION_LOC, 3, GLEnum.Float, false, (uint)vertexSize, (void*)0);
        gl.VertexAttribPointer(MeshPreviewerShaders.NORMAL_LOC, 3, GLEnum.Float, false, (uint)vertexSize, (void*)12);
        CheckError(5);

        gl.EnableVertexAttribArray(MeshPreviewerShaders.POSITION_LOC);
        gl.EnableVertexAttribArray(MeshPreviewerShaders.NORMAL_LOC);
        CheckError(6);
    }

    protected override unsafe void OnOpenGlRender(GlInterface glInterface, int fb)
    {
        var gl = GL.GetApi(glInterface.GetProcAddress);

        if (_dirtyModel)
        {
            _dirtyModel = false;
            BuildMesh(gl);
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

        var view = Matrix4x4.CreateLookAt(_cameraPos, new Vector3(), new Vector3(0, 1, 0));
        var model = Matrix4x4.Identity;
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