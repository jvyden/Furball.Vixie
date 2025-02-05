#if USE_IMGUI
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Numerics;
using System.Reflection;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Input.Extensions;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Veldrid;
using Key=Silk.NET.Input.Key;
using MouseButton=Silk.NET.Input.MouseButton;
using Point=System.Drawing.Point;

namespace Furball.Vixie.Backends.Veldrid; 

public class ImGuiController : IDisposable {
    private          IView         _view  = null!;
    private          IInputContext _input = null!;
    private          bool          _frameBegun;
    private readonly List<char>    _pressedChars = new();
    private          IKeyboard     _keyboard     = null!;

    private int            _windowWidth;
    private int            _windowHeight;
    private GraphicsDevice _gd = null!;

    // Device objects
    private DeviceBuffer   _vertexBuffer     = null!;
    private DeviceBuffer   _indexBuffer      = null!;
    private DeviceBuffer   _projMatrixBuffer = null!;
    private Texture?       _fontTexture      = null!;
    private Shader         _vertexShader     = null!;
    private Shader         _fragmentShader   = null!;
    private ResourceLayout _layout           = null!;
    private ResourceLayout _textureLayout    = null!;
    private Pipeline       _pipeline         = null!;
    private ResourceSet    _mainResourceSet  = null!;
    private ResourceSet?   _fontTextureResourceSet;
    private IntPtr         _fontAtlasId = (IntPtr)1;

    // Image trackers
    // ReSharper disable CollectionNeverUpdated.Local
    private readonly Dictionary<IntPtr, ResourceSetInfo> _viewsById          = new();
    private readonly List<IDisposable>                   _ownedResources     = new();
    // ReSharper restore CollectionNeverUpdated.Local
    private          ColorSpaceHandling                  _colorSpaceHandling = ColorSpaceHandling.Legacy;
    public           Assembly                            Assembly;

    private IntPtr _context;

    /// <summary>
    /// Constructs a new ImGuiController with font configuration and onConfigure Action.
    /// </summary>
    public ImGuiController(GraphicsDevice gd, OutputDescription outputDescription, IView view, IInputContext input) {
        this.Assembly = typeof(ImGuiController).Assembly;

        this.Init(gd, view, input);

        var io = ImGui.GetIO();

        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;

        this.CreateDeviceResources(gd, outputDescription, this._colorSpaceHandling);
        SetKeyMappings();

        this.SetPerFrameImGuiData(1f / 60f);

        this.BeginFrame();
    }

    private void Init(GraphicsDevice gd, IView view, IInputContext input) {
        this._gd           = gd;
        this._view         = view;
        this._input        = input;
        this._windowWidth  = view.Size.X;
        this._windowHeight = view.Size.Y;

        this._context = ImGui.CreateContext();
        ImGui.SetCurrentContext(this._context);
        ImGui.StyleColorsDark();
    }

    private void BeginFrame() {
        ImGui.NewFrame();
        this._frameBegun       =  true;
        this._keyboard         =  this._input.Keyboards[0];
        this._view.Resize      += this.WindowResized;
        this._keyboard.KeyChar += this.OnKeyChar;
    }

    private void OnKeyChar(IKeyboard arg1, char arg2) {
        this._pressedChars.Add(arg2);
    }

    private void WindowResized(Vector2D<int> size) {
        this._windowWidth  = size.X;
        this._windowHeight = size.Y;
    }

    /// <summary>
    /// Renders the ImGui draw list data.
    /// This method requires a <see cref="GraphicsDevice"/> because it may create new DeviceBuffers if the size of vertex
    /// or index data has increased beyond the capacity of the existing buffers.
    /// A <see cref="CommandList"/> is needed to submit drawing and resource update commands.
    /// </summary>
    public void Render(GraphicsDevice gd, CommandList cl) {
        if (this._frameBegun) {
            this._frameBegun = false;
            ImGui.Render();
            this.RenderImDrawData(ImGui.GetDrawData(), gd, cl);
        }
    }

    /// <summary>
    /// Updates ImGui input and IO configuration state.
    /// </summary>
    public void Update(float deltaSeconds) {
        if (this._frameBegun) {
            ImGui.Render();
        }

        this.SetPerFrameImGuiData(deltaSeconds);
        this.UpdateImGuiInput();

        this._frameBegun = true;
        ImGui.NewFrame();
    }

