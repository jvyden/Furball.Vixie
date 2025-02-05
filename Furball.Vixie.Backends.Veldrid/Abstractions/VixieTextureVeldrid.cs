using System;
using System.IO;
using Furball.Vixie.Backends.Shared;
using Furball.Vixie.Helpers;
using Silk.NET.Maths;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Veldrid;
using Rectangle=System.Drawing.Rectangle;

namespace Furball.Vixie.Backends.Veldrid.Abstractions; 

internal sealed class VixieTextureVeldrid : VixieTexture {
    public Texture Texture = null!;

    private          TextureFilterType[] _filterTypes     = new TextureFilterType[VeldridBackend.MAX_TEXTURE_UNITS];
    private readonly ResourceSet?[]      _resourceSets    = new ResourceSet[VeldridBackend.MAX_TEXTURE_UNITS];
    public static    ResourceLayout[]    ResourceLayouts = new ResourceLayout[VeldridBackend.MAX_TEXTURE_UNITS];
        
    private readonly VeldridBackend _backend;

    private readonly bool _mipmap;

    public ResourceSet GetResourceSet(VeldridBackend backend, int i) {
        if (this._filterTypes[i] != this.FilterType)
            this._resourceSets[i] = null;
        
        return this._resourceSets[i] ?? (this._resourceSets[i] = backend.ResourceFactory.CreateResourceSet(new ResourceSetDescription(ResourceLayouts[i], this.Texture, this.FilterType == TextureFilterType.Smooth ? this._backend.GraphicsDevice.Aniso4xSampler : this._backend.GraphicsDevice.PointSampler)));
    }

    private void Load(Image<Rgba32> image, TextureParameters parameters) {
        this._backend.CheckThread();
        uint mipLevels = (uint)(parameters.RequestMipmaps ? this.MipMapCount(image.Width, image.Height) : 1);
        TextureDescription textureDescription = TextureDescription.Texture2D(
        (uint)image.Width,
        (uint)image.Height,
        mipLevels,
        1,
        PixelFormat.R8_G8_B8_A8_UNorm,
        TextureUsage.Sampled | TextureUsage.RenderTarget |
        (parameters.RequestMipmaps ? TextureUsage.GenerateMipmaps : 0)
        );

        this.Texture = this._backend.ResourceFactory.CreateTexture(textureDescription);

        image.ProcessPixelRows(accessor => {
            for (int i = 0; i < accessor.Height; i++)
                this._backend.GraphicsDevice.UpdateTexture(this.Texture, accessor.GetRowSpan(i), 0, (uint) i, 0, (uint) image.Width, 1, 1, 0, 0);
        });

        if (parameters.RequestMipmaps)
            this._backend.CommandList.GenerateMipmaps(this.Texture);
    }

    /// <summary>
    /// Creates a Texture from a byte array which contains Image Data
    /// </summary>
    /// <param name="backend"></param>
    /// <param name="imageData">Image Data</param>
    /// <param name="parameters"></param>
    public VixieTextureVeldrid(VeldridBackend backend, byte[] imageData, TextureParameters parameters) {
        this._backend = backend;
        this._backend.CheckThread();
        this._mipmap = parameters.RequestMipmaps;

        Image<Rgba32> image;

        bool qoi = imageData.Length > 3 && imageData[0] == 'q' && imageData[1] == 'o' && imageData[2] == 'i' &&
                   imageData[3]     == 'f';

        if(qoi) {
            (Rgba32[] pixels, QoiLoader.QoiHeader header) data = QoiLoader.Load(imageData);

            image = Image.LoadPixelData(data.pixels, (int)data.header.Width, (int)data.header.Height);
        } else {
            image = Image.Load<Rgba32>(imageData);
        }

        int width  = image.Width;
        int height = image.Height;

        this.Load(image, parameters);

        this.Size = new Vector2D<int>(width, height);

        this.FilterType = parameters.FilterType;
    }
    /// <summary>
    /// Creates a Texture with a single White Pixel
    /// </summary>
    public VixieTextureVeldrid(VeldridBackend backend) {
        this._backend = backend;
        this._backend.CheckThread();

        Image<Rgba32> px = new(1, 1, new Rgba32(255, 255, 255, 255));

        this.Load(px, default);
            
        this.Size = new Vector2D<int>(1, 1);
    }
    /// <summary>
    /// Creates a Empty texture given a width and height
    /// </summary>
    /// <param name="backend"></param>
    /// <param name="width">Desired Width</param>
    /// <param name="height">Desired Height</param>
    /// <param name="parameters"></param>
    public VixieTextureVeldrid(VeldridBackend backend, uint width, uint height, TextureParameters parameters) {
        this._backend = backend;
        this._backend.CheckThread();
        this._mipmap = parameters.RequestMipmaps;

        Image<Rgba32> px = new((int)width, (int)height, new Rgba32(0, 0, 0, 0));

        this.Load(px, parameters);

        this.Size = new Vector2D<int>((int)width, (int)height);

        this.FilterType = parameters.FilterType;
    }
    /// <summary>
    /// Creates a Texture from a Stream which Contains Image Data
    /// </summary>
    /// <param name="backend"></param>
    /// <param name="stream">Image Data Stream</param>
    /// <param name="parameters"></param>
    public VixieTextureVeldrid(VeldridBackend backend, Stream stream, TextureParameters parameters) {
        this._backend = backend;
        this._backend.CheckThread();
        this._mipmap = parameters.RequestMipmaps;

        Image<Rgba32> image = Image.Load<Rgba32>(stream);

        int width  = image.Width;
        int height = image.Height;

        this.Load(image, parameters);

        this.Size = new Vector2D<int>(width, height);

        this.FilterType = parameters.FilterType;
    }

