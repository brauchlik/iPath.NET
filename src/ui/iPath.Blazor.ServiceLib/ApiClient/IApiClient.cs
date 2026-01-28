using FluentResults;
using iPath.Application.Features;
using iPath.Application.Features.Documents;
using iPath.Application.Features.ServiceRequests;
using iPath.Application.Features.Notifications;
using iPath.Application.Features.Users;
using iPath.Application.Localization;
using iPath.Application.Querying;
using iPath.Domain.Config;
using iPath.Domain.Entities;
using Refit;
using iPath.Application.Features.CMS;

namespace iPath.Blazor.ServiceLib.ApiClient;

[Headers("accept: application/json")]
public interface IPathApi
{

    [Get("/api/v1/translations/{lang}")]
    Task<IApiResponse<TranslationData>> GetTranslations(string lang);

    [Get("/api/v1/session")]
    Task<IApiResponse<SessionUserDto?>> GetSession();

    [Get("/api/v1/config")]
    Task<IApiResponse<iPathClientConfig>> GetConfig();

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

    [Put("/api/v1/users/password")]
    Task<IApiResponse<Result>> UpdateUserPassword(UpdateUserPasswordCommand command);

    [Put("/api/v1/users/profile")]
    Task<IApiResponse<Guid>> UpdateProfile(UpdateUserProfileCommand command);

    [Post("/api/v1/users/create")]
    Task<IApiResponse<OwnerDto>> CreateUser(CreateUserCommand command);

    [Delete("/api/v1/users/{id}")]
    Task<IApiResponse> DeleteUser(Guid id);

    // communities
    [Put("/api/v1/users/assign/community")]
    Task<IApiResponse> AssignUserToCommunity(AssignUserToCommunityCommand command);

    [Put("/api/v1/users/communities")]
    Task<IApiResponse<UserDto>> UpdateCommunityMemberships(UpdateCommunityMembershipCommand command);


    // groups
    [Put("/api/v1/users/assign/group")]
    Task<IApiResponse<GroupMemberDto>> AssignUserToGroup(AssignUserToGroupCommand command);

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


    [Delete("/api/v1/groups/{id}")]
    Task<IApiResponse> DeleteGroup(Guid id);

    [Delete("/api/v1/groups/drafts/{id}")]
    Task<IApiResponse> DeleteGroupDrafts(Guid id);

    #endregion


    #region "-- Communities --"

    [Post("/api/v1/communities/list")]
    Task<IApiResponse<PagedResultList<CommunityListDto>>> GetCommunityList(GetCommunityListQuery query);

    [Get("/api/v1/communities/{id}")]
    Task<IApiResponse<CommunityDto>> GetCommunity(Guid id);

    [Get("/api/v1/communities/{id}/members")]
    Task<IApiResponse<PagedResultList<CommunityMemberDto>>> GetCommunityMembers(Guid id);


    [Post("/api/v1/communities/create")]
    Task<IApiResponse<CommunityListDto>> CreateCommunity(CreateCommunityCommand input);

    [Put("/api/v1/communities/update")]
    Task<IApiResponse<CommunityListDto>> UpdateCommunity(UpdateCommunityCommand input);

    [Delete("/api/v1/communities/{id}")]
    Task<IApiResponse<CommunityListDto>> DeleteCommunity(Guid id);

    #endregion


    #region "-- ServiceRequest --"
    [Get("/api/v1/requests/{id}")]
    Task<IApiResponse<ServiceRequestDto>> GetRequestById(Guid id);

    [Post("/api/v1/requests/list")]
    Task<IApiResponse<PagedResultList<ServiceRequestListDto>>> GetRequestList(GetServiceRequestsQuery query);

    [Post("/api/v1/requests/idlist")]
    Task<IApiResponse<IReadOnlyList<Guid>>> GetRequestIdList(GetServiceRequestIdListQuery query);

    [Post("/api/v1/requests/create")]
    Task<IApiResponse<ServiceRequestDto>> CreateRequest(CreateServiceRequestCommand query);

    [Delete("/api/v1/requests/{id}")]
    Task<IApiResponse<ServiceRequestDeletedEvent>> DeleteRequest(Guid id);

    [Put("/api/v1/requests/update")]
    Task<IApiResponse<bool>> UpdateRequest(UpdateServiceRequestCommand request);

    [Post("/api/v1/requests/visit/{id}")]
    Task<IApiResponse<bool>> UpdateRequestVisit(Guid id);

    [Post("/api/v1/requests/annotation")]
    Task<IApiResponse<AnnotationDto>> CreateAnnotation(CreateAnnotationCommand request);

    [Delete("/api/v1/requests/annotation/{id}")]
    Task<IApiResponse<Guid>> DeleteAnnotation(Guid id);
    #endregion


    #region "-- Request-Documents --"
    [Multipart]
    [Post("/api/v1/documents/upload/{requestId}")]
    Task<IApiResponse<DocumentDto>> UploadDocument([AliasAs("file")] StreamPart file, Guid requestId, [AliasAs("parentId")] Guid? parentId = null);

    [Delete("/api/v1/documents/{id}")]
    Task<IApiResponse<Guid>> DeleteDocument(Guid id);

    [Put("/api/v1/documents/{id}")]
    Task<IApiResponse<Guid>> UpdateDocument(Guid id);

    [Put("/api/v1/documents/order")]
    Task<IApiResponse<ChildNodeSortOrderUpdatedEvent>> UpdateDocumentsSortOrder(UpdateDocumentsSortOrderCommand request);
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
    Task<IApiResponse<QuestionnaireEntity>> GetQuestionnaireById(Guid id);

    [Get("/api/v1/questionnaires/{id}")]
    Task<IApiResponse<QuestionnaireEntity>> GetQuestionnaire(string id, int? Version = null);

    [Post("/api/v1/questionnaires/list")]
    Task<IApiResponse<PagedResultList<QuestionnaireListDto>>> GetQuestionnnaires(GetQuestionnaireListQuery query);

    [Post("/api/v1/questionnaires/create")]
    Task<IApiResponse<Guid>> CreateQuestionnaire(UpdateQuestionnaireCommand cmd);

    [Put("/api/v1/questionnaires/assign")]
    Task<IApiResponse> AssignQuestionnaire(AssignQuestionnaireCommand command);
    #endregion


    #region "-- CMS --"
    [Post("/api/v1/cms/list")]
    Task<IApiResponse<PagedResultList<WebContentDto>>> GetWebContent(GetWebContentsQuery query);

    [Post("/api/v1/cms/create")]
    Task<IApiResponse<WebContentDto>> CreateWebContent(CreateWebContentCommand cmd);

    [Put("/api/v1/cms/{id}")]
    Task<IApiResponse<WebContentDto>> UpdateWebContent(Guid id, UpdateWebContentCommand cmd);

    [Delete("/api/v1/cms/{id}")]
    Task<IApiResponse> DeleteWebContent(Guid id);
    #endregion  
}