    /// <summary>
    /// Sets per-frame data based on the associated window.
    /// This is called by Update(float).
    /// </summary>
    private void SetPerFrameImGuiData(float deltaSeconds) {
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(this._windowWidth, this._windowHeight);

        if (this._windowWidth > 0 && this._windowHeight > 0) {
            io.DisplayFramebufferScale = new Vector2(this._view.FramebufferSize.X / this._windowWidth, this._view.FramebufferSize.Y / this._windowHeight);
        }

        io.DeltaTime = deltaSeconds; // DeltaTime is in seconds.
    }

    private Key[] _keyEnumValues = (Key[])Enum.GetValues(typeof(Key));
    private void UpdateImGuiInput() {
        var io = ImGui.GetIO();

        var mouseState    = this._input.Mice[0].CaptureState();
        var keyboardState = this._input.Keyboards[0];

        io.MouseDown[0] = mouseState.IsButtonPressed(MouseButton.Left);
        io.MouseDown[1] = mouseState.IsButtonPressed(MouseButton.Right);
        io.MouseDown[2] = mouseState.IsButtonPressed(MouseButton.Middle);

        var point = new Point((int)mouseState.Position.X, (int)mouseState.Position.Y);
        io.MousePos = new Vector2(point.X, point.Y);

        var wheel = mouseState.GetScrollWheels()[0];
        io.MouseWheel  = wheel.Y;
        io.MouseWheelH = wheel.X;

        for (var i = 0; i < this._keyEnumValues.Length; i++) {
            Key key = this._keyEnumValues[i];
            if (key == Key.Unknown) {
                continue;
            }
            io.KeysDown[(int)key] = keyboardState.IsKeyPressed(key);
        }

        foreach (var c in this._pressedChars) {
            io.AddInputCharacter(c);
        }

        this._pressedChars.Clear();

        io.KeyCtrl  = keyboardState.IsKeyPressed(Key.ControlLeft) || keyboardState.IsKeyPressed(Key.ControlRight);
        io.KeyAlt   = keyboardState.IsKeyPressed(Key.AltLeft)     || keyboardState.IsKeyPressed(Key.AltRight);
        io.KeyShift = keyboardState.IsKeyPressed(Key.ShiftLeft)   || keyboardState.IsKeyPressed(Key.ShiftRight);
        io.KeySuper = keyboardState.IsKeyPressed(Key.SuperLeft)   || keyboardState.IsKeyPressed(Key.SuperRight);
    }

    internal void PressChar(char keyChar) {
        this._pressedChars.Add(keyChar);
    }

    private static void SetKeyMappings() {
        var io = ImGui.GetIO();
        io.KeyMap[(int)ImGuiKey.Tab]        = (int)Key.Tab;
        io.KeyMap[(int)ImGuiKey.LeftArrow]  = (int)Key.Left;
        io.KeyMap[(int)ImGuiKey.RightArrow] = (int)Key.Right;
        io.KeyMap[(int)ImGuiKey.UpArrow]    = (int)Key.Up;
        io.KeyMap[(int)ImGuiKey.DownArrow]  = (int)Key.Down;
        io.KeyMap[(int)ImGuiKey.PageUp]     = (int)Key.PageUp;
        io.KeyMap[(int)ImGuiKey.PageDown]   = (int)Key.PageDown;
        io.KeyMap[(int)ImGuiKey.Home]       = (int)Key.Home;
        io.KeyMap[(int)ImGuiKey.End]        = (int)Key.End;
        io.KeyMap[(int)ImGuiKey.Delete]     = (int)Key.Delete;
        io.KeyMap[(int)ImGuiKey.Backspace]  = (int)Key.Backspace;
        io.KeyMap[(int)ImGuiKey.Enter]      = (int)Key.Enter;
        io.KeyMap[(int)ImGuiKey.Escape]     = (int)Key.Escape;
        io.KeyMap[(int)ImGuiKey.A]          = (int)Key.A;
        io.KeyMap[(int)ImGuiKey.C]          = (int)Key.C;
        io.KeyMap[(int)ImGuiKey.V]          = (int)Key.V;
        io.KeyMap[(int)ImGuiKey.X]          = (int)Key.X;
        io.KeyMap[(int)ImGuiKey.Y]          = (int)Key.Y;
        io.KeyMap[(int)ImGuiKey.Z]          = (int)Key.Z;
    }

