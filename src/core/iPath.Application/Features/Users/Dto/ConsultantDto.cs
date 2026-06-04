using iPath.Domain.Entities;

namespace iPath.Application.Features.Users;

public record ConsultantDto(Guid Id, string Username, string Email, string Initials,
    string? Specialisation, ConceptFilter? BodySiteFilter, string[] Roles)
{
    public string ToDisplay() => BodySiteFilter is null ? Username : $"{Username} [{BodySiteFilter.ConceptCodesString}]";
}