    public override TextureFilterType FilterType {
        get;
        set;
    } = TextureFilterType.Smooth;

    public override bool Mipmaps => this._mipmap;
    
    public override VixieTexture SetData <pDataType>(ReadOnlySpan<pDataType> data) {
        this._backend.CheckThread();
        this._backend.GraphicsDevice.UpdateTexture(this.Texture, data, 0, 0, 0, this.Texture.Width, this.Texture.Height, 1, 0, 0);

        if (this._mipmap)
            this._backend.CommandList.GenerateMipmaps(this.Texture);

        return this;
    }
    public override VixieTexture SetData <pDataType>(ReadOnlySpan<pDataType> data, Rectangle rect) {
        this._backend.CheckThread();
        this._backend.GraphicsDevice.UpdateTexture(this.Texture, data, (uint)rect.X, (uint)rect.Y, 0, (uint)rect.Width, (uint)rect.Height, 1, 0, 0);

        if (this._mipmap)
            this._backend.CommandList.GenerateMipmaps(this.Texture);

        return this;
    }
    
    public override unsafe Rgba32[] GetData() {
        this._backend.CheckThread();
        
        TextureDescription textureDescription = TextureDescription.Texture2D(
            (uint)this.Width,
            (uint)this.Height,
            1,
            1,
            PixelFormat.R8_G8_B8_A8_UNorm,
            TextureUsage.Staging
        );

        Texture? stagingTexture = this._backend.ResourceFactory.CreateTexture(textureDescription);

        CommandList cmdList = this._backend.ResourceFactory.CreateCommandList();

        cmdList.Begin();
        cmdList.CopyTexture(this.Texture, stagingTexture);
        cmdList.End();
        
        this._backend.GraphicsDevice.SubmitCommands(cmdList);

        Rgba32[] data = new Rgba32[this.Width * this.Height];
        
        MappedResource mapped = this._backend.GraphicsDevice.Map(stagingTexture, MapMode.Read);

        ReadOnlySpan<Rgba32> rawData = new((void*)mapped.Data, (int)mapped.SizeInBytes);
        
        //Copy the data into a contiguous array
        for (int i = 0; i < this.Height; i++)
            rawData.Slice((int)(i * (mapped.RowPitch / sizeof(Rgba32))), this.Width).CopyTo(data.AsSpan(i * this.Width));
        
        this._backend.GraphicsDevice.Unmap(stagingTexture);
        
        cmdList.Dispose();
        stagingTexture.Dispose();

        return data;
    }

    ~VixieTextureVeldrid() {
        DisposeQueue.Enqueue(this);
    }
        
    private bool _isDisposed = false;

    public override void Dispose() {
        this._backend.CheckThread();
        
        if (this._isDisposed) return;
        
        this._isDisposed = true;

        foreach (ResourceSet? resourceSet in this._resourceSets)
            resourceSet?.Dispose();
        
        this.Texture.Dispose(); 
        GC.SuppressFinalize(this);
    }
}