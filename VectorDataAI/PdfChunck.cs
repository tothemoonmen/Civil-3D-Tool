using Microsoft.Extensions.VectorData;

namespace VectorDataAI;

internal class PdfChunk
{
    [VectorStoreKey]
    public int Id { get; set; }

    [VectorStoreData]
    public string Text { get; set; }

    [VectorStoreData]
    public int PageNumber { get; set; }

    [VectorStoreVector(
        dimensions: 1536,
        DistanceFunction = DistanceFunction.CosineSimilarity)]
    public ReadOnlyMemory<float> Vector { get; set; }
}

