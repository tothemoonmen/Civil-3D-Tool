using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.VectorData;
using CommunityToolkit.VectorData.InMemory;

IConfigurationRoot config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

string model = config["ModelName"];
string key = config["OpenAIKey"];

Console.WriteLine($"Model: {model}");
Console.WriteLine($"Key loaded: {!string.IsNullOrWhiteSpace(key)}");
Console.WriteLine($"Key length: {key?.Length ?? 0}");
    


// Configure an embedding generator to transform text to embeddings
IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator =
    new OpenAI.Embeddings.EmbeddingClient(
        model: model,
        apiKey: key)
    .AsIEmbeddingGenerator();

// Create a vector store and get a collection with the embedding generator
var vectorStore = new InMemoryVectorStore(new() { EmbeddingGenerator = embeddingGenerator });
var collection = vectorStore.GetCollection<int, Movie>("movies");
await collection.EnsureCollectionExistsAsync();

// Upsert some records
await collection.UpsertAsync(new Movie { Key = 1, Title = "The Lion King", Description = "An animated film about a young lion prince" });
await collection.UpsertAsync(new Movie { Key = 2, Title = "Inception", Description = "A thief who steals corporate secrets through dream-sharing technology" });
await collection.UpsertAsync(new Movie { Key = 3, Title = "Finding Nemo", Description = "A fish searches the ocean for his lost son" });

// Search for movies similar to the query text
await foreach (var result in collection.SearchAsync("animals in the wild", top: 2))
{
    Console.WriteLine($"{result.Record.Title} (score: {result.Score})");
}

// Define the data model
class Movie
{
    [VectorStoreKey]
    public int Key { get; set; }

    [VectorStoreData]
    public string Title { get; set; }

    [VectorStoreVector(dimensions: 1536)]
    public string Description { get; set; }
}