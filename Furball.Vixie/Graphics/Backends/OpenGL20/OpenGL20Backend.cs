using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Numerics;
using System.Reflection.Metadata;
using Furball.Vixie.Graphics.Backends.OpenGL;
using Furball.Vixie.Graphics.Backends.OpenGL20.Abstractions;
using Furball.Vixie.Graphics.Backends.OpenGL41;
using Furball.Vixie.Graphics.Renderers;
using Furball.Vixie.Helpers;
using Kettu;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.OpenGL.Legacy;
using Silk.NET.OpenGL.Legacy.Extensions.EXT;
using Silk.NET.OpenGL.Legacy.Extensions.ImGui;
using Silk.NET.Windowing;
using BufferTargetARB=Silk.NET.OpenGL.BufferTargetARB;
using BufferUsageARB=Silk.NET.OpenGL.BufferUsageARB;
using FramebufferAttachment=Silk.NET.OpenGL.FramebufferAttachment;
using FramebufferTarget=Silk.NET.OpenGL.FramebufferTarget;
using InternalFormat=Silk.NET.OpenGL.InternalFormat;
using PixelFormat=Silk.NET.OpenGL.PixelFormat;
using PixelType=Silk.NET.OpenGL.PixelType;
using ProgramPropertyARB=Silk.NET.OpenGL.ProgramPropertyARB;
using RenderbufferTarget=Silk.NET.OpenGL.RenderbufferTarget;
using ShaderType=Silk.NET.OpenGL.ShaderType;
using TextureParameterName=Silk.NET.OpenGL.TextureParameterName;
using TextureTarget=Silk.NET.OpenGL.TextureTarget;
using TextureUnit=Silk.NET.OpenGL.TextureUnit;

namespace Furball.Vixie.Graphics.Backends.OpenGL20 {
    public class OpenGL20Backend : GraphicsBackend, IGLBasedBackend {
        private GL gl;

        private ImGuiController _imgui;
        
        public  Matrix4x4       ProjectionMatrix;
        
        public void CheckError(string message = "") {
            this.CheckErrorInternal(message);
        }

        /// <summary>
        /// Checks for OpenGL errors
        /// </summary>
        /// <param name="erorr"></param>
        [Conditional("DEBUG")]
        private void CheckErrorInternal(string erorr = "") {
            GLEnum error = this.gl.GetError();
            
            if (error != GLEnum.NoError) {
#if DEBUGWITHGL
                throw new Exception($"Got GL Error {error}!");
#else
                Debugger.Break();
                Logger.Log($"OpenGL Error! Code: {error.ToString()} Extra Info: {erorr}", LoggerLevelOpenGL20.InstanceError);
#endif
            }
        }
        
