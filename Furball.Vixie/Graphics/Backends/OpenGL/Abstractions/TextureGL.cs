using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Furball.Vixie.Graphics.Backends.OpenGL;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Rectangle=System.Drawing.Rectangle;

namespace Furball.Vixie.Graphics.Backends.OpenGL.Abstractions {
    public class TextureGL : Texture, IDisposable {
        private readonly OpenGLBackend _backend;
        /// <summary>
        /// All the Currently Bound Textures
        /// </summary>
        internal static Dictionary<TextureUnit, uint> BoundTextures = new() {
            { TextureUnit.Texture0,  0 },
            { TextureUnit.Texture1,  0 },
            { TextureUnit.Texture2,  0 },
            { TextureUnit.Texture3,  0 },
            { TextureUnit.Texture4,  0 },
            { TextureUnit.Texture5,  0 },
            { TextureUnit.Texture6,  0 },
            { TextureUnit.Texture7,  0 },
            { TextureUnit.Texture8,  0 },
            { TextureUnit.Texture9,  0 },
            { TextureUnit.Texture10, 0 },
            { TextureUnit.Texture11, 0 },
            { TextureUnit.Texture12, 0 },
            { TextureUnit.Texture13, 0 },
            { TextureUnit.Texture14, 0 },
            { TextureUnit.Texture15, 0 },
            { TextureUnit.Texture16, 0 },
            { TextureUnit.Texture17, 0 },
            { TextureUnit.Texture18, 0 },
            { TextureUnit.Texture19, 0 },
            { TextureUnit.Texture20, 0 },
            { TextureUnit.Texture21, 0 },
            { TextureUnit.Texture22, 0 },
            { TextureUnit.Texture23, 0 },
            { TextureUnit.Texture24, 0 },
            { TextureUnit.Texture25, 0 },
            { TextureUnit.Texture26, 0 },
            { TextureUnit.Texture27, 0 },
            { TextureUnit.Texture28, 0 },
            { TextureUnit.Texture29, 0 },
            { TextureUnit.Texture30, 0 },
            { TextureUnit.Texture31, 0 },
        };

        public bool Bound {
            get {
                uint texFound;

                if (BoundTextures.TryGetValue(this.BoundAt, out texFound)) {
                    return texFound == this.TextureId;
                }

                return false;
            }
        }

        internal TextureUnit BoundAt;

        /// <summary>
        /// OpenGL API, used to not write Global.Gl everytime
        /// </summary>
        private GL            gl;
        /// <summary>
        /// Unique ID which identifies this Texture
        /// </summary>
        internal uint          TextureId;
        /// <summary>
        /// Local Image, possibly useful to Sample on the CPU Side if necessary
        /// </summary>
        private Image<Rgba32> _localBuffer;
        /// <summary>
        /// Size of the Texture
        /// </summary>
        public override Vector2 Size { get; protected set; }

