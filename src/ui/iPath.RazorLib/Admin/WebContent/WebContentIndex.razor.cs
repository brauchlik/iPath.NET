using iPath.Application.Features.CMS;

namespace iPath.Blazor.Componenents.Admin.WebContent;

public partial class WebContentIndex(IPathApi api, ISnackbar snackbar)
{
    MudDataGrid<WebContentDto> grid;


    public List<BreadcrumbItem> BreadCrumbs
    {
        get
        {
            var ret = new List<BreadcrumbItem> { new(T["Administration"], href: "admin") };
            ret.Add(new(T["Web Content"], href: null, disabled: true));
            return ret;
        }
    }

    async Task<GridData<WebContentDto>> GetDataAsync(GridState<WebContentDto> state, CancellationToken ct)
    {
        var query = state.BuildQuery(new GetWebContentsQuery { });
        var resp = await api.GetWebContent(query);

        if (resp.IsSuccessful) return resp.Content.ToGridData();

        snackbar.AddWarning(resp.ErrorMessage);
        return new GridData<WebContentDto>();
    }

    WebContentDto SelectedItem { 
        get => field;
        set
        {
            field = value;
            SelectedModel = value is null ? null : new WebContentModel { Id = value.Id, Title = value.Title, Body = value.Body };
        }
    }

    WebContentModel SelectedModel { get; set; }

    async Task AddContent()
    {
        SelectedModel = new WebContentModel { Type = eWebContentType.news };
        StateHasChanged();
    }

    async Task Save()
    {
        if (SelectedModel is not null)
        {
            if (SelectedModel.Id.HasValue)
            {
                var cmd = new UpdateWebContentCommand(Id: SelectedModel.Id.Value, Title: SelectedModel.Title, Body: SelectedModel.Body);
                var resp = await api.UpdateWebContent(SelectedModel.Id.Value, cmd);
                snackbar.CheckSuccess(resp);
            }
            else
            {
                var cmd = new CreateWebContentCommand(Title: SelectedModel.Title, Body: SelectedModel.Body, Type: SelectedModel.Type);
                var resp = await api.CreateWebContent(cmd);
                snackbar.CheckSuccess(resp, "Content created");
            }
            await grid.ReloadServerData();
        }
    }
}

public class WebContentModel
{
    public Guid? Id { get; init; } = null;
    public string Title { get; set; }
    public string Body { get; set; }
    public eWebContentType Type { get; set; }
}