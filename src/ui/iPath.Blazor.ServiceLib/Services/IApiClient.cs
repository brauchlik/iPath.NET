using iPath.Application.Features;
using iPath.Application.Features.Nodes;
using iPath.Application.Features.Users;
using iPath.Application.Localization;
using iPath.Application.Querying;
using iPath.Domain.Entities;
using Refit;

namespace iPath.Blazor.ServiceLib.Services;

[Headers("accept: application/json")]
public interface IPathApi
{

    [Get("/api/v1/translations/{lang}")]
    Task<IApiResponse<TranslationData>> GetTranslations(string lang);

    [Get("/api/v1/session")]
    Task<IApiResponse<SessionUserDto>> GetSession();

    [Post("/api/v1/test/notify")]
    Task SendTestNodeEvent(TestEvent e);

        
    #region "-- Users --"

    [Post("/api/v1/users/list")]
    Task<IApiResponse<PagedResultList<UserListDto>>> GetUserList(GetUserListQuery query);

    [Get("/api/v1/users/{id}")]
    Task<IApiResponse<UserDto>> GetUser(Guid id);

    [Get("/api/v1/users/roles")]
    Task<IApiResponse<IEnumerable<RoleDto>>> GetRoles();


    [Put("/api/v1/users/role")]
    Task<IApiResponse<Guid>> SetUserRole(UpdateUserRoleCommand command);

    [Put("/api/v1/users/roles")]
    Task<IApiResponse> SetUserRoles(UpdateUserRolesCommand command);

    [Put("/api/v1/users/profile")]
    Task<IApiResponse<Guid>> UpdateProfile(UpdateUserProfileCommand command);

    #endregion


    #region "-- Groups --"

    [Post("/api/v1/groups/list")]
    Task<IApiResponse<PagedResultList<GroupListDto>>> GetGroupList(GetGroupListQuery query);

    [Get("/api/v1/groups/{id}")]
    Task<IApiResponse<GroupDto>> GetGroup(Guid id);

    #endregion


    #region "-- Communities --"

    [Post("/api/v1/communities/list")]
    Task<IApiResponse<PagedResultList<CommunityListDto>>> GetCommunityList(GetCommunityListQuery query);

    [Get("/api/v1/communities/{id}")]
    Task<IApiResponse<CommunityDto>> GetCommunity(Guid id);


    [Post("/api/v1/communities/create")]
    Task<IApiResponse<CommunityListDto>> CreateCommunity(CreateCommunityInput input);

    [Put("/api/v1/communities/update")]
    Task<IApiResponse<CommunityListDto>> UpdateCommunity(UpdateCommunityInput input);

    [Delete("/api/v1/communities/{id}")]
    Task<IApiResponse<CommunityListDto>> DeleteCommunity(Guid id);

    #endregion


    #region "-- Nodes --"
    [Get("/api/v1/nodes/{id}")]
    Task<IApiResponse<NodeDto>> GetNodeById(Guid id);

    [Post("/api/v1/nodes/list")]
    Task<IApiResponse<PagedResultList<NodeListDto>>> GetNodeList(GetNodesQuery query);

    [Post("/api/v1/nodes/idlist")]
    Task<IApiResponse<IReadOnlyList<Guid>>> GetNodeIdList(GetNodeIdListQuery query);

    [Post("/api/v1/nodes/create")]
    Task<IApiResponse<NodeDto>> CreateNode(CreateNodeCommand query);

    [Delete("/api/v1/nodes/{id}")]
    Task<IApiResponse<NodeDeletedEvent>> DeleteNode(Guid id);

    [Put("/api/v1/nodes/update")]
    Task<IApiResponse<bool>> UpdateNode(UpdateNodeCommand request);

    [Put("/api/v1/nodes/order")]
    Task<IApiResponse<ChildNodeSortOrderUpdatedEvent>> UpdateNodeSortOrder(UpdateChildNodeSortOrderCommand request);

    [Post("/api/v1/nodes/visit/{id}")]
    Task<IApiResponse<bool>> UpdateNodeVisit(Guid id);

    [Multipart]
    [Post("/api/v1/nodes/upload/{parentNodeId}")]
    Task<IApiResponse<NodeDto>> UploadNodeFile([AliasAs("file")] StreamPart file, Guid parentNodeId);


    [Post("/api/v1/nodes/annotation")]
    Task<IApiResponse<AnnotationDto>> CreateAnnotation(CreateNodeAnnotationCommand request);

    [Delete("/api/v1/nodes/annotation/{id}")]
    Task<IApiResponse<Guid>> DeleteAnnotation(Guid id);


    const long MaxFileSize = 1024L * 1024L * 1024L;

    #endregion


    #region "-- Mailbox --"
    [Get("/api/v1/mail/list")]
    Task<IApiResponse<PagedResultList<EmailMessage>>> GetMailBox(int page, int pageSize);

    [Delete("/api/v1/mail/{id}")]
    Task<IApiResponse> DeleteMail(Guid id);

    [Delete("/api/v1/mail/all")]
    Task<IApiResponse> DeleteAllMail();

    [Put("/api/v1/mail/read/{id}")]
    Task<IApiResponse> SetMailAsRead(Guid id);

    [Put("/api/v1/mail/unread/{id}")]
    Task<IApiResponse>SetMailAsUnread(Guid id);
    #endregion
}