    public ResourceSet GetImageResourceSet(IntPtr imGuiBinding) {
        if (!this._viewsById.TryGetValue(imGuiBinding, out ResourceSetInfo rsi)) {
            throw new InvalidOperationException("No registered ImGui binding with id " + imGuiBinding);
        }

        return rsi.ResourceSet;
    }

    private unsafe void RenderImDrawData(ImDrawDataPtr drawData, GraphicsDevice gd, CommandList cl) {
        uint vertexOffsetInVertices = 0;
        uint indexOffsetInElements  = 0;

        if (drawData.CmdListsCount == 0) {
            return;
        }

        uint totalVbSize = (uint)(drawData.TotalVtxCount * sizeof(ImDrawVert));
        if (totalVbSize > this._vertexBuffer.SizeInBytes) {
            this._vertexBuffer.Dispose();
            this._vertexBuffer = gd.ResourceFactory.CreateBuffer(new BufferDescription((uint)(totalVbSize * 1.5f), BufferUsage.VertexBuffer | BufferUsage.Dynamic));
        }

        uint totalIbSize = (uint)(drawData.TotalIdxCount * sizeof(ushort));
        if (totalIbSize > this._indexBuffer.SizeInBytes) {
            this._indexBuffer.Dispose();
            this._indexBuffer = gd.ResourceFactory.CreateBuffer(new BufferDescription((uint)(totalIbSize * 1.5f), BufferUsage.IndexBuffer | BufferUsage.Dynamic));
        }

        for (int i = 0; i < drawData.CmdListsCount; i++) {
            ImDrawListPtr cmdList = drawData.CmdListsRange[i];

            cl.UpdateBuffer(this._vertexBuffer, vertexOffsetInVertices * (uint)sizeof(ImDrawVert), cmdList.VtxBuffer.Data, (uint)(cmdList.VtxBuffer.Size * sizeof(ImDrawVert)));

            cl.UpdateBuffer(this._indexBuffer, indexOffsetInElements * sizeof(ushort), cmdList.IdxBuffer.Data, (uint)(cmdList.IdxBuffer.Size * sizeof(ushort)));

            vertexOffsetInVertices += (uint)cmdList.VtxBuffer.Size;
            indexOffsetInElements  += (uint)cmdList.IdxBuffer.Size;
        }

        // Setup orthographic projection matrix into our constant buffer
        {
            var io = ImGui.GetIO();

            Matrix4x4 mvp = Matrix4x4.CreateOrthographicOffCenter(0f, io.DisplaySize.X, io.DisplaySize.Y, 0.0f, -1.0f, 1.0f);

            this._gd.UpdateBuffer(this._projMatrixBuffer, 0, ref mvp);
        }

        cl.SetVertexBuffer(0, this._vertexBuffer);
        cl.SetIndexBuffer(this._indexBuffer, IndexFormat.UInt16);
        cl.SetPipeline(this._pipeline);
        cl.SetGraphicsResourceSet(0, this._mainResourceSet);

        drawData.ScaleClipRects(ImGui.GetIO().DisplayFramebufferScale);

        // Render command lists
        int vtxOffset = 0;
        int idxOffset = 0;
        for (int n = 0; n < drawData.CmdListsCount; n++) {
            ImDrawListPtr cmdList = drawData.CmdListsRange[n];
            for (int cmdI = 0; cmdI < cmdList.CmdBuffer.Size; cmdI++) {
                ImDrawCmdPtr pcmd = cmdList.CmdBuffer[cmdI];
                if (pcmd.UserCallback != IntPtr.Zero) {
                    throw new NotImplementedException();
                }
                if (pcmd.TextureId != IntPtr.Zero) {
                    if (pcmd.TextureId == this._fontAtlasId) {
                        cl.SetGraphicsResourceSet(1, this._fontTextureResourceSet);
                    } else {
                        cl.SetGraphicsResourceSet(1, this.GetImageResourceSet(pcmd.TextureId));
                    }
                }

                cl.SetScissorRect(0, (uint)pcmd.ClipRect.X, (uint)pcmd.ClipRect.Y, (uint)(pcmd.ClipRect.Z - pcmd.ClipRect.X), (uint)(pcmd.ClipRect.W - pcmd.ClipRect.Y));

                cl.DrawIndexed(pcmd.ElemCount, 1, pcmd.IdxOffset + (uint)idxOffset, (int)(pcmd.VtxOffset + vtxOffset), 0);
            }

            idxOffset += cmdList.IdxBuffer.Size;
            vtxOffset += cmdList.VtxBuffer.Size;
        }
            
        cl.SetFullScissorRect(0);
    }

