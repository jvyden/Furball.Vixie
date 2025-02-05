using System.Numerics;
using FontStashSharp.Interfaces;
using Furball.Vixie.Backends.Shared.Backends;
using Furball.Vixie.Backends.Shared.Renderers;

namespace Furball.Vixie.Backends.Shared.FontStashSharp; 

public class VixieFontStashRenderer : IFontStashRenderer2 {
    internal readonly Renderer Renderer;
    public VixieFontStashRenderer(GraphicsBackend backend, Renderer renderer) {
        this.Renderer      = renderer;
        this.TextureManager = new VixieTexture2dManager(backend);
    }
    
    public unsafe void DrawQuad(object                         texture,    ref VertexPositionColorTexture topLeft, ref VertexPositionColorTexture topRight,
                                ref VertexPositionColorTexture bottomLeft, ref VertexPositionColorTexture bottomRight) {
        MappedData map = this.Renderer.Reserve(4, 6);

        map.VertexPtr[0].Position = new Vector2(topLeft.Position.X, topLeft.Position.Y);
        map.VertexPtr[1].Position = new Vector2(topRight.Position.X, topRight.Position.Y);
        map.VertexPtr[2].Position = new Vector2(bottomLeft.Position.X, bottomLeft.Position.Y);
        map.VertexPtr[3].Position = new Vector2(bottomRight.Position.X, bottomRight.Position.Y);

        map.VertexPtr[0].Color = topLeft.Color;
        map.VertexPtr[1].Color = topRight.Color;
        map.VertexPtr[2].Color = bottomLeft.Color;
        map.VertexPtr[3].Color = bottomRight.Color;

        map.VertexPtr[0].TextureCoordinate = topLeft.TextureCoordinate;
        map.VertexPtr[1].TextureCoordinate = topRight.TextureCoordinate;
        map.VertexPtr[2].TextureCoordinate = bottomLeft.TextureCoordinate;
        map.VertexPtr[3].TextureCoordinate = bottomRight.TextureCoordinate;

        long texId = this.Renderer.GetTextureId((VixieTexture)texture);
        map.VertexPtr[0].TexId = texId;
        map.VertexPtr[1].TexId = texId;
        map.VertexPtr[2].TexId = texId;
        map.VertexPtr[3].TexId = texId;

        map.IndexPtr[0] = (ushort)(0 + map.IndexOffset);
        map.IndexPtr[1] = (ushort)(2 + map.IndexOffset);
        map.IndexPtr[2] = (ushort)(1 + map.IndexOffset);
        map.IndexPtr[3] = (ushort)(2 + map.IndexOffset);
        map.IndexPtr[4] = (ushort)(3 + map.IndexOffset);
        map.IndexPtr[5] = (ushort)(1 + map.IndexOffset);
    }
    public ITexture2DManager TextureManager {
        get;
    }
}