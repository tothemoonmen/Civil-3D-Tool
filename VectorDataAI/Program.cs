using VectorDataAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.AI;
using CommunityToolkit.VectorData.InMemory;
using Microsoft.Extensions.VectorData;using OpenAI;
using UglyToad.PdfPig;
using System.Text.Json;
using System.IO;
using System.Text.RegularExpressions;

List<CloudService> cloudServices =
[
    new() {
            Key = 0,
            Name = "Azure App Service",
            Description = "Host .NET, Java, Node.js, and Python web applications and APIs in a fully managed Azure service. You only need to deploy your code to Azure. Azure takes care of all the infrastructure management like high availability, load balancing, and autoscaling."
    },
    new() {
            Key = 1,
            Name = "Azure Service Bus",
            Description = "A fully managed enterprise message broker supporting both point to point and publish-subscribe integrations. It's ideal for building decoupled applications, queue-based load leveling, or facilitating communication between microservices."
    },
    new() {
            Key = 2,
            Name = "Azure Blob Storage",
            Description = "Azure Blob Storage allows your applications to store and retrieve files in the cloud. Azure Storage is highly scalable to store massive amounts of data and data is stored redundantly to ensure high availability."
    },
    new() {
            Key = 3,
            Name = "Microsoft Entra ID",
            Description = "Manage user identities and control access to your apps, data, and resources."
    },
    new() {
            Key = 4,
            Name = "Azure Key Vault",
            Description = "Store and access application secrets like connection strings and API keys in an encrypted vault with restricted access to make sure your secrets and your application aren't compromised."
    },
    new() {
            Key = 5,
            Name = "Azure AI Search",
            Description = "Information retrieval at scale for traditional and conversational search applications, with security and options for AI enrichment and vectorization."
    }
];



/// Load configuration values
IConfigurationRoot config =
    new ConfigurationBuilder()
        .AddUserSecrets<Program>()
        .Build();

// Retrieve settings
string model = config["ModelName"];
string key = config["OpenAIKey"];

// Create embedding generator
IEmbeddingGenerator<string, Embedding<float>> generator =
    new OpenAIClient(key)
        .GetEmbeddingClient(model: model)
        .AsIEmbeddingGenerator();

// PDF location
string pdfPath = @"C:\Users\AMYES\RCT\Civil-3D-Tool\Data\Regulatory Clauses\Durango Pipe Regulations.pdf";

// JSON storage location
string outputPath = Path.Combine(
    AppContext.BaseDirectory,
    "pdf_embeddings.json"
);

// Load existing embeddings or generate new ones
List<PdfEmbedding> records;

if (File.Exists(outputPath))
{
    Console.WriteLine("Loading existing embeddings...");

    string json = await File.ReadAllTextAsync(outputPath);

    records = JsonSerializer.Deserialize<List<PdfEmbedding>>(json)
        ?? new List<PdfEmbedding>();

    Console.WriteLine($"Loaded {records.Count} embeddings.");
}
else
{
    Console.WriteLine("No saved embeddings found. Processing PDF...");

    var pages = ExtractPdfText(pdfPath);

    Console.WriteLine($"Extracted {pages.Count} pages.");

    var chunks = SplitIntoChunks(pages);

    Console.WriteLine($"Generated {chunks.Count} chunks.");

    var texts = chunks
        .Select(c => c.Text)
        .Where(text => !string.IsNullOrWhiteSpace(text))
        .ToList();

    Console.WriteLine($"Non-empty chunks: {texts.Count}");

    if (texts.Count == 0)
        throw new Exception("No text was extracted from the PDF.");

    var embeddings = await generator.GenerateAsync(texts);

    Console.WriteLine($"Generated {embeddings.Count} embeddings.");
    Console.WriteLine($"Dimensions: {embeddings[0].Vector.Length}");

    records = new List<PdfEmbedding>();

    for (int i = 0; i < texts.Count; i++)
    {
        records.Add(new PdfEmbedding
        {
            Id = i,
            PageNumber = chunks
                .Where(c => !string.IsNullOrWhiteSpace(c.Text))
                .ElementAt(i).Page,
            Text = texts[i],
            Vector = embeddings[i].Vector.ToArray()
        });
    }

    string json = JsonSerializer.Serialize(
        records,
        new JsonSerializerOptions
        {
            WriteIndented = true
        }
    );

    await File.WriteAllTextAsync(outputPath, json);

    Console.WriteLine($"Embeddings saved to: {outputPath}");
}