    private byte[] GetEmbeddedResourceBytes(string resourceName) {
        using Stream s   = this.Assembly.GetManifestResourceStream(resourceName) ?? throw new Exception();
        byte[]       ret = new byte[s.Length];
        _ = s.Read(ret, 0, (int)s.Length);
        return ret;

    }

    private byte[] LoadEmbeddedShaderCode(ResourceFactory factory, string name, ShaderStages stage, ColorSpaceHandling colorSpaceHandling) {
        switch (factory.BackendType) {
            case GraphicsBackend.Direct3D11: {
                if (stage == ShaderStages.Vertex && colorSpaceHandling == ColorSpaceHandling.Legacy) { name += "-legacy"; }
                string resourceName = name + ".hlsl.bytes";
                return this.GetEmbeddedResourceBytes(resourceName);
            }
            case GraphicsBackend.OpenGL: {
                if (stage == ShaderStages.Vertex && colorSpaceHandling == ColorSpaceHandling.Legacy) { name += "-legacy"; }
                string resourceName = name + ".glsl";
                return this.GetEmbeddedResourceBytes(resourceName);
            }
            case GraphicsBackend.OpenGLES: {
                if (stage == ShaderStages.Vertex && colorSpaceHandling == ColorSpaceHandling.Legacy) { name += "-legacy"; }
                string resourceName = name + ".glsles";
                return this.GetEmbeddedResourceBytes(resourceName);
            }
            case GraphicsBackend.Vulkan: {
                string resourceName = name + ".spv";
                return this.GetEmbeddedResourceBytes(resourceName);
            }
            case GraphicsBackend.Metal: {
                string resourceName = name + ".metallib";
                return this.GetEmbeddedResourceBytes(resourceName);
            }
            default:
                throw new NotImplementedException();
        }
    }

