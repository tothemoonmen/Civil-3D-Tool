
public class PdfEmbedding
{
    public int Id { get; set; }

    public int PageNumber { get; set; }

    public string Text { get; set; } = "";

    public float[] Vector { get; set; } = [];
}
