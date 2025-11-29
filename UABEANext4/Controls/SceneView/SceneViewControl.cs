using Avalonia;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Rendering;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using UABEANext4.Logic.Scene;

namespace UABEANext4.Controls.SceneView;

public class SceneViewControl : OpenGlControlBase, ICustomHitTest
{
    private GL? _gl;
    private bool _loaded = false;
    private bool _dirtyScene = false;

    // Shaders
    private uint _mainShaderProgram;
    private uint _gridShaderProgram;
    private uint _gizmoShaderProgram;

    // Buffers for objects
    private readonly Dictionary<SceneObject, ObjectBuffers> _objectBuffers = new();
    private uint _gridVao;
    private uint _gridVbo;

    // Camera state
    private Vector3 _cameraPosition = new(0f, 5f, 10f);
    private float _cameraYaw = -90f; // Look towards -Z initially
    private float _cameraPitch = -15f;
    private Vector3 _cameraFront = new(0f, 0f, -1f);
    private Vector3 _cameraUp = Vector3.UnitY;

    // Input state
    private bool _isRightMouseDown = false;
    private Point _lastMousePos;
    private readonly HashSet<Key> _pressedKeys = new();
    private float _moveSpeed = 10f;
    private float _mouseSensitivity = 0.15f;

    // Object interaction
    private SceneObject? _selectedObject;
    private bool _isDragging = false;
    private Vector3 _dragStartPos;
    private Vector3 _dragPlaneNormal;
    private int _activeAxis = -1; // 0=X, 1=Y, 2=Z, -1=none

    // Frame timing
    private DateTime _lastFrameTime = DateTime.Now;
    private float _deltaTime = 0f;

    // Scene data
    private SceneData? _sceneData;

    public static readonly DirectProperty<SceneViewControl, SceneData?> SceneDataProperty =
        AvaloniaProperty.RegisterDirect<SceneViewControl, SceneData?>(
            nameof(SceneData), o => o.SceneData, (o, v) => o.SceneData = v);

    public SceneData? SceneData
    {
        get => _sceneData;
        set
        {
            SetAndRaise(SceneDataProperty, ref _sceneData, value);
            _dirtyScene = true;
        }
    }

