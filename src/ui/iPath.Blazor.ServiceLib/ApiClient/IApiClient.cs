using FluentResults;
using iPath.Application.Contracts;
using iPath.Application.Features;
using iPath.Application.Features.Admin;
using iPath.Application.Features.Annotations;
using iPath.Application.Features.CaseRoom;
using iPath.Application.Features.CMS;
using iPath.Application.Features.Documents;
using iPath.Application.Features.EmailImport;
using iPath.Application.Features.Notifications;
using iPath.Application.Features.TaskAssignments;
using iPath.Application.Features.ServiceRequests;
using iPath.Application.Features.ServiceRequests.Commands;
using iPath.Application.Features.Users;
using iPath.Application.Features.SyncImport;
using iPath.Application.Localization;
using iPath.Application.Querying;
using iPath.Domain.Config;
using iPath.Domain.Entities;
using Refit;

namespace iPath.Blazor.ServiceLib.ApiClient;

[Headers("accept: application/json")]
public interface IPathApi
{

    [Get("/api/v1/translations/{lang}")]
    Task<IApiResponse<TranslationData>> GetTranslations(string lang);

    [Post("/api/v1/translations/{lang}/add-missing")]
    Task<IApiResponse<bool>> AddMissingKeys(string lang, [Body] List<string> keys);

    [Get("/api/v1/session")]
    Task<IApiResponse<SessionUserDto?>> GetSession();

    [Get("/api/v1/config")]
    Task<IApiResponse<iPathClientConfig>> GetConfig();

    [Post("/api/v1/test/notify")]
    Task SendTestNodeEvent(TestEvent e);

        
    #region "-- Users --"

    [Post("/api/v1/users/list")]
    Task<IApiResponse<PagedResultList<UserListDto>>> GetUserList(GetUserListQuery query);

    [Post("/api/v1/users/consultants")]
    Task<IApiResponse<PagedResultList<ConsultantDto>>> GetConsultants(GetConsultantsQuery query);

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


    // upload folder
    [Post("/api/v1/users/{id}/uploadfolder")]
    Task<IApiResponse> CreateUploadFolder(Guid id);

    [Delete("/api/v1/users/{id}/uploadfolder")]
    Task<IApiResponse> DeleteUploadFolder(Guid id);
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

    [Delete("/api/v1/groups/{id}/destroy")]
    Task<IApiResponse> DestroyGroup(Guid id);

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
    Task<IApiResponse<ServiceRequestDto>> GetRequestById(Guid id, bool InclDeleted = false);

    [Post("/api/v1/requests/list")]
    Task<IApiResponse<PagedResultList<ServiceRequestListDto>>> GetRequestList(GetServiceRequestListQuery query);

    [Post("/api/v1/requests/idlist")]
    Task<IApiResponse<IReadOnlyList<Guid>>> GetRequestIdList(GetServiceRequestIdListQuery query);

    [Post("/api/v1/requests/adjacent")]
    Task<IApiResponse<Guid?>> GetAdjacentRequestId(GetAdjacentServiceRequestIdQuery query);

    [Post("/api/v1/requests/create")]
    Task<IApiResponse<ServiceRequestDto>> CreateRequest(CreateServiceRequestCommand query);

    [Delete("/api/v1/requests/{id}")]
    Task<IApiResponse<ServiceRequestDeletedEvent>> DeleteRequest(Guid id);

    [Put("/api/v1/requests/update")]
    Task<IApiResponse<bool>> UpdateRequest(UpdateServiceRequestCommand request);

    [Post("/api/v1/requests/visit/{id}")]
    Task<IApiResponse<bool>> UpdateRequestVisit(Guid id);

    [Post("/api/v1/requests/sync")]
    Task<IApiResponse> SyncStorage(SyncServiceRequestToStorageCommand cmd);


    // Annotations
    [Post("/api/v1/requests/annotation")]
    Task<IApiResponse<AnnotationDto>> CreateAnnotation(CreateAnnotationCommand request);

    [Put("/api/v1/requests/annotation")]
    Task<IApiResponse<AnnotationDto>> UpdateAnnotation(UpdateAnnotationCommand request);

