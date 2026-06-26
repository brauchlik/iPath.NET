using System.Net;
using System.Net.Http.Headers;
using DispatchR;
using FluentResults;
using iPath.Application.Contracts;
using iPath.Application.Features;
using iPath.Application.Features.Admin;
using iPath.Application.AI;
using iPath.Application.Features.Annotations;
using iPath.Application.Features.CMS;
using iPath.Application.Features.Documents;
using iPath.Application.Features.EmailImport;
using iPath.Application.Features.Notifications;
using iPath.Application.Features.ServiceRequests;
using iPath.Application.Features.ServiceRequests.Commands;
using iPath.Application.Features.SyncImport;
using iPath.Application.Features.TaskAssignments;
using iPath.Application.Features.Users;
using iPath.Application.Features.Users.Commands;
using iPath.Application.Localization;
using iPath.Application.Querying;
using iPath.Blazor.ServiceLib.ApiClient;
using iPath.Domain.Config;
using iPath.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refit;

namespace iPath.Blazor.ServiceLib.Services;

public class DirectApiClient(
    IMediator mediator,
    IGroupService groupService,
    IEmailRepository emailRepo,
    INotificationRepository notificationRepo,
    IUserSession userSession,
    ILocalizationDataProvider localization,
    IOptions<iPathClientConfig> config,
    ILogger<DirectApiClient> logger,
    ISyncImportRunner? syncRunner = null,
    ISyncJobManager? jobManager = null,
    IAiExtractionQueue? queue = null)
    : IPathApi
{
    private static IApiResponse<T> Respond<T>(T? content) => new DirectApiResponse<T>(content);
    private static IApiResponse<T> RespondError<T>(Exception? ex = null) => new DirectApiResponse<T>(default, false, HttpStatusCode.InternalServerError, ex);

    private static IApiResponse RespondOk() => new DirectApiResponse();
    private static IApiResponse RespondError(Exception? ex = null) => new DirectApiResponse(false, HttpStatusCode.InternalServerError, ex);

    private static Task<IApiResponse<T>> NotSupported<T>() => Task.FromResult(RespondError<T>());
    private static Task<IApiResponse> NotSupportedVoid() => Task.FromResult(RespondError());


    // -- Session & Config --

    public async Task<IApiResponse<SessionUserDto?>> GetSession()
    {
        try
        {
            var user = userSession.User;
            return Respond<SessionUserDto?>(user);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GetSession via IUserSession failed");
        }
        return Respond<SessionUserDto?>(SessionUserDto.Anonymous);
    }

    public async Task<IApiResponse<TranslationData>> GetTranslations(string lang)
    {
        var result = await localization.GetTranslationDataAsync(lang);
        return Respond(result.ValueOrDefault);
    }

    public async Task<IApiResponse<bool>> AddMissingKeys(string lang, List<string> keys)
    {
        var data = await localization.GetTranslationDataAsync(lang);
        if (data.IsSuccess && data.Value != null)
        {
            bool updated = false;
            foreach (var key in keys)
            {
                if (data.Value.Words.TryAdd(key, ""))
                {
                    updated = true;
                }
            }
            if (updated)
            {
                await localization.SaveTranslationDataAsync(data.Value);
            }
            return Respond(true);
        }
        return Respond(false);
    }

    public async Task<IApiResponse<iPathClientConfig>> GetConfig()
    {
        return Respond(config.Value);
    }

    public async Task SendTestNodeEvent(TestEvent e)
    {
        await mediator.Publish(e, default);
    }


    // -- Users --

    public async Task<IApiResponse<PagedResultList<UserListDto>>> GetUserList(GetUserListQuery query)
    {
        return Respond(await mediator.Send(query, default));
    }

    public async Task<IApiResponse<PagedResultList<ConsultantDto>>> GetConsultants(GetConsultantsQuery query)
    {
        return Respond(await mediator.Send(query, default));
    }

    public async Task<IApiResponse<UserDto>> GetUser(Guid id)
    {
        return Respond(await mediator.Send(new GetUserByIdQuery(id), default));
    }

    public async Task<IApiResponse<Guid>> SetUserRole(UpdateUserRoleCommand command)
    {
        return Respond(await mediator.Send(command, default));
    }

    public async Task<IApiResponse> UpdateUserAccount(UpdateUserAccountCommand command)
    {
        await mediator.Send(command, default);
        return RespondOk();
    }

    public async Task<IApiResponse<Result>> UpdateUserPassword(UpdateUserPasswordCommand command)
    {
        var result = await mediator.Send(command, default);
        return Respond(result);
    }

    public async Task<IApiResponse<Guid>> UpdateProfile(UpdateUserProfileCommand command)
    {
        return Respond(await mediator.Send(command, default));
    }

    public async Task<IApiResponse<OwnerDto>> CreateUser(CreateUserCommand command)
    {
        return Respond(await mediator.Send(command, default));
    }

    public async Task<IApiResponse> DeleteUser(Guid id)
    {
        await mediator.Send(new DeleteUserCommand(id), default);
        return RespondOk();
    }

    public async Task<IApiResponse> AssignUserToCommunity(AssignUserToCommunityCommand command)
    {
        await mediator.Send(command, default);
        return RespondOk();
    }

    public async Task<IApiResponse<UserDto>> UpdateCommunityMemberships(UpdateCommunityMembershipCommand command)
    {
        return Respond(await mediator.Send(command, default));
    }

    public async Task<IApiResponse<GroupMemberDto>> AssignUserToGroup(AssignUserToGroupCommand command)
    {
        return Respond(await mediator.Send(command, default));
    }

    public async Task<IApiResponse<UserDto>> UpdateGroupMemberships(UpdateGroupMembershipCommand command)
    {
        return Respond(await mediator.Send(command, default));
    }

    public async Task<IApiResponse<IEnumerable<UserGroupNotificationDto>>> GetUserNotification(Guid id)
    {
        return Respond(await mediator.Send(new GetUserNotificationsQuery(id), default));
    }

    public async Task<IApiResponse<UserDto>> UpdateUserNotification(UpdateUserNotificationsCommand cmd)
    {
        return Respond(await mediator.Send(cmd, default));
    }

    public async Task<IApiResponse> CreateUploadFolder(Guid id)
    {
        await mediator.Send(new CreateRequestUploadFolderCommand(id), default);
        return RespondOk();
    }

    public async Task<IApiResponse> DeleteUploadFolder(Guid id)
    {
        await mediator.Send(new DeleteUserUploadFolderCommand(id), default);
        return RespondOk();
    }


    // -- Groups --

    public async Task<IApiResponse<PagedResultList<GroupListDto>>> GetGroupList(GetGroupListQuery query)
    {
        return Respond(await groupService.GetGroupListAsync(query));
    }

    public async Task<IApiResponse<GroupDto>> GetGroup(Guid id)
    {
        return Respond(await groupService.GetGroupByIdAsync(id));
    }

    public async Task<IApiResponse<PagedResultList<GroupMemberDto>>> GetGrouMembers(GetGroupMembersQuery query)
    {
        return Respond(await groupService.GetGroupMembersAsync(query));
    }

    public async Task<IApiResponse<GroupListDto>> CreateGroup(CreateGroupCommand command)
    {
        return Respond(await groupService.CreateGroupAsync(command));
    }

    public async Task<IApiResponse> UpdateGroup(UpdateGroupCommand command)
    {
        await groupService.UpdateGroupAsync(command);
        return RespondOk();
    }

    public async Task<IApiResponse> AssignGroupToCommunity(AssignGroupToCommunityCommand command)
    {
        await groupService.AssignGroupToCommunityAsync(command);
        return RespondOk();
    }

    public async Task<IApiResponse> DeleteGroup(Guid id)
    {
        await groupService.DeleteGroupAsync(new DeleteGroupCommand(id));
        return RespondOk();
    }

    public async Task<IApiResponse> DeleteGroupDrafts(Guid id)
    {
        await groupService.DeleteGroupDraftsAsync(id);
        return RespondOk();
    }


    // -- Communities --

    public async Task<IApiResponse<PagedResultList<CommunityListDto>>> GetCommunityList(GetCommunityListQuery query)
    {
        return Respond(await mediator.Send(query, default));
    }

    public async Task<IApiResponse<CommunityDto>> GetCommunity(Guid id)
    {
        return Respond(await mediator.Send(new GetCommunityByIdQuery(id), default));
    }

    public async Task<IApiResponse<PagedResultList<CommunityMemberDto>>> GetCommunityMembers(Guid id)
    {
        return Respond(await mediator.Send(new GetCommunityMembersQuery { CommunityId = id }, default));
    }

    public async Task<IApiResponse<CommunityListDto>> CreateCommunity(CreateCommunityCommand input)
    {
        return Respond(await mediator.Send(input, default));
    }

    public async Task<IApiResponse<CommunityListDto>> UpdateCommunity(UpdateCommunityCommand input)
    {
        return Respond(await mediator.Send(input, default));
    }

    public async Task<IApiResponse<CommunityListDto>> DeleteCommunity(Guid id)
    {
        await mediator.Send(new DeleteCommunityCommand(id), default);
        return Respond<CommunityListDto>(default);
    }


    // -- Service Requests --

    public async Task<IApiResponse<ServiceRequestDto>> GetRequestById(Guid id, bool InclDeleted = false)
    {
        return Respond(await mediator.Send(new GetServiceRequestByIdQuery(id, inclDeletedData: InclDeleted), default));
    }

    public async Task<IApiResponse<PagedResultList<ServiceRequestListDto>>> GetRequestList(GetServiceRequestListQuery query)
    {
        return Respond(await mediator.Send(query, default));
    }

    public async Task<IApiResponse<IReadOnlyList<Guid>>> GetRequestIdList(GetServiceRequestIdListQuery query)
    {
        return Respond(await mediator.Send(query, default));
    }

    public async Task<IApiResponse<ServiceRequestDto>> CreateRequest(CreateServiceRequestCommand query)
    {
        return Respond(await mediator.Send(query, default));
    }

    public async Task<IApiResponse<ServiceRequestDeletedEvent>> DeleteRequest(Guid id)
    {
        return Respond(await mediator.Send(new DeleteServiceRequestCommand(id), default));
    }

    public async Task<IApiResponse<bool>> UpdateRequest(UpdateServiceRequestCommand request)
    {
        return Respond(await mediator.Send(request, default));
    }

    public async Task<IApiResponse<bool>> UpdateRequestVisit(Guid id)
    {
        return Respond(await mediator.Send(new UpdateServiceRequestVisitCommand(id), default));
    }

    public async Task<IApiResponse> SyncStorage(SyncServiceRequestToStorageCommand cmd)
    {
        await mediator.Send(cmd, default);
        return RespondOk();
    }

    // Annotations
    public async Task<IApiResponse<AnnotationDto>> CreateAnnotation(CreateAnnotationCommand request)
    {
        return Respond(await mediator.Send(request, default));
    }

    public async Task<IApiResponse<AnnotationDto>> UpdateAnnotation(UpdateAnnotationCommand request)
    {
        return Respond(await mediator.Send(request, default));
    }

    public async Task<IApiResponse<Guid>> DeleteAnnotation(Guid id)
    {
        return Respond(await mediator.Send(new DeleteAnnotationCommand(id), default));
    }

    public async Task<IApiResponse<ServiceRequestUpdatesDto>> GetServiceRequestUpdates()
    {
        return Respond(await mediator.Send(new GetServiceRequestUpdatesQuery(), default));
    }

    public Task<IApiResponse<PagedResultList<ServiceRequestListDto>>> GetNewServiceRequests() => NotSupported<PagedResultList<ServiceRequestListDto>>();
    public Task<IApiResponse<PagedResultList<ServiceRequestListDto>>> GetNewAnnotations() => NotSupported<PagedResultList<ServiceRequestListDto>>();

    public async Task<IApiResponse<Guid>> CreateServiceRequestUploadFolder(Guid id)
    {
        return Respond(await mediator.Send(new CreateServiceRequestUploadFolderCommand(id), default));
    }

    public async Task<IApiResponse> DeleteServiceRequestUploadFolder(Guid id)
    {
        await mediator.Send(new DeleteServiceRequestUploadFolderCommand(id), default);
        return RespondOk();
    }

    public async Task<IApiResponse<ScanExternalDocumentResponse>> ScanExternalDocuments(Guid uploadFolderId)
    {
        return Respond(await mediator.Send(new ScanExternalDocumentsQuery(uploadFolderId), default));
    }

    public async Task<IApiResponse<FolderImportResponse>> ImportExternalDocuments(Guid uploadFolderId, IReadOnlyList<string>? storageIds)
    {
        return Respond(await mediator.Send(new ImportExternalDocumentsCommand(uploadFolderId, storageIds), default));
    }


    // -- Documents --

    public async Task<IApiResponse<DocumentDto>> UploadDocument(StreamPart file, Guid requestId, Guid? parentId = null)
    {
        var stream = file.Value;
        using var memStream = new MemoryStream();
        await stream.CopyToAsync(memStream);
        memStream.Position = 0;

        var cmd = new UploadDocumentCommand(
            RequestId: requestId,
            ParentId: parentId,
            filename: file.FileName,
            fileSize: memStream.Length,
            fileStream: memStream,
            contenttype: file.ContentType
        );
        return Respond(await mediator.Send(cmd, default));
    }

    public async Task<IApiResponse> DeleteDocument(Guid id)
    {
        await mediator.Send(new DeleteDocumentCommand(id), default);
        return RespondOk();
    }

    public async Task<IApiResponse<Guid>> UpdateDocument(Guid id)
    {
        await mediator.Send(new UpdateDocumenttCommand(DocumentId: id, Description: null, IsDraft: null), default);
        return Respond<Guid>(id);
    }

    public async Task<IApiResponse<ChildNodeSortOrderUpdatedEvent>> UpdateDocumentsSortOrder(UpdateDocumentsSortOrderCommand request)
    {
        return Respond(await mediator.Send(request, default));
    }

    public async Task<IApiResponse<VsiImportResponse>> VsiImport(VsiImportCommand request)
    {
        return Respond(await mediator.Send(request, default));
    }


    // -- Mailbox --

    public async Task<IApiResponse<PagedResultList<EmailMessage>>> GetMailBox(int page, int pageSize)
    {
        return Respond(await emailRepo.GetPage(new PagedQuery<EmailMessage> { Page = page, PageSize = pageSize }, CancellationToken.None));
    }

    public async Task<IApiResponse> DeleteMail(Guid id)
    {
        await emailRepo.Delete(id, CancellationToken.None);
        return RespondOk();
    }

    public async Task<IApiResponse> DeleteAllMail()
    {
        await emailRepo.DeleteAll(CancellationToken.None);
        return RespondOk();
    }

    public async Task<IApiResponse> SetMailAsRead(Guid id)
    {
        await emailRepo.SetReadState(id, true, CancellationToken.None);
        return RespondOk();
    }

    public async Task<IApiResponse> SetMailAsUnread(Guid id)
    {
        await emailRepo.SetReadState(id, false, CancellationToken.None);
        return RespondOk();
    }

    public async Task<IApiResponse<EmailMessage>> SendMail(EmailDto email)
    {
        return Respond(await emailRepo.Create(email.Address, email.Subject, email.Body, CancellationToken.None));
    }


    // -- Notifications --

    public async Task<IApiResponse<PagedResultList<NotificationDto>>> GetNotifications(int page, int pageSize, eNotificationTarget target, string[]? sort = null, CancellationToken ct = default)
    {
        var query = new GetNotificationsQuery
        {
            Page = page,
            PageSize = pageSize,
            Target = target,
            Sorting = sort,
            UserId = userSession.User?.Id
        };
        return Respond(await notificationRepo.GetPage(query, ct));
    }

    public async Task<IApiResponse> MarkNotificationAsRead(Guid id, CancellationToken ct = default)
    {
        await mediator.Send(new MarkNotificationAsReadCommand(id), ct);
        return RespondOk();
    }

    public async Task<IApiResponse> MarkAllNotificationsAsRead(CancellationToken ct = default)
    {
        if (userSession.User is not null)
        {
            await notificationRepo.MarkAllAsRead(userSession.User.Id, ct);
        }
        return RespondOk();
    }

    public async Task<IApiResponse<int>> GetUnreadNotificationCount(CancellationToken ct = default)
    {
        if (userSession.User is not null)
        {
            var count = await notificationRepo.GetUnreadCount(userSession.User.Id, eNotificationTarget.InApp, ct);
            return Respond(count);
        }
        return Respond(0);
    }

    public async Task<IApiResponse> DeleteNotification(Guid id, CancellationToken ct = default)
    {
        if (userSession.User is not null)
        {
            await notificationRepo.Delete(id, userSession.User.Id, ct);
        }
        return RespondOk();
    }

    public async Task<IApiResponse> DeleteAllNotifications(CancellationToken ct = default)
    {
        await notificationRepo.DeleteAll(ct);
        return RespondOk();
    }


    // -- Admin --

    public async Task<IApiResponse<IEnumerable<RoleDto>>> GetRoles()
    {
        return Respond(await mediator.Send(new GetRolesQuery(), default));
    }

    public async Task<IApiResponse<PagedResultList<EventDto>>> GetEvents(GetEventsQuery query)
    {
        return Respond(await mediator.Send(query, default));
    }

    public async Task<IApiResponse<DatabaseStatusDto>> GetDatabaseStatus()
    {
        return Respond(await mediator.Send(new GetDatabaseStatusQuery(), default));
    }

    public async Task<IApiResponse<List<TableRowCountDto>>> GetDatabaseTableCounts()
    {
        return Respond(await mediator.Send(new GetDatabaseTableCountsQuery(), default));
    }

    public async Task<IApiResponse<List<VsiConversionJobDto>>> GetVsiConversionJobs()
    {
        return Respond(await mediator.Send(new GetVsiConversionJobsQuery(), default));
    }

    public async Task<IApiResponse<List<PurgeDocumentFileDto>>> GetDeletedDocumentsWithFiles()
    {
        return Respond(await mediator.Send(new GetDeletedDocumentsWithFilesQuery(), default));
    }

    public async Task<IApiResponse<bool>> PurgeDocumentFiles(Guid documentId)
    {
        return Respond(await mediator.Send(new PurgeDocumentFilesCommand(documentId), default));
    }

    public async Task<IApiResponse<List<StaleCacheFileDto>>> GetStaleCacheFiles(int daysOld = 7)
    {
        return Respond(await mediator.Send(new GetStaleCacheFilesQuery(daysOld), default));
    }

    public async Task<IApiResponse<int>> CleanStaleCacheFiles(int daysOld = 7)
    {
        return Respond(await mediator.Send(new CleanStaleCacheFilesCommand(daysOld), default));
    }

    public async Task<IApiResponse<AiStatusDto>> GetAiStatus(bool checkConnection = false)
    {
        return Respond(await mediator.Send(new GetAiStatusQuery(checkConnection), default));
    }

    public async Task<IApiResponse<TranslationStatusDto>> GetTranslationStatus(string locale)
    {
        return Respond(await mediator.Send(new GetTranslationStatusQuery(locale), default));
    }

    public async Task<IApiResponse<TranslationResultDto>> TranslateKeysBatch(TranslateKeysBatchCommand command)
    {
        return Respond(await mediator.Send(command, default));
    }

    public async Task<IApiResponse<bool>> UpdateTranslationKey(UpdateTranslationKeyCommand command)
    {
        return Respond(await mediator.Send(command, default));
    }

    public async Task<IApiResponse<AiLineageDetailDto>> GetAiLineageDetail(Guid id)
    {
        return Respond(await mediator.Send(new GetAiLineageDetailQuery(id), default));
    }

    public async Task<IApiResponse<List<AiLineageDetailDto>>> GetAiLineageByCase(Guid caseId)
    {
        return Respond(await mediator.Send(new GetAiLineageByCaseQuery(caseId), default));
    }

    public async Task<IApiResponse<DatabaseStatusDto>> ApplyDatabaseMigrations()
    {
        return Respond(await mediator.Send(new ApplyDatabaseMigrationsCommand(), default));
    }

    public async Task<IApiResponse<AiEnqueueResult>> EnqueueAiExtraction(Guid caseId)
    {
        try
        {
            if (queue is null) return RespondError<AiEnqueueResult>();

            if (queue.IsInQueue(caseId))
            {
                return Respond(new AiEnqueueResult(Enqueued: false, Message: "Case is already in the AI transcription queue."));
            }

            await queue.EnqueueAsync(caseId);
            return Respond(new AiEnqueueResult(Enqueued: true, Message: "Case added to AI transcription queue."));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "EnqueueAiExtraction failed");
            return RespondError<AiEnqueueResult>(ex);
        }
    }

    // -- ServiceRequest Events --

    public async Task<IApiResponse<List<EventDto>>> GetServiceRequestEvents(Guid id)
    {
        return Respond(await mediator.Send(new GetServiceRequestEventsQuery(id), default));
    }

    public async Task<IApiResponse<List<NotificationDto>>> GetServiceRequestNotifications(Guid id, Guid? eventId = null)
    {
        return Respond(await mediator.Send(new GetServiceRequestNotificationsQuery(id), default));
    }


    // -- Questionnaires --

    public async Task<IApiResponse<QuestionnaireEntity>> GetQuestionnaireById(Guid id)
    {
        return Respond(await mediator.Send(new GetQuestionnaireByIdQuery(id), default));
    }

    public async Task<IApiResponse<QuestionnaireEntity>> GetQuestionnaire(string id, int? Version = null)
    {
        if (Guid.TryParse(id, out var guid))
        {
            return Respond(await mediator.Send(new GetQuestionnaireByIdQuery(guid), default));
        }
        return Respond(await mediator.Send(new GetQuestionnaireQuery(id, Version), default));
    }

    public async Task<IApiResponse<PagedResultList<QuestionnaireListDto>>> GetQuestionnnaires(GetQuestionnaireListQuery query)
    {
        return Respond(await mediator.Send(query, default));
    }

    public async Task<IApiResponse<Guid>> CreateQuestionnaire(UpdateQuestionnaireCommand cmd)
    {
        return Respond(await mediator.Send(cmd, default));
    }

    public async Task<IApiResponse> AssignQuestionnaire(AssignQuestionnaireCommand command)
    {
        await mediator.Send(command, default);
        return RespondOk();
    }


    // -- CMS --

    public async Task<IApiResponse<PagedResultList<WebContentDto>>> GetWebContent(GetWebContentsQuery query)
    {
        return Respond(await mediator.Send(query, default));
    }

    public async Task<IApiResponse<WebContentDto>> CreateWebContent(CreateWebContentCommand cmd)
    {
        return Respond(await mediator.Send(cmd, default));
    }

    public async Task<IApiResponse<WebContentDto>> UpdateWebContent(Guid id, UpdateWebContentCommand cmd)
    {
        return Respond(await mediator.Send(cmd, default));
    }

    public async Task<IApiResponse> DeleteWebContent(Guid id)
    {
        await mediator.Send(new DeleteWebContentCommand(id), default);
        return RespondOk();
    }


    // -- Email Import --

    public Task<IApiResponse<IReadOnlyList<ImportMailboxSummary>>> GetEmailImportMailboxes() => NotSupported<IReadOnlyList<ImportMailboxSummary>>();
    public Task<IApiResponse<IReadOnlyList<ImportEmailPreview>>> GetPendingEmails(string mailboxName) => NotSupported<IReadOnlyList<ImportEmailPreview>>();
    public Task<IApiResponse<ImportEmailPreview?>> GetEmailPreview(string mailboxName, string messageId) => NotSupported<ImportEmailPreview?>();
    public Task<IApiResponse<EmailImportGroupResolverResult>> ResolveEmailImport(ResolveEmailImportQuery query) => NotSupported<EmailImportGroupResolverResult>();
    public Task<IApiResponse<ImportEmailResult>> ImportEmail(ImportEmailCommand command) => NotSupported<ImportEmailResult>();
    public Task<IApiResponse> DeleteEmail(string mailboxName, string messageId) => NotSupportedVoid();
    public Task<IApiResponse<IReadOnlyList<ImportEmailResult>>> ImportAllEmails() => NotSupported<IReadOnlyList<ImportEmailResult>>();
    public Task<IApiResponse<List<EmailImportLog>>> GetEmailImportLogs(int page = 0, int pageSize = 50) => NotSupported<List<EmailImportLog>>();


    #region "-- Task Assignments --"

    public async Task<IApiResponse<PagedResultList<TaskAssignmentDto>>> GetMyTaskAssignments(GetUserTaskAssignmentsQuery query)
    {
        try
        {
            var result = await mediator.Send(query, default);
            return Respond(result);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GetMyTaskAssignments failed");
            return RespondError<PagedResultList<TaskAssignmentDto>>(ex);
        }
    }

    public async Task<IApiResponse<IReadOnlyList<TaskAssignmentDto>>> GetGroupTaskAssignments(Guid groupId, eTaskStatus? statusFilter = null)
    {
        try
        {
            var result = await mediator.Send(new GetGroupTaskAssignmentsQuery(groupId, statusFilter), default);
            return Respond<IReadOnlyList<TaskAssignmentDto>>(result);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GetGroupTaskAssignments failed");
            return RespondError<IReadOnlyList<TaskAssignmentDto>>(ex);
        }
    }

    public async Task<IApiResponse<IReadOnlyList<TaskAssignmentDto>>> GetCaseTaskAssignments(Guid serviceRequestId)
    {
        try
        {
            var result = await mediator.Send(new GetCaseTaskAssignmentsQuery(serviceRequestId), default);
            return Respond<IReadOnlyList<TaskAssignmentDto>>(result);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GetCaseTaskAssignments failed");
            return RespondError<IReadOnlyList<TaskAssignmentDto>>(ex);
        }
    }

    public async Task<IApiResponse<TaskAssignmentDto>> GetTaskAssignmentById(Guid id)
    {
        try
        {
            var result = await mediator.Send(new GetTaskAssignmentByIdQuery(id), default);
            return Respond<TaskAssignmentDto>(result);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GetTaskAssignmentById failed");
            return RespondError<TaskAssignmentDto>(ex);
        }
    }

    public async Task<IApiResponse<TaskAssignmentDto>> ProposeTaskAssignment(ProposeTaskAssignmentCommand command)
    {
        try
        {
            var result = await mediator.Send(command, default);
            return Respond<TaskAssignmentDto>(result);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ProposeTaskAssignment failed");
            return RespondError<TaskAssignmentDto>(ex);
        }
    }

    public async Task<IApiResponse<TaskAssignmentDto>> AcceptTaskAssignment(Guid id)
    {
        try
        {
            var result = await mediator.Send(new AcceptTaskAssignmentCommand(id), default);
            return Respond<TaskAssignmentDto>(result);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AcceptTaskAssignment failed");
            return RespondError<TaskAssignmentDto>(ex);
        }
    }

    public async Task<IApiResponse<TaskAssignmentDto>> DeclineTaskAssignment(Guid id)
    {
        try
        {
            var result = await mediator.Send(new DeclineTaskAssignmentCommand(id), default);
            return Respond<TaskAssignmentDto>(result);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DeclineTaskAssignment failed");
            return RespondError<TaskAssignmentDto>(ex);
        }
    }

    public async Task<IApiResponse<TaskAssignmentDto>> CompleteTaskAssignment(Guid id)
    {
        try
        {
            var result = await mediator.Send(new CompleteTaskAssignmentCommand(id), default);
            return Respond<TaskAssignmentDto>(result);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CompleteTaskAssignment failed");
            return RespondError<TaskAssignmentDto>(ex);
        }
    }

    public async Task<IApiResponse<TaskAssignmentDto>> ReturnTaskAssignment(Guid id)
    {
        try
        {
            var result = await mediator.Send(new ReturnTaskAssignmentCommand(id), default);
            return Respond<TaskAssignmentDto>(result);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ReturnTaskAssignment failed");
            return RespondError<TaskAssignmentDto>(ex);
        }
    }

    public async Task<IApiResponse<TaskAssignmentDto>> CancelTaskAssignment(Guid id)
    {
        try
        {
            var result = await mediator.Send(new CancelTaskAssignmentCommand(id), default);
            return Respond<TaskAssignmentDto>(result);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CancelTaskAssignment failed");
            return RespondError<TaskAssignmentDto>(ex);
        }
    }

    public async Task<IApiResponse<TaskAssignmentDto>> CreateFollowUpTask(CreateFollowUpTaskCommand command)
    {
        try
        {
            var result = await mediator.Send(command, default);
            return Respond<TaskAssignmentDto>(result);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CreateFollowUpTask failed");
            return RespondError<TaskAssignmentDto>(ex);
        }
    }

    #endregion


    // -- Sync Import --

    public async Task<IApiResponse<List<OldGroupSummary>>> GetOldGroupSummaries()
    {
        if (syncRunner is not null) return Respond(await syncRunner.GetOldGroupSummariesAsync());
        return await NotSupported<List<OldGroupSummary>>();
    }

    public async Task<IApiResponse<GroupImportStatus>> GetGroupImportStatus(int groupId)
    {
        if (syncRunner is not null) return Respond(await syncRunner.GetGroupImportStatusAsync(groupId));
        return await NotSupported<GroupImportStatus>();
    }

    public async Task<IApiResponse<SyncStartResponse>> StartSync(int groupId)
    {
        if (jobManager is null) return RespondError<SyncStartResponse>();
        var userId = userSession.User?.Id;
        var jobId = jobManager.StartSync(groupId, userId);
        return Respond(new SyncStartResponse(jobId.ToString()));
    }

    public async Task<IApiResponse<SyncStartResponse>> StartReimport(int groupId)
    {
        if (jobManager is null) return RespondError<SyncStartResponse>();
        var userId = userSession.User?.Id;
        var jobId = jobManager.StartReimport(groupId, userId);
        return Respond(new SyncStartResponse(jobId.ToString()));
    }

    public async Task<IApiResponse<SyncStartResponse>> DeleteImport(int groupId)
    {
        if (jobManager is null) return RespondError<SyncStartResponse>();
        var userId = userSession.User?.Id;
        var jobId = jobManager.StartDelete(groupId, userId);
        return Respond(new SyncStartResponse(jobId.ToString()));
    }

    public async Task<IApiResponse<SyncJobState>> GetSyncJobStatus()
        => Respond(jobManager?.Current);
}