    // Event for selection changes
    public event EventHandler<SceneObject?>? SelectionChanged;

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct Vertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 TexCoord;
    }

    private class ObjectBuffers
    {
        public uint Vao;
        public uint Vbo;
        public uint Ebo;
        public uint TextureId;
        public int IndexCount;
        public bool HasTexture;
    }

    public SceneViewControl()
    {
        Focusable = true;

        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        PointerMoved += OnPointerMoved;
        PointerWheelChanged += OnPointerWheelChanged;
        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;

        UpdateCameraVectors();
    }

    private void UpdateCameraVectors()
    {
        var yawRad = MathF.PI / 180f * _cameraYaw;
        var pitchRad = MathF.PI / 180f * _cameraPitch;

        _cameraFront = Vector3.Normalize(new Vector3(
            MathF.Cos(yawRad) * MathF.Cos(pitchRad),
            MathF.Sin(pitchRad),
            MathF.Sin(yawRad) * MathF.Cos(pitchRad)
        ));
    }

    #region Input Handling

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        _lastMousePos = point.Position;

        if (point.Properties.IsRightButtonPressed)
        {
            _isRightMouseDown = true;
            Focus();
            e.Pointer.Capture(this);
        }
        else if (point.Properties.IsLeftButtonPressed)
        {
            Focus();

            // Check if clicking on gizmo first
            if (_selectedObject != null && _activeAxis >= 0)
            {
                _isDragging = true;
                _dragStartPos = _selectedObject.LocalPosition;
                switch (_activeAxis)
                {
                    case 0: _dragPlaneNormal = Vector3.UnitZ; break;
                    case 1: _dragPlaneNormal = Vector3.UnitZ; break;
                    case 2: _dragPlaneNormal = Vector3.UnitX; break;
                }
            }
            else
            {
                // Object picking
                PickObject(point.Position);
            }
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Right)
        {
            _isRightMouseDown = false;
            e.Pointer.Capture(null);
        }
        else if (e.InitialPressMouseButton == MouseButton.Left)
        {
            _isDragging = false;
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        var currentPos = point.Position;

        if (_isRightMouseDown)
        {
            // Mouse look
            var deltaX = (float)(currentPos.X - _lastMousePos.X);
            var deltaY = (float)(currentPos.Y - _lastMousePos.Y);

            _cameraYaw += deltaX * _mouseSensitivity;
            _cameraPitch -= deltaY * _mouseSensitivity;

            // Clamp pitch
            _cameraPitch = MathF.Max(-89f, MathF.Min(89f, _cameraPitch));

            UpdateCameraVectors();
        }
        else if (_isDragging && _selectedObject != null && _activeAxis >= 0)
        {
            // Move object along axis
            var ray = GetMouseRay(currentPos);
            var delta = GetDragDelta(ray, _dragPlaneNormal);

            var newPos = _dragStartPos;
            switch (_activeAxis)
            {
                case 0: newPos.X += delta.X; break;
                case 1: newPos.Y += delta.Y; break;
                case 2: newPos.Z += delta.Z; break;
            }

            _selectedObject.LocalPosition = newPos;
            _selectedObject.ComputeWorldMatrix();
            _selectedObject.ComputeBounds();
        }
        else
        {
            // Check for gizmo hover
            if (_selectedObject != null)
            {
                var ray = GetMouseRay(currentPos);
                _activeAxis = GetHoveredGizmoAxis(ray, _selectedObject.LocalPosition);
            }
        }

        _lastMousePos = currentPos;
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        // Zoom by moving forward/backward
        _cameraPosition += _cameraFront * (float)e.Delta.Y * 2f;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        _pressedKeys.Add(e.Key);
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        _pressedKeys.Remove(e.Key);
    }

    private void ProcessKeyboardInput()
    {
        if (!_isRightMouseDown) return;

        var speed = _moveSpeed * _deltaTime;
        var right = Vector3.Normalize(Vector3.Cross(_cameraFront, _cameraUp));

        if (_pressedKeys.Contains(Key.W))
            _cameraPosition += _cameraFront * speed;
        if (_pressedKeys.Contains(Key.S))
            _cameraPosition -= _cameraFront * speed;
        if (_pressedKeys.Contains(Key.A))
            _cameraPosition -= right * speed;
        if (_pressedKeys.Contains(Key.D))
            _cameraPosition += right * speed;
        if (_pressedKeys.Contains(Key.E) || _pressedKeys.Contains(Key.Space))
            _cameraPosition += _cameraUp * speed;
        if (_pressedKeys.Contains(Key.Q) || _pressedKeys.Contains(Key.LeftCtrl))
            _cameraPosition -= _cameraUp * speed;

        // Speed modifiers
        if (_pressedKeys.Contains(Key.LeftShift))
            _moveSpeed = 25f;
        else
            _moveSpeed = 10f;
    }

    #endregion

    #region Object Picking and Gizmos

    private (Vector3 origin, Vector3 direction) GetMouseRay(Point screenPos)
    {
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(
            MathF.PI / 4f, (float)(Bounds.Width / Bounds.Height), 0.1f, 1000f);
        var view = Matrix4x4.CreateLookAt(_cameraPosition, _cameraPosition + _cameraFront, _cameraUp);

        Matrix4x4.Invert(projection * view, out var invViewProj);

        var ndcX = (float)(2.0 * screenPos.X / Bounds.Width - 1.0);
        var ndcY = (float)(1.0 - 2.0 * screenPos.Y / Bounds.Height);

        var nearPoint = Vector4.Transform(new Vector4(ndcX, ndcY, -1f, 1f), invViewProj);
        var farPoint = Vector4.Transform(new Vector4(ndcX, ndcY, 1f, 1f), invViewProj);

        nearPoint /= nearPoint.W;
        farPoint /= farPoint.W;

        var direction = Vector3.Normalize(new Vector3(farPoint.X - nearPoint.X, farPoint.Y - nearPoint.Y, farPoint.Z - nearPoint.Z));

        return (_cameraPosition, direction);
    }

    private void PickObject(Point screenPos)
    {
        if (SceneData == null) return;

        var (origin, direction) = GetMouseRay(screenPos);

        // Deselect previous
        if (_selectedObject != null)
        {
            _selectedObject.IsSelected = false;
        }

        _selectedObject = SceneData.PickObject(origin, direction);

        if (_selectedObject != null)
        {
            _selectedObject.IsSelected = true;
        }

        SelectionChanged?.Invoke(this, _selectedObject);
    }

    private int GetHoveredGizmoAxis(in (Vector3 origin, Vector3 direction) ray, Vector3 gizmoCenter)
    {
        const float axisLength = 1.5f;
        const float axisRadius = 0.15f;

        float closestDist = float.MaxValue;
        int closestAxis = -1;

        // Test X axis (red)
        if (RayIntersectsCylinder(ray.origin, ray.direction, gizmoCenter, gizmoCenter + Vector3.UnitX * axisLength, axisRadius, out var dist))
        {
            if (dist < closestDist) { closestDist = dist; closestAxis = 0; }
        }

        // Test Y axis (green)
        if (RayIntersectsCylinder(ray.origin, ray.direction, gizmoCenter, gizmoCenter + Vector3.UnitY * axisLength, axisRadius, out dist))
        {
            if (dist < closestDist) { closestDist = dist; closestAxis = 1; }
        }

        // Test Z axis (blue)
        if (RayIntersectsCylinder(ray.origin, ray.direction, gizmoCenter, gizmoCenter + Vector3.UnitZ * axisLength, axisRadius, out dist))
        {
            if (dist < closestDist) { closestDist = dist; closestAxis = 2; }
        }

        return closestAxis;
    }

    private static bool RayIntersectsCylinder(Vector3 rayOrigin, Vector3 rayDir, Vector3 cylStart, Vector3 cylEnd, float radius, out float distance)
    {
        distance = float.MaxValue;

        var cylDir = cylEnd - cylStart;
        var cylLen = cylDir.Length();
        if (cylLen < 0.0001f) return false;
        cylDir /= cylLen;

        var dp = rayOrigin - cylStart;
        var a = Vector3.Dot(rayDir, rayDir) - MathF.Pow(Vector3.Dot(rayDir, cylDir), 2);
        var b = 2 * (Vector3.Dot(rayDir, dp) - Vector3.Dot(rayDir, cylDir) * Vector3.Dot(dp, cylDir));
        var c = Vector3.Dot(dp, dp) - MathF.Pow(Vector3.Dot(dp, cylDir), 2) - radius * radius;

        var discriminant = b * b - 4 * a * c;
        if (discriminant < 0) return false;

        var t = (-b - MathF.Sqrt(discriminant)) / (2 * a);
        if (t < 0) return false;

        var hitPoint = rayOrigin + rayDir * t;
        var projLen = Vector3.Dot(hitPoint - cylStart, cylDir);

        if (projLen >= 0 && projLen <= cylLen)
        {
            distance = t;
            return true;
        }

        return false;
    }

    private Vector3 GetDragDelta((Vector3 origin, Vector3 direction) ray, Vector3 planeNormal)
    {
        // Intersect ray with plane
        var d = Vector3.Dot(_dragStartPos, planeNormal);
        var t = (d - Vector3.Dot(ray.origin, planeNormal)) / Vector3.Dot(ray.direction, planeNormal);

        if (t > 0)
        {
            var hitPoint = ray.origin + ray.direction * t;
            return hitPoint - _dragStartPos;
        }

        return Vector3.Zero;
    }

    #endregion

    #region OpenGL Rendering

    public bool HitTest(Point point) => true;

    private void CheckError(string location)
    {
        if (_gl == null || !_loaded) return;

        GLEnum err;
        while ((err = _gl.GetError()) != GLEnum.NoError)
        {
            Debug.WriteLine($"OpenGL Error at {location}: {err}");
        }
    }

    private uint LoadShader(ShaderType type, string source)
    {
        if (_gl == null) return 0;

        var shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);

        var log = _gl.GetShaderInfoLog(shader);
        if (!string.IsNullOrWhiteSpace(log))
        {
            Debug.WriteLine($"Shader compilation error ({type}): {log}");
        }

        return shader;
    }

    private uint CreateShaderProgram(string vertexSource, string fragmentSource)
    {
        if (_gl == null) return 0;

        var vs = LoadShader(ShaderType.VertexShader, vertexSource);
        var fs = LoadShader(ShaderType.FragmentShader, fragmentSource);

        var program = _gl.CreateProgram();
        _gl.AttachShader(program, vs);
        _gl.AttachShader(program, fs);
        _gl.LinkProgram(program);

        _gl.DeleteShader(vs);
        _gl.DeleteShader(fs);

        return program;
    }

    protected override unsafe void OnOpenGlInit(GlInterface glInterface)
    {
        if (_loaded) return;
        _loaded = true;

        base.OnOpenGlInit(glInterface);
        _gl = GL.GetApi(glInterface.GetProcAddress);

        _gl.Enable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        // Create shaders
        _mainShaderProgram = CreateShaderProgram(SceneViewShaders.VERTEX_SOURCE, SceneViewShaders.FRAGMENT_SOURCE);
        _gridShaderProgram = CreateShaderProgram(SceneViewShaders.GRID_VERTEX_SOURCE, SceneViewShaders.GRID_FRAGMENT_SOURCE);
        _gizmoShaderProgram = CreateShaderProgram(SceneViewShaders.GIZMO_VERTEX_SOURCE, SceneViewShaders.GIZMO_FRAGMENT_SOURCE);

        CreateGridBuffers();

        CheckError("Init");
    }

    private unsafe void CreateGridBuffers()
    {
        if (_gl == null) return;

        // Create a large ground plane for grid
        float size = 100f;
        float[] gridVertices =
        [
            -size, 0, -size,
            size, 0, -size,
            size, 0, size,
            -size, 0, -size,
            size, 0, size,
            -size, 0, size,
        ];

        _gridVao = _gl.GenVertexArray();
        _gridVbo = _gl.GenBuffer();

        _gl.BindVertexArray(_gridVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _gridVbo);

        fixed (float* ptr = gridVertices)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(gridVertices.Length * sizeof(float)), ptr, BufferUsageARB.StaticDraw);
        }

        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);
        _gl.EnableVertexAttribArray(0);

        _gl.BindVertexArray(0);
    }

    private unsafe void BuildSceneBuffers()
    {
        if (_gl == null || SceneData == null) return;

        // Clean up old buffers
        foreach (var buf in _objectBuffers.Values)
        {
            _gl.DeleteVertexArray(buf.Vao);
            _gl.DeleteBuffer(buf.Vbo);
            _gl.DeleteBuffer(buf.Ebo);
            if (buf.HasTexture)
            {
                _gl.DeleteTexture(buf.TextureId);
            }
        }
        _objectBuffers.Clear();

        // Create buffers for each object with a mesh
        foreach (var obj in SceneData.AllObjects)
        {
            if (!obj.HasMesh || obj.Mesh == null) continue;

            var mesh = obj.Mesh;
            var vertexCount = mesh.Vertices.Length / 3;

            var vertices = new Vertex[vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                var pos = new Vector3(mesh.Vertices[i * 3], mesh.Vertices[i * 3 + 1], mesh.Vertices[i * 3 + 2]);

                var normal = Vector3.UnitY;
                if (mesh.Normals != null && mesh.Normals.Length >= (i + 1) * 3)
                {
                    normal = new Vector3(-mesh.Normals[i * 3], mesh.Normals[i * 3 + 1], mesh.Normals[i * 3 + 2]);
                }

                var texCoord = Vector2.Zero;
                if (obj.UVs != null && obj.UVs.Length >= (i + 1) * 2)
                {
                    texCoord = new Vector2(obj.UVs[i * 2], obj.UVs[i * 2 + 1]);
                }

                vertices[i] = new Vertex
                {
                    Position = pos,
                    Normal = normal,
                    TexCoord = texCoord
                };
            }

            var buffers = new ObjectBuffers
            {
                IndexCount = mesh.Indices.Length
            };

            buffers.Vao = _gl.GenVertexArray();
            buffers.Vbo = _gl.GenBuffer();
            buffers.Ebo = _gl.GenBuffer();

            _gl.BindVertexArray(buffers.Vao);

            // Vertex buffer
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffers.Vbo);
            var vertexSize = Marshal.SizeOf<Vertex>();
            fixed (Vertex* ptr = vertices)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * vertexSize), ptr, BufferUsageARB.StaticDraw);
            }

            // Index buffer
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, buffers.Ebo);
            fixed (ushort* ptr = mesh.Indices)
            {
                _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(mesh.Indices.Length * sizeof(ushort)), ptr, BufferUsageARB.StaticDraw);
            }

            // Vertex attributes
            _gl.VertexAttribPointer(SceneViewShaders.POSITION_LOC, 3, VertexAttribPointerType.Float, false, (uint)vertexSize, (void*)0);
            _gl.VertexAttribPointer(SceneViewShaders.NORMAL_LOC, 3, VertexAttribPointerType.Float, false, (uint)vertexSize, (void*)12);
            _gl.VertexAttribPointer(SceneViewShaders.TEXCOORD_LOC, 2, VertexAttribPointerType.Float, false, (uint)vertexSize, (void*)24);

            _gl.EnableVertexAttribArray(SceneViewShaders.POSITION_LOC);
            _gl.EnableVertexAttribArray(SceneViewShaders.NORMAL_LOC);
            _gl.EnableVertexAttribArray(SceneViewShaders.TEXCOORD_LOC);

            // Texture
            if (obj.HasTexture && obj.TextureData != null)
            {
                buffers.TextureId = _gl.GenTexture();
                buffers.HasTexture = true;

                _gl.BindTexture(TextureTarget.Texture2D, buffers.TextureId);
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

                fixed (byte* ptr = obj.TextureData)
                {
                    _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)obj.TextureWidth, (uint)obj.TextureHeight, 0,
                        PixelFormat.Bgra, PixelType.UnsignedByte, ptr);
                }
                _gl.GenerateMipmap(TextureTarget.Texture2D);
            }

            _gl.BindVertexArray(0);

            _objectBuffers[obj] = buffers;
        }

        CheckError("BuildSceneBuffers");
    }

    protected override unsafe void OnOpenGlRender(GlInterface glInterface, int fb)
    {
        if (_gl == null) return;

        // Calculate delta time
        var now = DateTime.Now;
        _deltaTime = (float)(now - _lastFrameTime).TotalSeconds;
        _lastFrameTime = now;

        // Process input
        ProcessKeyboardInput();

        // Handle dirty scene
        if (_dirtyScene)
        {
            _dirtyScene = false;
            BuildSceneBuffers();
        }

        // Clear
        _gl.ClearColor(0.15f, 0.15f, 0.18f, 1f);
        _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
        _gl.Viewport(0, 0, (uint)Bounds.Width, (uint)Bounds.Height);

        // Create matrices
        var aspectRatio = (float)(Bounds.Width / Bounds.Height);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4f, aspectRatio, 0.1f, 1000f);
        var view = Matrix4x4.CreateLookAt(_cameraPosition, _cameraPosition + _cameraFront, _cameraUp);

        // Render grid
        RenderGrid(projection, view);

        // Render objects
        RenderObjects(projection, view);

        // Render gizmo for selected object
        if (_selectedObject != null)
        {
            RenderGizmo(projection, view, _selectedObject.LocalPosition);
        }

        CheckError("Render");

        RequestNextFrameRendering();
    }

    private unsafe void RenderGrid(Matrix4x4 projection, Matrix4x4 view)
    {
        if (_gl == null) return;

        _gl.UseProgram(_gridShaderProgram);

        var projLoc = _gl.GetUniformLocation(_gridShaderProgram, "uProjection");
        var viewLoc = _gl.GetUniformLocation(_gridShaderProgram, "uView");
        var gridSizeLoc = _gl.GetUniformLocation(_gridShaderProgram, "uGridSize");
        var gridColorLoc = _gl.GetUniformLocation(_gridShaderProgram, "uGridColor");

        _gl.UniformMatrix4(projLoc, 1, false, &projection.M11);
        _gl.UniformMatrix4(viewLoc, 1, false, &view.M11);
        _gl.Uniform1(gridSizeLoc, 1f);
        _gl.Uniform3(gridColorLoc, 0.5f, 0.5f, 0.5f);

        _gl.BindVertexArray(_gridVao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        _gl.BindVertexArray(0);
    }

    private unsafe void RenderObjects(Matrix4x4 projection, Matrix4x4 view)
    {
        if (_gl == null || SceneData == null) return;

        _gl.UseProgram(_mainShaderProgram);

        var projLoc = _gl.GetUniformLocation(_mainShaderProgram, "uProjection");
        var viewLoc = _gl.GetUniformLocation(_mainShaderProgram, "uView");
        var modelLoc = _gl.GetUniformLocation(_mainShaderProgram, "uModel");
        var lightDirLoc = _gl.GetUniformLocation(_mainShaderProgram, "uDirectionalLightDir");
        var lightColorLoc = _gl.GetUniformLocation(_mainShaderProgram, "uDirectionalLightColor");
        var cameraPosLoc = _gl.GetUniformLocation(_mainShaderProgram, "uCameraPos");
        var hasTextureLoc = _gl.GetUniformLocation(_mainShaderProgram, "uHasTexture");
        var isSelectedLoc = _gl.GetUniformLocation(_mainShaderProgram, "uIsSelected");
        var baseColorLoc = _gl.GetUniformLocation(_mainShaderProgram, "uBaseColor");
        var textureLoc = _gl.GetUniformLocation(_mainShaderProgram, "uTexture");

        _gl.UniformMatrix4(projLoc, 1, false, &projection.M11);
        _gl.UniformMatrix4(viewLoc, 1, false, &view.M11);
        _gl.Uniform3(lightDirLoc, -0.5f, -1f, -0.3f);
        _gl.Uniform3(lightColorLoc, 1f, 1f, 1f);
        _gl.Uniform3(cameraPosLoc, _cameraPosition.X, _cameraPosition.Y, _cameraPosition.Z);
        _gl.Uniform1(textureLoc, 0);

        foreach (var obj in SceneData.AllObjects)
        {
            if (!_objectBuffers.TryGetValue(obj, out var buffers)) continue;

            var model = obj.WorldMatrix;
            _gl.UniformMatrix4(modelLoc, 1, false, &model.M11);
            _gl.Uniform1(hasTextureLoc, buffers.HasTexture ? 1 : 0);
            _gl.Uniform1(isSelectedLoc, obj.IsSelected ? 1 : 0);
            _gl.Uniform3(baseColorLoc, 0.7f, 0.7f, 0.7f);

            if (buffers.HasTexture)
            {
                _gl.ActiveTexture(TextureUnit.Texture0);
                _gl.BindTexture(TextureTarget.Texture2D, buffers.TextureId);
            }

            _gl.BindVertexArray(buffers.Vao);
            _gl.DrawElements(PrimitiveType.Triangles, (uint)buffers.IndexCount, DrawElementsType.UnsignedShort, (void*)0);
            _gl.BindVertexArray(0);
        }
    }

    private unsafe void RenderGizmo(Matrix4x4 projection, Matrix4x4 view, Vector3 position)
    {
        if (_gl == null) return;

        _gl.UseProgram(_gizmoShaderProgram);
        _gl.Disable(EnableCap.DepthTest);

        var projLoc = _gl.GetUniformLocation(_gizmoShaderProgram, "uProjection");
        var viewLoc = _gl.GetUniformLocation(_gizmoShaderProgram, "uView");
        var modelLoc = _gl.GetUniformLocation(_gizmoShaderProgram, "uModel");
        var colorLoc = _gl.GetUniformLocation(_gizmoShaderProgram, "uColor");

        _gl.UniformMatrix4(projLoc, 1, false, &projection.M11);
        _gl.UniformMatrix4(viewLoc, 1, false, &view.M11);

        const float axisLength = 1.5f;

        // Draw X axis (red)
        DrawLine(position, position + Vector3.UnitX * axisLength, modelLoc, colorLoc,
            _activeAxis == 0 ? new Vector3(1f, 0.6f, 0f) : new Vector3(1f, 0f, 0f));

        // Draw Y axis (green)
        DrawLine(position, position + Vector3.UnitY * axisLength, modelLoc, colorLoc,
            _activeAxis == 1 ? new Vector3(1f, 0.6f, 0f) : new Vector3(0f, 1f, 0f));

        // Draw Z axis (blue)
        DrawLine(position, position + Vector3.UnitZ * axisLength, modelLoc, colorLoc,
            _activeAxis == 2 ? new Vector3(1f, 0.6f, 0f) : new Vector3(0f, 0f, 1f));

        _gl.Enable(EnableCap.DepthTest);
    }

    private unsafe void DrawLine(Vector3 start, Vector3 end, int modelLoc, int colorLoc, Vector3 color)
    {
        if (_gl == null) return;

        float[] vertices = [start.X, start.Y, start.Z, end.X, end.Y, end.Z];

        var vao = _gl.GenVertexArray();
        var vbo = _gl.GenBuffer();

        _gl.BindVertexArray(vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);

        fixed (float* ptr = vertices)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), ptr, BufferUsageARB.StaticDraw);
        }

        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);
        _gl.EnableVertexAttribArray(0);

        var model = Matrix4x4.Identity;
        _gl.UniformMatrix4(modelLoc, 1, false, &model.M11);
        _gl.Uniform3(colorLoc, color.X, color.Y, color.Z);

        _gl.LineWidth(3f);
        _gl.DrawArrays(PrimitiveType.Lines, 0, 2);

        _gl.DeleteVertexArray(vao);
        _gl.DeleteBuffer(vbo);

        _gl.BindVertexArray(0);
    }

    protected override void OnOpenGlDeinit(GlInterface glInterface)
    {
        if (_gl == null) return;

        foreach (var buf in _objectBuffers.Values)
        {
            _gl.DeleteVertexArray(buf.Vao);
            _gl.DeleteBuffer(buf.Vbo);
            _gl.DeleteBuffer(buf.Ebo);
            if (buf.HasTexture)
            {
                _gl.DeleteTexture(buf.TextureId);
            }
        }
        _objectBuffers.Clear();

        _gl.DeleteVertexArray(_gridVao);
        _gl.DeleteBuffer(_gridVbo);

        _gl.DeleteProgram(_mainShaderProgram);
        _gl.DeleteProgram(_gridShaderProgram);
        _gl.DeleteProgram(_gizmoShaderProgram);
    }

    #endregion
}