    [Delete("/api/v1/requests/annotation/{id}")]
    Task<IApiResponse<Guid>> DeleteAnnotation(Guid id);


    [Get("/api/v1/requests/updates")]
    Task<IApiResponse<ServiceRequestUpdatesDto>> GetServiceRequestUpdates();

    [Get("/api/v1/requests/new")]
    Task<IApiResponse<PagedResultList<ServiceRequestListDto>>> GetNewServiceRequests();

    [Get("/api/v1/requests/newannotations")]
    Task<IApiResponse<PagedResultList<ServiceRequestListDto>>> GetNewAnnotations();



    [Post("/api/v1/requests/{id}/uploadfolder")]
    Task<IApiResponse<Guid>> CreateServiceRequestUploadFolder(Guid id);

    [Delete("/api/v1/requests/{id}/uploadfolder")]
    Task<IApiResponse> DeleteServiceRequestUploadFolder(Guid id);



    [Get("/api/v1/requests/{uploadFolderId}/scandocuments")]
    Task<IApiResponse<ScanExternalDocumentResponse>> ScanExternalDocuments(Guid uploadFolderId);

    [Post("/api/v1/requests/{uploadFolderId}/importdocuments")]
    Task<IApiResponse<FolderImportResponse>> ImportExternalDocuments(Guid uploadFolderId, IReadOnlyList<string>? storageIds);
    #endregion


    #region "-- Documents --"
    [Multipart]
    [Post("/api/v1/documents/upload/{requestId}")]
    Task<IApiResponse<DocumentDto>> UploadDocument([AliasAs("file")] StreamPart file, Guid requestId, [AliasAs("parentId")] Guid? parentId = null);

    [Delete("/api/v1/documents/{id}")]
    Task<IApiResponse> DeleteDocument(Guid id);

    [Put("/api/v1/documents/{id}")]
    Task<IApiResponse<Guid>> UpdateDocument(Guid id);

    [Put("/api/v1/documents/order")]
    Task<IApiResponse<ChildNodeSortOrderUpdatedEvent>> UpdateDocumentsSortOrder(UpdateDocumentsSortOrderCommand request);

    [Post("/api/v1/documents/vsi/import")]
    Task<IApiResponse<WsiImportResponse>> WsiImport([Body] WsiImportCommand request);
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
    Task<IApiResponse<PagedResultList<NotificationDto>>> GetNotifications(int page, int pageSize, eNotificationTarget target, [Query] string[]? sort = null, CancellationToken ct = default);

    [Post("/api/v1/notifications/{id}/read")]
    Task<IApiResponse> MarkNotificationAsRead(Guid id, CancellationToken ct = default);

    [Post("/api/v1/notifications/read-all")]
    Task<IApiResponse> MarkAllNotificationsAsRead(CancellationToken ct = default);

    [Get("/api/v1/notifications/unread-count")]
    Task<IApiResponse<int>> GetUnreadNotificationCount(CancellationToken ct = default);

    [Delete("/api/v1/notifications/{id}")]
    Task<IApiResponse> DeleteNotification(Guid id, CancellationToken ct = default);

    [Delete("/api/v1/notifications/all")]
    Task<IApiResponse> DeleteAllNotifications(CancellationToken ct = default);
    #endregion


    #region "-- Admin --"
    [Get("/api/v1/admin/roles")]
    Task<IApiResponse<IEnumerable<RoleDto>>> GetRoles();

    [Post("/api/v1/admin/events")]
    Task<IApiResponse<PagedResultList<EventDto>>> GetEvents(GetEventsQuery query);

    [Get("/api/v1/admin/database")]
    Task<IApiResponse<DatabaseStatusDto>> GetDatabaseStatus();

    [Get("/api/v1/admin/database/tables")]
    Task<IApiResponse<List<TableRowCountDto>>> GetDatabaseTableCounts();

    [Get("/api/v1/admin/vsi/jobs")]
    Task<IApiResponse<List<WsiConversionJobDto>>> GetWsiConversionJobs();