        public override void Initialize(IWindow window) {
            this.gl = window.CreateLegacyOpenGL();

            //TODO: Lets just assume they have it for now :^)
            this.framebufferObjectEXT = new ExtFramebufferObject(this.gl.Context);

#if DEBUGWITHGL
            unsafe {
                //Enables Debugging
                gl.Enable(EnableCap.DebugOutput);
                gl.Enable(EnableCap.DebugOutputSynchronous);
                gl.DebugMessageCallback(this.Callback, null);
            }
#endif
            
            //Setup blend mode
            this.gl.Enable(EnableCap.Blend);
            this.gl.BlendFunc(GLEnum.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            
            this.gl.Enable(EnableCap.Texture2D);
            
            Logger.Log($"OpenGL Version: {this.gl.GetStringS(StringName.Version)}",                LoggerLevelOpenGL20.InstanceInfo);
            Logger.Log($"GLSL Version:   {this.gl.GetStringS(StringName.ShadingLanguageVersion)}", LoggerLevelOpenGL20.InstanceInfo);
            Logger.Log($"OpenGL Vendor:  {this.gl.GetStringS(StringName.Vendor)}",                 LoggerLevelOpenGL20.InstanceInfo);
            Logger.Log($"Renderer:       {this.gl.GetStringS(StringName.Renderer)}",               LoggerLevelOpenGL20.InstanceInfo);

            this._imgui = new ImGuiController(this.gl, Global.GameInstance.WindowManager.GameWindow, Global.GameInstance._inputContext);
        }
        
        /// <summary>
        /// Debug Callback
        /// </summary>
        private void Callback(GLEnum source, GLEnum type, int id, GLEnum severity, int length, nint message, nint userparam) {
            string stringMessage = SilkMarshal.PtrToString(message);

            LoggerLevel level = severity switch {
                GLEnum.DebugSeverityHigh         => LoggerLevelDebugMessageCallback.InstanceHigh,
                GLEnum.DebugSeverityMedium       => LoggerLevelDebugMessageCallback.InstanceMedium,
                GLEnum.DebugSeverityLow          => LoggerLevelDebugMessageCallback.InstanceLow,
                GLEnum.DebugSeverityNotification => LoggerLevelDebugMessageCallback.InstanceNotification,
                _                                => null
            };

            Console.WriteLine(stringMessage);
        }
        
        public override void Cleanup() {
            this.gl.Dispose();
        }

        public override void HandleWindowSizeChange(int width, int height) {
            this.gl.Viewport(0, 0, (uint)width, (uint)height);

            this.ProjectionMatrix = Matrix4x4.CreateOrthographicOffCenter(0, width, height, 0, 0, 1);
        }
        
        public override void HandleFramebufferResize(int width, int height) {
            this.gl.Viewport(0, 0, (uint)width, (uint)height);

            this.ProjectionMatrix = Matrix4x4.CreateOrthographicOffCenter(0, width, height, 0, 0, 1);
        }
        
        public override IQuadRenderer CreateTextureRenderer() => new QuadRendererGL20(this);
        public override ILineRenderer CreateLineRenderer()    => new LineRendererGL20(this);

        private int                  _maxTexUnits = -1;
        private ExtFramebufferObject framebufferObjectEXT;
        public override int QueryMaxTextureUnits() {
            if (this._maxTexUnits == -1)
                this._maxTexUnits = this.gl.GetInteger((GLEnum)GetPName.MaxTextureImageUnits);

            return this._maxTexUnits;
        }
        public override void Clear() {
            this.gl.Clear(ClearBufferMask.ColorBufferBit);
            this.gl.ClearColor(0f, 0, 0, 0);
        }

        public override TextureRenderTarget CreateRenderTarget(uint width, uint height) => new TextureRenderTargetGL(this, width, height);

        public override Texture CreateTexture(byte[] imageData, bool qoi = false) => new TextureGL(this, imageData, qoi);

        public override Texture CreateTexture(Stream stream) => new TextureGL(this, stream);

        public override Texture CreateTexture(uint width, uint height) => new TextureGL(this, width, height);

        public override Texture CreateTexture(string filepath) => new TextureGL(this, filepath);

        public override Texture CreateWhitePixelTexture() => new TextureGL(this);

        [Pure]
        public GL GetOpenGL() => this.gl;
        [Pure]
        public ExtFramebufferObject GetOpenGLFramebufferEXT() => this.framebufferObjectEXT;
        
        public override void ImGuiUpdate(double deltaTime) {
            this._imgui.Update((float)deltaTime);
        }
        public override void ImGuiDraw(double deltaTime) {
            this._imgui.Render();
        }

        public new GLBackendType        GetType()     => GLBackendType.Legacy;
        public     Silk.NET.OpenGL.GL   GetModernGL() => throw new WrongGLBackendException();
        public     GL                   GetLegacyGL() => this.gl;
        public     Silk.NET.OpenGLES.GL GetGLES()     => throw new WrongGLBackendException();
        
        public     uint                 GenBuffer()   => this.gl.GenBuffer();
        public void BindBuffer(BufferTargetARB usage, uint buf) {
            this.gl.BindBuffer((Silk.NET.OpenGL.Legacy.BufferTargetARB)usage, buf);
        }
        public unsafe void BufferData(BufferTargetARB bufferType, nuint size, void* data, BufferUsageARB bufferUsage) {
            this.gl.BufferData((Silk.NET.OpenGL.Legacy.BufferTargetARB)bufferType, size, data, (Silk.NET.OpenGL.Legacy.BufferUsageARB)bufferUsage);
        }
        public unsafe void BufferSubData(BufferTargetARB bufferType, nint offset, nuint size, void* data) {
            this.gl.BufferSubData((Silk.NET.OpenGL.Legacy.BufferTargetARB)bufferType, offset, size, data);
        }
        public void DeleteBuffer(uint bufferId) {
            this.gl.DeleteBuffer(bufferId);
        }
        public void DeleteFramebuffer(uint frameBufferId) {
            this.framebufferObjectEXT.DeleteFramebuffer(frameBufferId);
        }
        public void DeleteTexture(uint textureId) {
            this.gl.DeleteTexture(textureId);
        }
        public void DeleteRenderbuffer(uint bufId) {
            this.framebufferObjectEXT.DeleteRenderbuffer(bufId);
        }
        public unsafe void DrawBuffers(uint i, in Silk.NET.OpenGL.GLEnum[] drawBuffers) {
            //this isnt pretty, but should work
            fixed (void* ptr = drawBuffers)
                this.gl.DrawBuffers(i, (GLEnum*)ptr);
        }
        public void BindFramebuffer(FramebufferTarget framebuffer, uint frameBufferId) {
            this.framebufferObjectEXT.BindFramebuffer((Silk.NET.OpenGL.Legacy.FramebufferTarget)framebuffer, frameBufferId);
        }
        public uint GenFramebuffer() => this.framebufferObjectEXT.GenFramebuffer();
        public void BindTexture(TextureTarget target, uint textureId) {
            this.gl.BindTexture((Silk.NET.OpenGL.Legacy.TextureTarget)target, textureId);
        }
        public unsafe void TexImage2D(TextureTarget target, int level, InternalFormat format, uint width, uint height, int border, PixelFormat pxFormat, PixelType type, void* data) {
            this.gl.TexImage2D((Silk.NET.OpenGL.Legacy.TextureTarget)target, level, (Silk.NET.OpenGL.Legacy.InternalFormat)format, width, height, border, (Silk.NET.OpenGL.Legacy.PixelFormat)pxFormat, (Silk.NET.OpenGL.Legacy.PixelType)type, data);
        }
        public void TexParameterI(TextureTarget target, Silk.NET.OpenGL.GLEnum param, int paramData) {
            this.gl.TexParameterI((Silk.NET.OpenGL.Legacy.TextureTarget)target, (GLEnum)param, paramData);
        }
        public uint GenRenderbuffer() => this.framebufferObjectEXT.GenRenderbuffer();
        public void Viewport(int x, int y, uint width, uint height) {
            this.gl.Viewport(x, y, width, height);
        }
        public uint GenTexture() => this.gl.GenTexture();
        public void BindRenderbuffer(RenderbufferTarget target, uint id) {
            this.framebufferObjectEXT.BindRenderbuffer((EXT)target, id);
        }
        public void RenderbufferStorage(RenderbufferTarget target, InternalFormat format, uint width, uint height) {
            this.framebufferObjectEXT.RenderbufferStorage((EXT)target, (EXT)format, width, height);
        }
        public void FramebufferRenderbuffer(FramebufferTarget target, FramebufferAttachment attachment, RenderbufferTarget rbTarget, uint id) {
            this.framebufferObjectEXT.FramebufferRenderbuffer((Silk.NET.OpenGL.Legacy.FramebufferTarget)target, (Silk.NET.OpenGL.Legacy.FramebufferAttachment)attachment, (Silk.NET.OpenGL.Legacy.RenderbufferTarget)rbTarget, id);
        }
        public void FramebufferTexture(FramebufferTarget target, FramebufferAttachment colorAttachment0, uint textureId, int level) {
            this.framebufferObjectEXT.FramebufferTexture2D((Silk.NET.OpenGL.Legacy.FramebufferTarget)target, (Silk.NET.OpenGL.Legacy.FramebufferAttachment)colorAttachment0, Silk.NET.OpenGL.Legacy.TextureTarget.Texture2D, textureId, level);
        }
        public Silk.NET.OpenGL.GLEnum CheckFramebufferStatus(FramebufferTarget target) => (Silk.NET.OpenGL.GLEnum)this.framebufferObjectEXT.CheckFramebufferStatus((EXT)target);
        public void GetInteger(Silk.NET.OpenGL.GetPName viewport, ref int[] oldViewPort) {
            this.gl.GetInteger((GLEnum)viewport, oldViewPort);
        }
        public void TexParameter(TextureTarget target, TextureParameterName paramName, int param) {
            this.gl.TexParameter((GLEnum)target, (GLEnum)paramName, param);
        }
        public unsafe void TexSubImage2D(TextureTarget target, int level, int x, int y, uint width, uint height, PixelFormat pxformat, PixelType pxtype, void* data) {
            this.gl.TexSubImage2D((GLEnum)target, level, x, y, width, height, (GLEnum)pxformat, (GLEnum)pxtype, data);
        }
        public void ActiveTexture(TextureUnit textureSlot) {
            this.gl.ActiveTexture((GLEnum)textureSlot);
        }
        public uint CreateProgram()               => this.gl.CreateProgram();
        public uint CreateShader(ShaderType type) => this.gl.CreateShader((GLEnum)type);
        public void ShaderSource(uint shaderId, string source) {
            this.gl.ShaderSource(shaderId, source);
        }
        public void CompileShader(uint shaderId) {
            this.gl.CompileShader(shaderId);
        }
        public string GetShaderInfoLog(uint shaderId) => this.gl.GetShaderInfoLog(shaderId);
        public void AttachShader(uint programId, uint shaderId) {
            this.gl.AttachShader(programId, shaderId);
        }
        public void LinkProgram(uint programId) {
            this.gl.LinkProgram(programId);
        }
        public void GetProgram(uint programId, ProgramPropertyARB linkStatus, out int i) {
            this.gl.GetProgram(programId, (GLEnum)linkStatus, out i);
        }
        public void DeleteShader(uint shader) {
            this.gl.DeleteShader(shader);
        }
        public string GetProgramInfoLog(uint programId) => this.gl.GetProgramInfoLog(programId);
        public void UseProgram(uint programId) {
            this.gl.UseProgram(programId);
        }
        public int GetUniformLocation(uint programId, string uniformName) => this.gl.GetUniformLocation(programId, uniformName);
        public unsafe void UniformMatrix4(int getUniformLocation, uint i, bool b, float* f) {
            this.gl.UniformMatrix4(getUniformLocation, i, b, f);
        }
        public void Uniform1(int getUniformLocation, float f) {
            this.gl.Uniform1(getUniformLocation, f);
        }
        public void Uniform1(int getUniformLocation, int f) {
            this.gl.Uniform1(getUniformLocation, f);
        }
        public void Uniform2(int getUniformLocation, float f, float f2) {
            this.gl.Uniform2(getUniformLocation, f, f2);
        }
        public void Uniform2(int getUniformLocation, int f, int f2) {
            this.gl.Uniform2(getUniformLocation, f, f2);
        }
        public void DeleteProgram(uint programId) {
            this.gl.DeleteProgram(programId);
        }
    }
}
