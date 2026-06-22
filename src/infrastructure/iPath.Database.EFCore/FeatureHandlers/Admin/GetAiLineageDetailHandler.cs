using System.Text.Json;
using iPath.Application.Features.Admin;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;

namespace iPath.EF.Core.FeatureHandlers.Admin;

public class GetAiLineageDetailHandler(iPathDbContext db)
    : IRequestHandler<GetAiLineageDetailQuery, Task<AiLineageDetailDto?>>
{
    public async Task<AiLineageDetailDto?> Handle(GetAiLineageDetailQuery request, CancellationToken ct)
    {
        var lineage = await db.CaseIngestionLineages
            .AsNoTracking()
            .Where(l => l.Id == request.Id)
            .Join(db.ServiceRequests,
                l => l.CaseId,
                c => c.Id,
                (l, c) => new { Lineage = l, Case = c })
            .FirstOrDefaultAsync(ct);

        if (lineage == null) return null;

        var dto = new AiLineageDetailDto
        {
            Id = lineage.Lineage.Id,
            CaseId = lineage.Lineage.CaseId,
            GroupId = lineage.Lineage.GroupId?.ToString(),
            CaseTitle = lineage.Case.Description?.Title ?? "Untitled Case",
            RawInputText = lineage.Lineage.RawInputText,
            AiSuggestedDataJson = lineage.Lineage.AiSuggestedDataJson,
            HumanAcceptedDataJson = lineage.Lineage.HumanAcceptedDataJson,
            ModelUsed = lineage.Lineage.ModelIdentifierUsed,
            WasOverridden = lineage.Lineage.WasOverridden,
            Timestamp = lineage.Lineage.Timestamp,
            Status = lineage.Lineage.Status,
            ErrorMessage = lineage.Lineage.ErrorMessage
        };

        // Try to extract Age, Sex, Topography from the JSON
        if (!string.IsNullOrEmpty(lineage.Lineage.AiSuggestedDataJson) && lineage.Lineage.AiSuggestedDataJson != "{}")
        {
            try
            {
                using var doc = JsonDocument.Parse(lineage.Lineage.AiSuggestedDataJson);
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
                // Non-fatal — just leave fields null
            }
        }

        return dto;
    }
}
