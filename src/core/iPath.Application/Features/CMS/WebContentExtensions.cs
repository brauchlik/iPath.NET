namespace iPath.Application.Features.CMS;

public static class WebContentExtensions
{
    extension (WebContent entity)
    {
        public WebContentDto ToDto()
        {
            return new WebContentDto(Id: entity.Id, Title: entity.Title, Body: entity.Body, Type: entity.Type, CreatedOn: entity.CreatedOn, Owner: entity.Owner.ToOwnerDto());
        }
    }
}