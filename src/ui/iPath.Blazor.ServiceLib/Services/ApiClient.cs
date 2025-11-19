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
    [Get("/api/v1/admin/mailbox")]
    Task<ApiResponse<PagedResultList<EmailMessage>>> GetMailBox(int page, int pageSize);

    [Get("/api/v1/translations/{lang}")]
    Task<ApiResponse<TranslationData>> GetTranslations(string lang);

        
    #region "-- Users --"

    [Post("/api/v1/users/list")]
    Task<ApiResponse<PagedResultList<UserListDto>>> GetUserList(GetUserListQuery query);

    [Get("/api/v1/users/{id}")]
    Task<ApiResponse<UserDto>> GetUser(Guid id);

    [Get("/api/v1/user/roles")]
    Task<ApiResponse<IEnumerable<RoleDto>>> GetRoles();


    [Put("/api/v1/user/role")]
    Task<ApiResponse<Guid>> SetUserRole(UpdateUserRoleCommand command);

    #endregion


    #region "-- Groups --"

    [Post("/api/v1/groups/list")]
    Task<ApiResponse<PagedResultList<GroupListDto>>> GetGroupList(GetGroupListQuery query);

    [Get("/api/v1/groups/{id}")]
    Task<ApiResponse<GroupDto>> GetGroup(Guid id);

    #endregion


    #region "-- Communities --"

    [Post("/api/v1/communities/list")]
    Task<ApiResponse<PagedResultList<CommunityListDto>>> GetCommunityList(GetCommunityListQuery query);

    [Get("/api/v1/communities/{id}")]
    Task<ApiResponse<CommunityDto>> GetCommunity(Guid id);


    [Post("/api/v1/communities/create")]
    Task<ApiResponse<CommunityListDto>> CreateCommunity(CreateCommunityInput input);

    [Put("/api/v1/communities/update")]
    Task<ApiResponse<CommunityListDto>> UpdateCommunity(UpdateCommunityInput input);

    [Delete("/api/v1/communities/{id}")]
    Task<ApiResponse<CommunityListDto>> DeleteCommunity(Guid id);

    #endregion


    #region "-- Nodes --"
    [Get("/api/v1/nodes/{id}")]
    Task<ApiResponse<NodeDto>> GetNodeById(Guid id);

    [Post("/api/v1/nodes/list")]
    Task<ApiResponse<PagedResultList<NodeListDto>>> GetNodeList(GetNodesQuery query);

    [Post("/api/v1/nodes/create")]
    Task<ApiResponse<NodeListDto>> CreateNode(CreateNodeCommand query);

    [Delete("/api/v1/nodes/{id}")]
    Task<ApiResponse<NodeDeletedEvent>> DeleteNode(Guid id);

    [Put("/api/v1/nodes/update")]
    Task<ApiResponse<bool>> UpdateNodeDescription(UpdateNodeDescriptionCommand request);

    [Put("/api/v1/nodes/order")]
    Task<ApiResponse<ChildNodeSortOrderUpdatedEvent>> UpdateNodeSortOrder(UpdateChildNodeSortOrderCommand request);

    [Post("/api/v1/nodes/visit/{id}")]
    Task<ApiResponse<bool>> UpdateNodeVisit(Guid id);

    [Multipart]
    [Post("/api/v1/nodes/upload")]
    Task<ApiResponse<bool>> UploadNodeFile(Guid rootNodeId, Guid? parentNodeId = null);


    [Post("/api/v1/nodes/annotation")]
    Task<ApiResponse<AnnotationDto>> CreateAnnotation(CreateNodeAnnotationCommand request);

    [Delete("/api/v1/nodes/annotation/{id}")]
    Task<ApiResponse<Guid>> DeleteAnnotation(Guid id);

    #endregion

}