// At this point, records contains your saved PDF embeddings.



Console.WriteLine("\n==================================");
Console.WriteLine(" DURANGO PIPE REGULATIONS SEARCH");
Console.WriteLine("==================================");

while (true)
{
    Console.Write("\nEnter your question (or type 'exit'): ");

    string question = Console.ReadLine() ?? "";

    if (question.Equals("exit", StringComparison.OrdinalIgnoreCase))
        break;

    if (string.IsNullOrWhiteSpace(question))
    {
        Console.WriteLine("Please enter a question.");
        continue;
    }

    // Generate an embedding for the question only
    var queryEmbedding = await generator.GenerateAsync(
        new[] { question }
    );

    float[] queryVector = queryEmbedding[0].Vector.ToArray();

    // Compare the question against saved PDF embeddings
    var results = records
        .Select(record => new
        {
            Record = record,
            Score = CosineSimilarity(
                queryVector,
                record.Vector
            )
        })
        .OrderByDescending(x => x.Score)
        .Take(5)
        .ToList();

    // Display results
    Console.WriteLine("\n===== SEARCH RESULTS =====");

    foreach (var result in results)
    {
        Console.WriteLine($"\nPage: {result.Record.PageNumber}");
        Console.WriteLine($"Similarity: {result.Score:F4}");
        Console.WriteLine($"Text:\n{result.Record.Text}");
        Console.WriteLine("----------------------------------");
    }
}

Console.WriteLine("Search application closed.");


// --------------------------------------------------
// PDF EXTRACTION
// --------------------------------------------------

static List<(int Page, string Text)> ExtractPdfText(string path)
{
    var pages = new List<(int Page, string Text)>();

    using (var document = PdfDocument.Open(path))
    {
        foreach (var page in document.GetPages())
        {
            string text = page.Text;

            Console.WriteLine(
                $"Page {page.Number}: {text.Length} characters");

            pages.Add((page.Number, text));
        }
    }

    return pages;
}

// --------------------------------------------------
// SENTENCE-BASED CHUNKING
// --------------------------------------------------

static List<(int Page, string Text)> SplitIntoChunks(
    List<(int Page, string Text)> pages,
    int maxChunkSize = 300)
{
    var chunks = new List<(int Page, string Text)>();

    foreach (var page in pages)
    {
        var sentences = Regex.Split(
            page.Text,
            @"(?<=[.!?])\s+");

        string currentChunk = "";

        foreach (var sentence in sentences)
        {
            string cleanSentence = sentence.Trim();

            if (string.IsNullOrWhiteSpace(cleanSentence))
                continue;

            if (currentChunk.Length + cleanSentence.Length > maxChunkSize)
            {
                if (!string.IsNullOrWhiteSpace(currentChunk))
                {
                    chunks.Add((page.Page, currentChunk.Trim()));
                    currentChunk = "";
                }
            }

            if (currentChunk.Length > 0)
                currentChunk += " ";

            currentChunk += cleanSentence;

            if (currentChunk.Length > maxChunkSize)
            {
                chunks.Add((page.Page, currentChunk.Trim()));
                currentChunk = "";
            }
        }

        if (!string.IsNullOrWhiteSpace(currentChunk))
        {
            chunks.Add((page.Page, currentChunk.Trim()));
        }
    }

    return chunks;
}



static double CosineSimilarity(float[] a, float[] b)
{
    if (a.Length != b.Length)
        throw new ArgumentException("Vector dimensions do not match.");

    double dotProduct = 0;
    double magnitudeA = 0;
    double magnitudeB = 0;

    for (int i = 0; i < a.Length; i++)
    {
        dotProduct += a[i] * b[i];
        magnitudeA += a[i] * a[i];
        magnitudeB += b[i] * b[i];
    }

    if (magnitudeA == 0 || magnitudeB == 0)
        return 0;

    return dotProduct /
        (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
}
