namespace iPath.Application.AI;

public record SemanticSearchResult(
    Guid CaseId,
    float SimilarityScore
);

public interface ISemanticSearchService
{
    Task SaveEmbeddingAsync(Guid caseId, string text, CancellationToken ct = default);
    Task<List<SemanticSearchResult>> SearchSimilarCasesAsync(string queryText, int maxResults = 5, CancellationToken ct = default);
}
