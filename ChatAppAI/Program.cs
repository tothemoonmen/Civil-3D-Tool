
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration.UserSecrets;
using OpenAI;
using System.Text.Json;

// --------------------------------------------------
// 1. LOAD CONFIGURATION
// --------------------------------------------------

var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

string chatModel = config["ChatModelName"]
    ?? throw new Exception("ChatModelName is missing.");

string embeddingModel = config["EmbeddingModelName"]
    ?? throw new Exception("EmbeddingModelName is missing.");

string key = config["OpenAIKey"]
    ?? throw new Exception("OpenAIKey is missing.");

string embeddingsPath = config["EmbeddingsPath"]
    ?? throw new Exception("EmbeddingsPath is missing.");

// --------------------------------------------------
// 2. CREATE AI CLIENTS
// --------------------------------------------------

var openAIClient = new OpenAIClient(key);

// Chat client
IChatClient chatClient =
    openAIClient
        .GetChatClient(chatModel)
        .AsIChatClient();

// Embedding generator
IEmbeddingGenerator<string, Embedding<float>> generator =
    openAIClient
        .GetEmbeddingClient(model: embeddingModel)
        .AsIEmbeddingGenerator();

// --------------------------------------------------
// 3. LOAD SAVED VECTOR DATABASE
// --------------------------------------------------

Console.WriteLine("Loading regulatory embeddings...");

if (!File.Exists(embeddingsPath))
    throw new FileNotFoundException(
        "Embedding database not found.", embeddingsPath);

string json = await File.ReadAllTextAsync(embeddingsPath);

List<PdfEmbedding> records =
    JsonSerializer.Deserialize<List<PdfEmbedding>>(json)
    ?? new List<PdfEmbedding>();

Console.WriteLine($"Loaded {records.Count} regulatory chunks.");

// --------------------------------------------------
// 4. CHAT SYSTEM INSTRUCTIONS
// --------------------------------------------------

List<ChatMessage> chatHistory =
[
    new ChatMessage(ChatRole.System, """
        You are a civil engineering assistant specializing
        in checking Civil 3D files against regulatory clauses.

        You have access to retrieved regulatory information
        supplied with each user question.

        Use the retrieved regulatory clauses as your primary
        source when answering questions about regulations.

        Do not invent regulatory requirements or section numbers.

        If the retrieved information does not answer the question,
        explain that the relevant regulation was not found.

        When helping users, ask for:

        1. The location of the project.
        2. What specifically they want checked.
        3. The Civil 3D file, when file inspection is needed.

        Explain suggested corrections and cite the relevant
        regulatory section and page number when available.

        Ask whether there is anything else you can help with.
        """)
];

// --------------------------------------------------
// 5. CHAT LOOP WITH VECTOR SEARCH
// --------------------------------------------------

while (true)
{
    Console.Write("\nYour prompt: ");

    string? userPrompt = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(userPrompt))
        continue;

    if (userPrompt.Equals(
        "exit", StringComparison.OrdinalIgnoreCase))
        break;

    // ----------------------------------------------
    // A. GENERATE QUESTION EMBEDDING
    // ----------------------------------------------

    var queryEmbedding = await generator.GenerateAsync(
        new[] { userPrompt });

    float[] queryVector =
        queryEmbedding[0].Vector.ToArray();

    // ----------------------------------------------
    // B. SEARCH VECTOR DATABASE
    // ----------------------------------------------

    var results = records
        .Select(record => new
        {
            Record = record,
            Score = CosineSimilarity(
                queryVector,
                record.Vector)
        })
        .OrderByDescending(x => x.Score)
        .Take(5)
        .ToList();

    // ----------------------------------------------
    // C. BUILD REGULATORY CONTEXT
    // ----------------------------------------------

    string regulatoryContext = string.Join(
        "\n\n",
        results.Select(result =>
            $"""
            Page: {result.Record.PageNumber}
            Similarity: {result.Score:F4}

            Regulatory Text:
            {result.Record.Text}
            """)
    );

    // ----------------------------------------------
    // D. SEND QUESTION + RETRIEVED CONTEXT TO AI
    // ----------------------------------------------

    string enhancedPrompt = $"""
        Answer the following question using the
        retrieved regulatory information below.

        RETRIEVED REGULATORY INFORMATION:

        {regulatoryContext}

        USER QUESTION:

        {userPrompt}

        Reference the page numbers when applicable.
        If the retrieved information is insufficient,
        clearly state that.
        """;

    chatHistory.Add(
        new ChatMessage(ChatRole.User, enhancedPrompt));

    // ----------------------------------------------
    // E. GENERATE AI RESPONSE
    // ----------------------------------------------

    Console.WriteLine("\nAI Response:");

    string response = "";

    await foreach (ChatResponseUpdate item in
        chatClient.GetStreamingResponseAsync(chatHistory))
    {
        Console.Write(item.Text);
        response += item.Text;
    }

    chatHistory.Add(
        new ChatMessage(ChatRole.Assistant, response));

    Console.WriteLine();
}

// --------------------------------------------------
// 6. COSINE SIMILARITY
// --------------------------------------------------

static double CosineSimilarity(float[] a, float[] b)
{
    if (a.Length != b.Length)
        throw new ArgumentException(
            "Vector dimensions do not match.");

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
