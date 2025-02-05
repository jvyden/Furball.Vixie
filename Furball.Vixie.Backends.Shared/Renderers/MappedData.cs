﻿namespace Furball.Vixie.Backends.Shared.Renderers;

public unsafe struct MappedData {
    public readonly Vertex* VertexPtr;
    public readonly ushort* IndexPtr;

    public readonly ushort VertexCount;
    public readonly uint   IndexCount;

    public readonly uint IndexOffset;
    
    public MappedData(Vertex* vertexPtr, ushort* indexPtr, ushort vertexCount, uint indexCount, uint indexOffset) {
        this.VertexPtr   = vertexPtr;
        this.IndexPtr    = indexPtr;
        this.VertexCount = vertexCount;
        this.IndexCount  = indexCount;
        this.IndexOffset = indexOffset;
    }
}