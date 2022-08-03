using System;
using System.Drawing;
using System.Numerics;

namespace Furball.Vixie.Backends.Shared; 

public abstract class Texture : IDisposable {
    protected Vector2 _size;

    public Vector2 Size => this._size;
    public          int     Width  => (int)this.Size.X;
    public          int     Height => (int)this.Size.Y;

    public abstract TextureFilterType FilterType { get; set; }
    
    public abstract Texture SetData<pDataType>(int level, pDataType[] data) where pDataType : unmanaged;
    public abstract Texture SetData<pDataType>(int level, Rectangle rect, pDataType[] data) where pDataType : unmanaged;
    public virtual  void    Dispose() {}
}