    [Get("/api/v1/admin/purge/deleted-documents")]
    Task<IApiResponse<List<PurgeDocumentFileDto>>> GetDeletedDocumentsWithFiles();

    [Post("/api/v1/admin/purge/document/{documentId}")]
    Task<IApiResponse<bool>> PurgeDocumentFiles(Guid documentId);

    [Get("/api/v1/admin/purge/stale-cache")]
    Task<IApiResponse<List<StaleCacheFileDto>>> GetStaleCacheFiles([Query] int daysOld = 7);

    [Post("/api/v1/admin/purge/stale-cache/clean")]
    Task<IApiResponse<int>> CleanStaleCacheFiles([Query] int daysOld = 7);

    [Get("/api/v1/admin/ai/status")]
    Task<IApiResponse<AiStatusDto>> GetAiStatus([Query] bool checkConnection = false);

    [Get("/api/v1/admin/ai/lineage/{id}")]
    Task<IApiResponse<AiLineageDetailDto>> GetAiLineageDetail(Guid id);

    [Get("/api/v1/admin/ai/lineage/by-case/{caseId}")]
    Task<IApiResponse<List<AiLineageDetailDto>>> GetAiLineageByCase(Guid caseId);

    [Post("/api/v1/admin/ai/enqueue/{caseId}")]
    Task<IApiResponse<AiEnqueueResult>> EnqueueAiExtraction(Guid caseId);

    [Get("/api/v1/admin/ai/translations/status")]
    Task<IApiResponse<TranslationStatusDto>> GetTranslationStatus([Query] string locale);

    [Post("/api/v1/admin/ai/translations/translate")]
    Task<IApiResponse<TranslationResultDto>> TranslateKeysBatch(TranslateKeysBatchCommand command);

    [Post("/api/v1/admin/ai/translations/update-key")]
    Task<IApiResponse<bool>> UpdateTranslationKey(UpdateTranslationKeyCommand command);

    [Post("/api/v1/admin/database/migrate")]
    Task<IApiResponse<DatabaseStatusDto>> ApplyDatabaseMigrations();

    [Get("/api/v1/admin/documents/{id}/storage")]
    Task<IApiResponse<DocumentStorageInfoDto>> GetDocumentStorageInfo(Guid id);
    #endregion


    #region "-- ServiceRequest Events --"
    [Get("/api/v1/requests/{id}/events")]
    Task<IApiResponse<List<EventDto>>> GetServiceRequestEvents(Guid id);

    [Get("/api/v1/requests/{id}/notifications")]
    Task<IApiResponse<List<NotificationDto>>> GetServiceRequestNotifications(Guid id, [Query] Guid? eventId = null);
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


    #region "-- Email Import --"
    [Get("/api/v1/admin/email-import/mailboxes")]
    Task<IApiResponse<IReadOnlyList<ImportMailboxSummary>>> GetEmailImportMailboxes();

    [Get("/api/v1/admin/email-import/{mailboxName}/pending")]
    Task<IApiResponse<IReadOnlyList<ImportEmailPreview>>> GetPendingEmails(string mailboxName);

    [Get("/api/v1/admin/email-import/{mailboxName}/{messageId}/preview")]
    Task<IApiResponse<ImportEmailPreview?>> GetEmailPreview(string mailboxName, string messageId);

    [Post("/api/v1/admin/email-import/resolve")]
    Task<IApiResponse<EmailImportGroupResolverResult>> ResolveEmailImport([Body] ResolveEmailImportQuery query);

    [Post("/api/v1/admin/email-import/import")]
    Task<IApiResponse<ImportEmailResult>> ImportEmail([Body] ImportEmailCommand command);

    [Delete("/api/v1/admin/email-import/{mailboxName}/{messageId}")]
    Task<IApiResponse> DeleteEmail(string mailboxName, string messageId);

    [Post("/api/v1/admin/email-import/import-all")]
    Task<IApiResponse<IReadOnlyList<ImportEmailResult>>> ImportAllEmails();

    [Get("/api/v1/admin/email-import/logs")]
    Task<IApiResponse<List<EmailImportLog>>> GetEmailImportLogs(int page = 0, int pageSize = 50);
    #endregion


