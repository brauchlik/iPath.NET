namespace iPath.API.Services.Search;

public class VectorStoreService
{
    private readonly Dictionary<string, (string Content, ReadOnlyMemory<float> Vector)> _db = new();

    private readonly OllamaService _embedding;

    public VectorStoreService(OllamaService embedding)
    {
        _embedding = embedding;
    }

    public async Task Add(string id, string content)
    {
        var vector = await _embedding.GenerateEmbedding(content);

        _db[id] = (content, vector);
    }
}