    private void CreateDeviceResources(GraphicsDevice gd, OutputDescription outputDescription, ColorSpaceHandling colorSpaceHandling) {
        this._gd                 = gd;
        this._colorSpaceHandling = colorSpaceHandling;
        ResourceFactory factory = gd.ResourceFactory;
        this._vertexBuffer      = factory.CreateBuffer(new BufferDescription(10000, BufferUsage.VertexBuffer | BufferUsage.Dynamic));
        this._vertexBuffer.Name = "ImGui.NET Vertex Buffer";
        this._indexBuffer       = factory.CreateBuffer(new BufferDescription(2000, BufferUsage.IndexBuffer | BufferUsage.Dynamic));
        this._indexBuffer.Name  = "ImGui.NET Index Buffer";

        this._projMatrixBuffer      = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer | BufferUsage.Dynamic));
        this._projMatrixBuffer.Name = "ImGui.NET Projection Buffer";

        byte[] vertexShaderBytes   = this.LoadEmbeddedShaderCode(gd.ResourceFactory, "imgui-vertex", ShaderStages.Vertex,   this._colorSpaceHandling);
        byte[] fragmentShaderBytes = this.LoadEmbeddedShaderCode(gd.ResourceFactory, "imgui-frag",   ShaderStages.Fragment, this._colorSpaceHandling);
        this._vertexShader   = factory.CreateShader(new ShaderDescription(ShaderStages.Vertex,   vertexShaderBytes,   this._gd.BackendType == GraphicsBackend.Vulkan ? "main" : "VS"));
        this._fragmentShader = factory.CreateShader(new ShaderDescription(ShaderStages.Fragment, fragmentShaderBytes, this._gd.BackendType == GraphicsBackend.Vulkan ? "main" : "FS"));

        VertexLayoutDescription[] vertexLayouts = new VertexLayoutDescription[] {
            new(new VertexElementDescription("in_position", VertexElementSemantic.Position, VertexElementFormat.Float2), new VertexElementDescription("in_texCoord", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2), new VertexElementDescription("in_color", VertexElementSemantic.Color, VertexElementFormat.Byte4_Norm))
        };

        this._layout        = factory.CreateResourceLayout(new ResourceLayoutDescription(new ResourceLayoutElementDescription("ProjectionMatrixBuffer", ResourceKind.UniformBuffer,   ShaderStages.Vertex), new ResourceLayoutElementDescription("MainSampler", ResourceKind.Sampler, ShaderStages.Fragment)));
        this._textureLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(new ResourceLayoutElementDescription("MainTexture",            ResourceKind.TextureReadOnly, ShaderStages.Fragment)));

        GraphicsPipelineDescription pd = new(BlendStateDescription.SingleAlphaBlend,
                                             new DepthStencilStateDescription(false, false, ComparisonKind.Always),
                                             new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.Clockwise, true, true),
                                             PrimitiveTopology.TriangleList,
                                             new ShaderSetDescription(vertexLayouts,
                                                                      new[] {
                                                                          this._vertexShader, this._fragmentShader
                                                                      },
                                                                      new[] {
                                                                          new SpecializationConstant(0, gd.IsClipSpaceYInverted), new SpecializationConstant(1, this._colorSpaceHandling == ColorSpaceHandling.Legacy),
                                                                      }),
                                             new ResourceLayout[] {
                                                 this._layout, this._textureLayout
                                             },
                                             outputDescription,
                                             ResourceBindingModel.Default);
        this._pipeline = factory.CreateGraphicsPipeline(ref pd);

        this._mainResourceSet = factory.CreateResourceSet(new ResourceSetDescription(this._layout, this._projMatrixBuffer, gd.PointSampler));

        this.RecreateFontDeviceTexture(gd);
    }

    /// <summary>
    /// Creates the texture used to render text.
    /// </summary>
    private unsafe void RecreateFontDeviceTexture(GraphicsDevice gd) {
        ImGuiIOPtr io = ImGui.GetIO();
        // Build
        io.Fonts.GetTexDataAsRGBA32(out byte* pixels, out int width, out int height, out int bytesPerPixel);

        // Store our identifier
        io.Fonts.SetTexID(this._fontAtlasId);

        this._fontTexture?.Dispose();
        this._fontTexture      = gd.ResourceFactory.CreateTexture(TextureDescription.Texture2D((uint)width, (uint)height, 1, 1, PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.Sampled));
        this._fontTexture.Name = "ImGui.NET Font Texture";
        gd.UpdateTexture(this._fontTexture, (IntPtr)pixels, (uint)(bytesPerPixel * width * height), 0, 0, 0, (uint)width, (uint)height, 1, 0, 0);

        this._fontTextureResourceSet?.Dispose();
        this._fontTextureResourceSet = gd.ResourceFactory.CreateResourceSet(new ResourceSetDescription(this._textureLayout, this._fontTexture));

        io.Fonts.ClearTexData();
    }

    /// <summary>
    /// Frees all graphics resources used by the renderer.
    /// </summary>
    public void Dispose() {
        this._view.Resize      -= this.WindowResized;
        this._keyboard.KeyChar -= this.OnKeyChar;

        this._vertexBuffer.Dispose();
        this._indexBuffer.Dispose();
        this._projMatrixBuffer.Dispose();
        this._fontTexture?.Dispose();
        this._vertexShader.Dispose();
        this._fragmentShader.Dispose();
        this._layout.Dispose();
        this._textureLayout.Dispose();
        this._pipeline.Dispose();
        this._mainResourceSet.Dispose();
        this._fontTextureResourceSet?.Dispose();

        foreach (IDisposable resource in this._ownedResources) {
            resource.Dispose();
        }

        ImGui.DestroyContext(this._context);
        GC.SuppressFinalize(this);
    }

    [SuppressMessage("ReSharper", "NotAccessedField.Local")]
    private struct ResourceSetInfo {
        public readonly IntPtr      ImGuiBinding;
        public readonly ResourceSet ResourceSet;

        // ReSharper disable once UnusedMember.Local
        public ResourceSetInfo(IntPtr imGuiBinding, ResourceSet resourceSet) {
            this.ImGuiBinding = imGuiBinding;
            this.ResourceSet  = resourceSet;
        }
    }
}
#endif