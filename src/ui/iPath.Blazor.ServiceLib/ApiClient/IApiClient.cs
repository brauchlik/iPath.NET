using iPath.Application.Features;
using iPath.Application.Features.Nodes;
using iPath.Application.Features.Notifications;
using iPath.Application.Features.Users;
using iPath.Application.Localization;
using iPath.Application.Querying;
using iPath.Domain.Entities;
using Refit;

namespace iPath.Blazor.ServiceLib.ApiClient;

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


    // commands
    [Put("/api/v1/users/role")]
    Task<IApiResponse<Guid>> SetUserRole(UpdateUserRoleCommand command);

    [Put("/api/v1/users/account")]
    Task<IApiResponse> UpdateUserAccount(UpdateUserAccountCommand command);

    [Put("/api/v1/users/profile")]
    Task<IApiResponse<Guid>> UpdateProfile(UpdateUserProfileCommand command);

    [Post("/api/v1/users/create")]
    Task<IApiResponse<OwnerDto>> CreateUser(CreateUserCommand command);


    // communities
    [Put("/api/v1/users/assign/community")]
    Task<IApiResponse> AssignUserToCommunity(AssignUserToCommunityCommand command);

    [Put("/api/v1/users/communities")]
    Task<IApiResponse<UserDto>> UpdateCommunityMemberships(UpdateCommunityMembershipCommand command);


    // groups
    [Put("/api/v1/users/assign/group")]
    Task<IApiResponse> AssignUserToGroup(AssignUserToGroupCommand command);

    [Put("/api/v1/users/groups")]
    Task<IApiResponse<UserDto>> UpdateGroupMemberships(UpdateGroupMembershipCommand command);


    // notifications
    [Get("/api/v1/users/{id}/notifications")]
    Task<IApiResponse<IEnumerable<UserGroupNotificationDto>>> GetUserNotification(Guid id);

    [Post("/api/v1/users/notifications")]
    Task<IApiResponse<UserDto>> UpdateUserNotification(UpdateUserNotificationsCommand cmd);
    #endregion


    #region "-- Groups --"

    [Post("/api/v1/groups/list")]
    Task<IApiResponse<PagedResultList<GroupListDto>>> GetGroupList(GetGroupListQuery query);

    [Get("/api/v1/groups/{id}")]
    Task<IApiResponse<GroupDto>> GetGroup(Guid id);

    [Post("/api/v1/groups/members")]
    Task<IApiResponse<PagedResultList<GroupMemberDto>>> GetGrouMembers(GetGroupMembersQuery query);

    [Post("/api/v1/groups/create")]
    Task<IApiResponse<GroupListDto>> CreateGroup(CreateGroupCommand command);

    [Put("/api/v1/groups/update")]
    Task<IApiResponse> UpdateGroup(UpdateGroupCommand command);


    [Put("/api/v1/groups/community/assign")]
    Task<IApiResponse> AssignGroupToCommunity(AssignGroupToCommunityCommand command);

    [Put("/api/v1/groups/asignquestionnaire")]
    Task<IApiResponse> AssignQuestionnaireToGroup(AssignQuestionnaireToGroupCommand command);

    #endregion


    #region "-- Communities --"

    [Post("/api/v1/communities/list")]
    Task<IApiResponse<PagedResultList<CommunityListDto>>> GetCommunityList(GetCommunityListQuery query);

    [Get("/api/v1/communities/{id}")]
    Task<IApiResponse<CommunityDto>> GetCommunity(Guid id);

    [Get("/api/v1/communities/members")]
    Task<IApiResponse<PagedResultList<CommunityMemberDto>>> GetCommunityMembers(GetCommunityMembersQuery query);


    [Post("/api/v1/communities/create")]
    Task<IApiResponse<CommunityListDto>> CreateCommunity(CreateCommunityCommand input);

    [Put("/api/v1/communities/update")]
    Task<IApiResponse<CommunityListDto>> UpdateCommunity(UpdateCommunityCommand input);

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

    [Post("/api/v1/mail/send")]
    Task<IApiResponse<EmailMessage>> SendMail(EmailDto email);
    #endregion


    #region "-- Notifications --"
    [Get("/api/v1/notifications/list")]
    Task<IApiResponse<PagedResultList<NotificationDto>>> GetNotifications(int page, int pageSize, eNotificationTarget target);

    [Delete("/api/v1/notifications/all")]
    Task<IApiResponse> DeleteAllNotifications();
    #endregion


    #region "-- Admin --"
    [Get("/api/v1/admin/roles")]
    Task<IApiResponse<IEnumerable<RoleDto>>> GetRoles();
    #endregion


    #region "-- Questionnaires --"
    [Get("/api/v1/questionnaires/{id}")]
    Task<IApiResponse<Questionnaire>> GetQuestionnaireById(Guid id);

    [Get("/api/v1/questionnaires/{id}")]
    Task<IApiResponse<Questionnaire>> GetQuestionnaire(string id, int? Version = null);

    [Post("/api/v1/questionnaires/list")]
    Task<IApiResponse<PagedResultList<QuestionnaireListDto>>> GetQuestionnnaires(GetQuestionnaireListQuery query);

    [Post("/api/v1/questionnaires/create")]
    Task<IApiResponse<Guid>> CreateQuestionnaire(CreateQuestionnaireCommand cmd);

    #endregion
}