        /// <summary>
        /// Creates a Texture from a File
        /// </summary>
        /// <param name="filepath">Path to an Image</param>
        public unsafe TextureGL(OpenGLBackend backend, string filepath) {
            this._backend = backend;
            this.gl       = backend.GetGlApi();

            Image<Rgba32> image = (Image<Rgba32>)Image.Load(filepath);

            this._localBuffer = image;

            int width = image.Width;
            int height = image.Height;

            this.Load(image);

            this.Size = new Vector2(width, height);
        }
        /// <summary>
        /// Creates a Texture from a byte array which contains Image Data
        /// </summary>
        /// <param name="imageData">Image Data</param>
        public unsafe TextureGL(OpenGLBackend backend, byte[] imageData, bool qoi = false) {
            this._backend = backend;
            this.gl       = backend.GetGlApi();

            Image<Rgba32> image;

            if(qoi) {
                (Rgba32[] pixels, QoiLoader.QoiHeader header) data  = QoiLoader.Load(imageData);

                image = Image.LoadPixelData(data.pixels, (int)data.header.Width, (int)data.header.Height);
            } else {
                image = Image.Load<Rgba32>(imageData);
            }

            this._localBuffer = image;

            int width = image.Width;
            int height = image.Height;

            this.Load(image);

            this.Size = new Vector2(width, height);
        }
        /// <summary>
        /// Creates a Texture with a single White Pixel
        /// </summary>
        public unsafe TextureGL(OpenGLBackend backend) {
            this._backend = backend;
            this._backend.CheckThread();
            
            this.gl = backend.GetGlApi();

            this.TextureId = this.gl.GenTexture();
            //Bind as we will be working on the Texture
            this.gl.BindTexture(TextureTarget.Texture2D, this.TextureId);
            //Apply Linear filtering, and make Image wrap around and repeat
            this.gl.TexParameter(GLEnum.Texture2D,        TextureParameterName.TextureMinFilter, (int) GLEnum.Linear);
            this.gl.TexParameter(GLEnum.Texture2D,        TextureParameterName.TextureMagFilter, (int) GLEnum.Linear);
            this.gl.TexParameter(TextureTarget.Texture2D, GLEnum.TextureWrapS,                   (int) GLEnum.Repeat);
            this.gl.TexParameter(TextureTarget.Texture2D, GLEnum.TextureWrapT,                   (int) GLEnum.Repeat);
            //White color
            uint color = 0xffffffff;
            //Upload Image Data
            this.gl.TexImage2D(GLEnum.Texture2D, 0, InternalFormat.Rgba8, 1, 1, 0, PixelFormat.Rgba, PixelType.UnsignedByte, &color);
            //Unbind as we have finished
            this.gl.BindTexture(TextureTarget.Texture2D, 0);
            this._backend.CheckError();

            this.Size = new Vector2(1, 1);
        }
        /// <summary>
        /// Creates a Empty texture given a width and height
        /// </summary>
        /// <param name="width">Desired Width</param>
        /// <param name="height">Desired Height</param>
        public unsafe TextureGL(OpenGLBackend backend, uint width, uint height) {
            this._backend = backend;
            this._backend.CheckThread();
            
            this.gl = backend.GetGlApi();

            this.TextureId = this.gl.GenTexture();
            //Bind as we will be working on the Texture
            this.gl.BindTexture(TextureTarget.Texture2D, this.TextureId);
            //Apply Linear filtering, and make Image wrap around and repeat
            this.gl.TexParameter(GLEnum.Texture2D,        TextureParameterName.TextureMinFilter, (int) GLEnum.Linear);
            this.gl.TexParameter(GLEnum.Texture2D,        TextureParameterName.TextureMagFilter, (int) GLEnum.Linear);
            this.gl.TexParameter(TextureTarget.Texture2D, GLEnum.TextureWrapS,                   (int) GLEnum.Repeat);
            this.gl.TexParameter(TextureTarget.Texture2D, GLEnum.TextureWrapT,                   (int) GLEnum.Repeat);
            //Upload Image Data
            this.gl.TexImage2D(GLEnum.Texture2D, 0, InternalFormat.Rgba8, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
            //Unbind as we have finished
            this.gl.BindTexture(TextureTarget.Texture2D, 0);
            this._backend.CheckError();

            this.Size = new Vector2(width, height);
        }
        /// <summary>
        /// Creates a Texture from a Stream which Contains Image Data
        /// </summary>
        /// <param name="stream">Image Data Stream</param>
        public unsafe TextureGL(OpenGLBackend backend, Stream stream) {
            this._backend = backend;
            this.gl       = backend.GetGlApi();

            Image<Rgba32> image = Image.Load<Rgba32>(stream);

            this._localBuffer = image;

            int width = image.Width;
            int height = image.Height;

            this.Load(image);

            this.Size = new Vector2(width, height);
        }

        ~TextureGL() {
            DisposeQueue.Enqueue(this);
        }

        private unsafe void Load(Image<Rgba32> image) {
            this.Load(null, image.Width, image.Height);
            this.Bind();
            image.ProcessPixelRows(accessor =>
            {
                for (int i = 0; i < accessor.Height; i++) {
                    fixed(void* ptr = &accessor.GetRowSpan(i).GetPinnableReference())
                        this.gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, i, (uint)accessor.Width, 1, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
                }
            });
            this.Unbind();
        }
        
        /// <summary>
        /// Creates a Texture class using a already Generated OpenGL Texture
        /// </summary>
        /// <param name="textureId">OpenGL Texture ID</param>
        /// <param name="width">Width of the Texture</param>
        /// <param name="height">Height of the Texture</param>
        internal TextureGL(OpenGLBackend backend, uint textureId, uint width, uint height) {
            this.gl = backend.GetGlApi();

            this._backend  = backend;
            this.TextureId = textureId;
            this.Size      = new Vector2(width, height);
        }

        /// <summary>
        /// Generates the Texture on the GPU, Sets Parameters and Uploads the Image Data
        /// </summary>
        /// <param name="data">Image Data</param>
        /// <param name="width">Width of Image</param>
        /// <param name="height">Height of Imgae</param>
        private unsafe void Load(void* data, int width, int height) {
            this.TextureId = this.gl.GenTexture();
            //Bind as we will be working on the Texture
            this.gl.BindTexture(TextureTarget.Texture2D, this.TextureId);
            //Apply Linear filtering, and make Image wrap around and repeat
            this.gl.TexParameter(GLEnum.Texture2D,        TextureParameterName.TextureMinFilter, (int) GLEnum.Linear);
            this.gl.TexParameter(GLEnum.Texture2D,        TextureParameterName.TextureMagFilter, (int) GLEnum.Linear);
            this.gl.TexParameter(TextureTarget.Texture2D, GLEnum.TextureWrapS,                   (int) GLEnum.Repeat);
            this.gl.TexParameter(TextureTarget.Texture2D, GLEnum.TextureWrapT,                   (int) GLEnum.Repeat);
            //Upload Image Data
            this.gl.TexImage2D(GLEnum.Texture2D, 0, InternalFormat.Rgba8, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, data);
            //Unbind as we have finished
            this.gl.BindTexture(TextureTarget.Texture2D, 0);
            this._backend.CheckError();
        }
        /// <summary>
        /// Sets the Data of the Texture Directly
        /// </summary>
        /// <param name="level">Level of the texture</param>
        /// <param name="data">Data to put there</param>
        /// <typeparam name="pDataType">Type of the Data</typeparam>
        /// <returns>Self, used for chaining methods</returns>
        public override unsafe TextureGL SetData<pDataType>(int level, pDataType[] data) {
            this.LockingBind();

            fixed(void* d = data)
                this.gl.TexImage2D(TextureTarget.Texture2D, level, InternalFormat.Rgba, (uint) this.Size.X, (uint) this.Size.Y, 0, PixelFormat.Rgba, PixelType.UnsignedByte, d);

            this.gl.Finish();
            this._backend.CheckError();

            this.UnlockingUnbind();

            return this;
        }
        /// <summary>
        /// Sets the Data of the Texture Directly
        /// </summary>
        /// <param name="level">Level of the Texture</param>
        /// <param name="rect">Rectangle of Data to Edit</param>
        /// <param name="data">Data to put there</param>
        /// <typeparam name="pDataType">Type of Data to put</typeparam>
        /// <returns>Self, used for chaining methods</returns>
        public override unsafe TextureGL SetData<pDataType>(int level, Rectangle rect, pDataType[] data) {
            this.LockingBind();

            fixed(void* d = data)
                this.gl.TexSubImage2D(TextureTarget.Texture2D, level, rect.X, rect.Y, (uint) rect.Width, (uint) rect.Height, PixelFormat.Rgba, PixelType.UnsignedByte, d);
            this._backend.CheckError();

            this.UnlockingUnbind();

            return this;
        }

        /// <summary>
        /// Binds the Texture to a certain Texture Slot
        /// </summary>
        /// <param name="textureSlot">Desired Texture Slot</param>
        /// <returns>Self, used for chaining methods</returns>
        public TextureGL Bind(TextureUnit textureSlot = TextureUnit.Texture0) {
            if (this.Locked)
                return null;

            this.gl.ActiveTexture(textureSlot);
            this.gl.BindTexture(TextureTarget.Texture2D, this.TextureId);
            this._backend.CheckError();

            BoundTextures[textureSlot] = this.TextureId;
            this.BoundAt               = textureSlot;

            return this;
        }

        /// <summary>
        /// Indicates whether Object is Locked or not,
        /// This is done internally to not be able to switch Textures while a Batch is happening
        /// or really anything that would possibly get screwed over by switching Textures
        /// </summary>
        internal bool Locked = false;

        /// <summary>
        /// Binds and sets a Lock so that the Texture cannot be unbound/rebound
        /// </summary>
        /// <returns>Self, used for chaining Methods</returns>
        internal TextureGL LockingBind() {
            this.Bind();
            this.Lock();

            return this;
        }
        /// <summary>
        /// Locks the Texture so that other Textures cannot be bound/unbound/rebound
        /// </summary>
        /// <returns>Self, used for chaining Methods</returns>
        internal TextureGL Lock() {
            this.Locked = true;

            return this;
        }
        /// <summary>
        /// Unlocks the Texture, so that other Textures can be bound
        /// </summary>
        /// <returns>Self, used for chaining Methods</returns>
        internal TextureGL Unlock() {
            this.Locked = false;

            return this;
        }
        /// <summary>
        /// Uninds and unlocks the Texture so that other Textures can be bound/rebound
        /// </summary>
        /// <returns>Self, used for chaining Methods</returns>
        internal TextureGL UnlockingUnbind() {
            this.Unlock();
            this.Unbind();

            return this;
        }

        /// <summary>
        /// Unbinds the Texture
        /// </summary>
        /// <returns>Self, used for chaining methods</returns>
        public TextureGL Unbind() {
            if (this.Locked)
                return null;

            this.gl.ActiveTexture(this.BoundAt);
            this.gl.BindTexture(TextureTarget.Texture2D, 0);
            this._backend.CheckError();

            BoundTextures[this.BoundAt] = 0;

            return this;
        }
        /// <summary>
        /// Gets the OpenGL texture ID
        /// </summary>
        /// <returns>Texture ID</returns>
        internal uint GetTextureId() => this.TextureId;

        private bool _isDisposed = false;

        /// <summary>
        /// Disposes the Texture and the Local Image Buffer
        /// </summary>
        public void Dispose() {
            if (this.Bound)
                this.UnlockingUnbind();

            if (this._isDisposed)
                return;

            this._isDisposed = true;

            try {
                this.gl.DeleteTexture(this.TextureId);
                this._localBuffer.Dispose();
            }
            catch {

            }
            this._backend.CheckError();
        }
    }
}