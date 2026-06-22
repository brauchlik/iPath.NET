using iPath.Application.AI;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace iPath.Database.EFCore.AI;

public class SemanticSearchService : ISemanticSearchService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly iPathDbContext _dbContext;
    private readonly ILogger<SemanticSearchService> _logger;

    public SemanticSearchService(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        iPathDbContext dbContext,
        ILogger<SemanticSearchService> _logger)
    {
        _embeddingGenerator = embeddingGenerator;
        _dbContext = dbContext;
        this._logger = _logger;
    }

    public async Task SaveEmbeddingAsync(Guid caseId, string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        try
        {
            _logger.LogInformation("Generating embedding for CaseId: {CaseId}", caseId);
            var vectorMemory = await _embeddingGenerator.GenerateVectorAsync(text, cancellationToken: ct);
            var floatArray = vectorMemory.ToArray();
            var byteArray = ConvertFloatArrayToByteArray(floatArray);

            var modelName = "nomic-embed-text";

            var existing = await _dbContext.CaseEmbeddings
                .FirstOrDefaultAsync(e => e.CaseId == caseId, ct);

            if (existing != null)
            {
                existing.VectorData = byteArray;
                existing.ModelIdentifierUsed = modelName;
                existing.Timestamp = DateTime.UtcNow;
                _dbContext.CaseEmbeddings.Update(existing);
            }
            else
            {
                var newEmbedding = new CaseEmbedding
                {
                    CaseId = caseId,
                    VectorData = byteArray,
                    ModelIdentifierUsed = modelName
                };
                await _dbContext.CaseEmbeddings.AddAsync(newEmbedding, ct);
            }

            await _dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save embedding for CaseId: {CaseId}", caseId);
            throw;
        }
    }

    public async Task<List<SemanticSearchResult>> SearchSimilarCasesAsync(string queryText, int maxResults = 5, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(queryText)) return new List<SemanticSearchResult>();

        try
        {
            var queryVectorMemory = await _embeddingGenerator.GenerateVectorAsync(queryText, cancellationToken: ct);
            var queryVector = queryVectorMemory.ToArray();

            var allEmbeddings = await _dbContext.CaseEmbeddings
                .AsNoTracking()
                .ToListAsync(ct);

            var results = new List<SemanticSearchResult>();

            foreach (var item in allEmbeddings)
            {
                if (item.VectorData == null || item.VectorData.Length == 0) continue;

                try
                {
                    var itemVector = ConvertByteArrayToFloatArray(item.VectorData);
                    float score = CosineSimilarity(queryVector, itemVector);
                    results.Add(new SemanticSearchResult(item.CaseId, score));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error computing cosine similarity for CaseId {CaseId}", item.CaseId);
                }
            }

            return results
                .OrderByDescending(r => r.SimilarityScore)
                .Take(maxResults)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search similar cases for query");
            throw;
        }
    }

    private static byte[] ConvertFloatArrayToByteArray(float[] floatArray)
    {
        var byteArray = new byte[floatArray.Length * sizeof(float)];
        Buffer.BlockCopy(floatArray, 0, byteArray, 0, byteArray.Length);
        return byteArray;
    }

    private static float[] ConvertByteArrayToFloatArray(byte[] byteArray)
    {
        var floatArray = new float[byteArray.Length / sizeof(float)];
        Buffer.BlockCopy(byteArray, 0, floatArray, 0, byteArray.Length);
        return floatArray;
    }

    private static float CosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA.Length != vectorB.Length)
            return 0f;

        float dotProduct = 0f;
        float normA = 0f;
        float normB = 0f;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            normA += vectorA[i] * vectorA[i];
            normB += vectorB[i] * vectorB[i];
        }

        if (normA == 0f || normB == 0f)
            return 0f;

        return dotProduct / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }
}
