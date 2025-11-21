using DispatchR;
using DispatchR.Abstractions.Send;
using iPath.Application.Features;
using iPath.Application.Features.Nodes;
using iPath.Application.Features.Users;
using iPath.Application.Localization;
using iPath.Application.Querying;
using iPath.Domain.Entities;
using Refit;
using System.Net;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace iPath.Blazor.ServiceLib.Services;


public class MediatorApiResponse<T> : IApiResponse<T>
{
    private T _content;

    public MediatorApiResponse(T content)
    {
        _content = content;
    }

    private ApiException _exception;
    public MediatorApiResponse(Exception ex)
    {
        _exception = (ApiException) ex;
    }


    public ApiException? Error => throw new NotImplementedException();

    public HttpContentHeaders? ContentHeaders => throw new NotImplementedException();

    public bool IsSuccessStatusCode => throw new NotImplementedException();

    public bool IsSuccessful => throw new NotImplementedException();

    public T? Content => _content;

    public HttpResponseHeaders Headers => throw new NotImplementedException();

    public HttpStatusCode StatusCode => throw new NotImplementedException();

    public string? ReasonPhrase => throw new NotImplementedException();

    public HttpRequestMessage? RequestMessage => throw new NotImplementedException();

    public Version Version => throw new NotImplementedException();

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}

public class MediatorApiClient(IMediator mediator) : IPathApi
{
    private async Task<IApiResponse<TResponse>> Send<TRequest, TResponse>(IRequest<TRequest, Task<TResponse>> request, CancellationToken cancellationToken = default)
        where TRequest : class, IRequest
    {
        try
        {
            var ret = await mediator.Send(request, cancellationToken);
            return new MediatorApiResponse<TResponse>(ret);
        }
        catch (Exception ex)
        {
            return new MediatorApiResponse<TResponse>(ex);
        }
    }

    public Task<IApiResponse<AnnotationDto>> CreateAnnotation(CreateNodeAnnotationCommand request)
        => Send(request);

    public Task<IApiResponse<PagedResultList<EmailMessage>>> GetMailBox(int page, int pageSize)
    {
        throw new NotImplementedException();
    }

    public Task<IApiResponse<TranslationData>> GetTranslations(string lang)
    {
        throw new NotImplementedException();
    }

    public Task<IApiResponse<SessionUserDto>> GetSession()
    {
        throw new NotImplementedException();
    }

    public Task<IApiResponse<PagedResultList<UserListDto>>> GetUserList(GetUserListQuery query)
        => Send(query);

    public Task<IApiResponse<UserDto>> GetUser(Guid id)
    {
        throw new NotImplementedException();
    }

    public Task<IApiResponse<IEnumerable<RoleDto>>> GetRoles()
    {
        throw new NotImplementedException();
    }

    public Task<IApiResponse<Guid>> SetUserRole(UpdateUserRoleCommand command)
        => Send(command);

    public Task<IApiResponse<PagedResultList<GroupListDto>>> GetGroupList(GetGroupListQuery query)
        => Send(query);

    public Task<IApiResponse<GroupDto>> GetGroup(Guid id)
    {
        throw new NotImplementedException();
    }

    public Task<IApiResponse<PagedResultList<CommunityListDto>>> GetCommunityList(GetCommunityListQuery query)
        => Send(query);

    public Task<IApiResponse<CommunityDto>> GetCommunity(Guid id)
    {
        throw new NotImplementedException();
    }

    public Task<IApiResponse<CommunityListDto>> CreateCommunity(CreateCommunityInput input)
        => Send(input);

    public Task<IApiResponse<CommunityListDto>> UpdateCommunity(UpdateCommunityInput input)
        => Send(input);

    public Task<IApiResponse<CommunityListDto>> DeleteCommunity(Guid id)
    {
        throw new NotImplementedException();
    }

    public Task<IApiResponse<NodeDto>> GetNodeById(Guid id)
    {
        throw new NotImplementedException();
    }

    public Task<IApiResponse<PagedResultList<NodeListDto>>> GetNodeList(GetNodesQuery query)
        => Send(query);

    public Task<IApiResponse<IReadOnlyList<Guid>>> GetNodeIdList(GetNodeIdListQuery query)
    {
        throw new NotImplementedException();
    }

    public Task<IApiResponse<NodeListDto>> CreateNode(CreateNodeCommand query)
    {
        throw new NotImplementedException();
    }

    public Task<IApiResponse<NodeDeletedEvent>> DeleteNode(Guid id)
    {
        throw new NotImplementedException();
    }

    public Task<IApiResponse<bool>> UpdateNodeDescription(UpdateNodeDescriptionCommand request)
    {
        throw new NotImplementedException();
    }

    public Task<IApiResponse<ChildNodeSortOrderUpdatedEvent>> UpdateNodeSortOrder(UpdateChildNodeSortOrderCommand request)
    {
        throw new NotImplementedException();
    }

    public Task<IApiResponse<bool>> UpdateNodeVisit(Guid id)
    {
        throw new NotImplementedException();
    }

    public Task<IApiResponse<bool>> UploadNodeFile(Guid rootNodeId, Guid? parentNodeId = null)
    {
        throw new NotImplementedException();
    }

    public Task<IApiResponse<Guid>> DeleteAnnotation(Guid id)
    {
        throw new NotImplementedException();
    }
}