    #region "-- Task Assignments --"
    [Post("/api/v1/taskassignments/my")]
    Task<IApiResponse<PagedResultList<TaskAssignmentDto>>> GetMyTaskAssignments(GetUserTaskAssignmentsQuery query);

    [Get("/api/v1/taskassignments/group/{groupId}")]
    Task<IApiResponse<IReadOnlyList<TaskAssignmentDto>>> GetGroupTaskAssignments(Guid groupId, eTaskStatus? statusFilter = null);

    [Get("/api/v1/taskassignments/case/{serviceRequestId}")]
    Task<IApiResponse<IReadOnlyList<TaskAssignmentDto>>> GetCaseTaskAssignments(Guid serviceRequestId);

    [Get("/api/v1/taskassignments/{id}")]
    Task<IApiResponse<TaskAssignmentDto>> GetTaskAssignmentById(Guid id);

    [Post("/api/v1/taskassignments/propose")]
    Task<IApiResponse<TaskAssignmentDto>> ProposeTaskAssignment([Body] ProposeTaskAssignmentCommand command);

    [Post("/api/v1/taskassignments/{id}/accept")]
    Task<IApiResponse<TaskAssignmentDto>> AcceptTaskAssignment(Guid id);

    [Post("/api/v1/taskassignments/{id}/decline")]
    Task<IApiResponse<TaskAssignmentDto>> DeclineTaskAssignment(Guid id);

    [Post("/api/v1/taskassignments/{id}/complete")]
    Task<IApiResponse<TaskAssignmentDto>> CompleteTaskAssignment(Guid id);

    [Post("/api/v1/taskassignments/{id}/return")]
    Task<IApiResponse<TaskAssignmentDto>> ReturnTaskAssignment(Guid id);

    [Post("/api/v1/taskassignments/{id}/cancel")]
    Task<IApiResponse<TaskAssignmentDto>> CancelTaskAssignment(Guid id);

    [Post("/api/v1/taskassignments/followup")]
    Task<IApiResponse<TaskAssignmentDto>> CreateFollowUpTask([Body] CreateFollowUpTaskCommand command);
    #endregion


    #region "-- Sync Import --"

    [Get("/api/v1/admin/sync/groups")]
    Task<IApiResponse<List<OldGroupSummary>>> GetOldGroupSummaries();

    [Get("/api/v1/admin/sync/groups/{groupId}/status")]
    Task<IApiResponse<GroupImportStatus>> GetGroupImportStatus(int groupId);

    [Post("/api/v1/admin/sync/groups/{groupId}")]
    Task<IApiResponse<SyncStartResponse>> StartSync(int groupId);

    [Post("/api/v1/admin/sync/groups/{groupId}/reimport")]
    Task<IApiResponse<SyncStartResponse>> StartReimport(int groupId);

    [Post("/api/v1/admin/sync/groups/{groupId}/delete")]
    Task<IApiResponse<SyncStartResponse>> DeleteImport(int groupId);

    [Get("/api/v1/admin/sync/job")]
    Task<IApiResponse<SyncJobState>> GetSyncJobStatus();

    #endregion


    #region "-- CaseRoom --"
    [Post("/api/v1/caseroom/{requestId}/join")]
    Task<IApiResponse<CaseRoomSnapshot>> JoinCaseRoom(Guid requestId, [Body] SessionRequest body, [Query] string? token = null);

    [Post("/api/v1/caseroom/{requestId}/leave")]
    Task<IApiResponse> LeaveCaseRoom(Guid requestId, [Body] SessionRequest body);

    [Post("/api/v1/caseroom/{requestId}/sync")]
    Task<IApiResponse> SyncCaseRoom(Guid requestId, [Body] SyncPayload payload);

    [Get("/api/v1/caseroom/{requestId}")]
    Task<IApiResponse<CaseRoomStatus?>> GetCaseRoomStatus(Guid requestId);

    [Post("/api/v1/caseroom/{requestId}/share-token")]
    Task<IApiResponse<ShareTokenResponse>> CreateShareToken(Guid requestId);
    #endregion
}

public record ShareTokenResponse(string Token);
