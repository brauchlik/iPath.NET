using System.Text.Json;
using iPath.Application.Features.Admin;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;

namespace iPath.EF.Core.FeatureHandlers.Admin;

public class GetAiLineageByCaseHandler(iPathDbContext db)
    : IRequestHandler<GetAiLineageByCaseQuery, Task<List<AiLineageDetailDto>>>
{
    public async Task<List<AiLineageDetailDto>> Handle(GetAiLineageByCaseQuery request, CancellationToken ct)
    {
        var records = await db.CaseIngestionLineages
            .AsNoTracking()
            .Where(l => l.CaseId == request.CaseId)
            .OrderByDescending(l => l.Timestamp)
            .Join(db.ServiceRequests,
                l => l.CaseId,
                c => c.Id,
                (l, c) => new { Lineage = l, Case = c })
            .ToListAsync(ct);

        var result = new List<AiLineageDetailDto>(records.Count);

        foreach (var item in records)
        {
            var dto = new AiLineageDetailDto
            {
                Id = item.Lineage.Id,
                CaseId = item.Lineage.CaseId,
                GroupId = item.Lineage.GroupId?.ToString(),
                CaseTitle = item.Case.Description?.Title ?? "Untitled Case",
                RawInputText = item.Lineage.RawInputText,
                AiSuggestedDataJson = item.Lineage.AiSuggestedDataJson,
                HumanAcceptedDataJson = item.Lineage.HumanAcceptedDataJson,
                ModelUsed = item.Lineage.ModelIdentifierUsed,
                WasOverridden = item.Lineage.WasOverridden,
                Timestamp = item.Lineage.Timestamp,
                Status = item.Lineage.Status,
                ErrorMessage = item.Lineage.ErrorMessage
            };

            TryParseExtractedFields(dto);
            result.Add(dto);
        }

        return result;
    }

    private static void TryParseExtractedFields(AiLineageDetailDto dto)
    {
        if (string.IsNullOrEmpty(dto.AiSuggestedDataJson) || dto.AiSuggestedDataJson == "{}")
            return;

        try
        {
            using var doc = JsonDocument.Parse(dto.AiSuggestedDataJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("Age", out var ageEl) && ageEl.ValueKind == JsonValueKind.Number)
                dto.Age = ageEl.GetRawText();
            else if (root.TryGetProperty("age", out ageEl) && ageEl.ValueKind == JsonValueKind.Number)
                dto.Age = ageEl.GetRawText();

            if (root.TryGetProperty("Sex", out var sexEl) && sexEl.ValueKind == JsonValueKind.String)
                dto.Sex = sexEl.GetString();
            else if (root.TryGetProperty("sex", out sexEl) && sexEl.ValueKind == JsonValueKind.String)
                dto.Sex = sexEl.GetString();

            if (root.TryGetProperty("TopographyCode", out var topoCode) && topoCode.ValueKind == JsonValueKind.String)
                dto.TopographyCode = topoCode.GetString();
            else if (root.TryGetProperty("topographyCode", out topoCode) && topoCode.ValueKind == JsonValueKind.String)
                dto.TopographyCode = topoCode.GetString();

            if (root.TryGetProperty("TopographyName", out var topoName) && topoName.ValueKind == JsonValueKind.String)
                dto.TopographyName = topoName.GetString();
            else if (root.TryGetProperty("topographyName", out topoName) && topoName.ValueKind == JsonValueKind.String)
                dto.TopographyName = topoName.GetString();
        }
        catch (JsonException)
        {
        }
    }
}
