using iPath.Blazor.Componenents.Admin.Groups;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

namespace iPath.Blazor.Componenents.Admin.Communities;

public class CommunityAdminViewModel(IPathApi api, 
    ISnackbar snackbar, 
    IDialogService dialog,
    IStringLocalizer T,
    ILogger<CommunityAdminViewModel> logger)
    : IViewModel
{
    public MudDataGrid<CommunityListDto> grid;

    public async Task<GridData<CommunityListDto>> GetListAsync(GridState<CommunityListDto> state)
    {
        var query = state.BuildQuery(new GetCommunityListQuery());
        var resp = await api.GetCommunityList(query);

        if (resp.IsSuccessful) return resp.Content.ToGridData();

        snackbar.AddWarning(resp.ErrorMessage);
        return new GridData<CommunityListDto>();
    }

    public async Task<IEnumerable<CommunityListDto>> GetAllAsync()
    {
        var query = new GetCommunityListQuery { PageSize= null };
        var resp = await api.GetCommunityList(query);
        if (resp.IsSuccessful)
        {
            return resp.Content.Items;
        }
        return [];
    }


    public CommunityListDto SelectedItem { get; private set; }
    public async Task SetSelectedItem(CommunityListDto item)
    {
        if (SelectedItem == item) return;
        SelectedItem = item;
        await LoadCommunity(item?.Id);
    }

    public string SelectedRowStyle(CommunityListDto item, int rowIndex)
    {
        if (item is not null && SelectedItem is not null && item.Id == SelectedItem.Id )
            return "background-color: var(--mud-palette-background-gray)";

        return "";
    }



    public CommunityDto SelectedCommunity { get; private set; }
    private async Task LoadCommunity(Guid? id)
    {
        if (!id.HasValue)
        {
            SelectedCommunity = null;
        }
        else
        {
            var resp = await api.GetCommunity(id.Value);
            if (resp.IsSuccessful)
            {
                SelectedCommunity = resp.Content;
            }
            snackbar.AddError(resp.ErrorMessage);
        }
    }



    public async Task Create()
    {
        var p = new DialogParameters<CreateCommunityDialog> { { x => x.Model, new CommunityEditModel() } };
        DialogOptions opts = new() { MaxWidth = MaxWidth.Medium, FullWidth = false, NoHeader = false };
        var dlg = await dialog.ShowAsync<CreateCommunityDialog>(T["Create a new community"], options: opts, parameters: p);
        var res = await dlg.Result;
        var m = res?.Data as CommunityEditModel;
        if (m != null)
        {
            var cmd = new CreateCommunityCommand(Name: m.Name, OwnerId: m.Owner.Id, Description: m.Description, BaseUrl: m.BaseUrl);
            var resp = await api.CreateCommunity(cmd);
            if (!resp.IsSuccessful)
            {
                snackbar.AddWarning(resp.ErrorMessage);
            }
            await grid.ReloadServerData();
        }
    }

    public async Task Edit()
    {
        if (SelectedCommunity != null)
        {
            var m = new CommunityEditModel
            {
                Id = SelectedCommunity.Id,
                Name = SelectedCommunity.Name,
                Description = SelectedCommunity.Description,
                BaseUrl = SelectedCommunity.BaseUrl
            };

            var p = new DialogParameters<EditCommunityDialog> { { x => x.Model, m } };
            DialogOptions opts = new() { MaxWidth = MaxWidth.Medium, FullWidth = false, NoHeader = false };
            var dlg = await dialog.ShowAsync<EditCommunityDialog>(T["Edit communiy"], options: opts, parameters: p);
            var res = await dlg.Result;
            var r = res?.Data as CommunityEditModel;
            if (r != null && r.Id.HasValue)
            {
                var cmd = new UpdateCommunityCommand(Id: r.Id.Value, Name: r.Name, OwnerId: r.Owner.Id, Description: r.Description, BaseUrl: r.BaseUrl);
                var resp = await api.UpdateCommunity(cmd);
                if (!resp.IsSuccessful)
                {
                    snackbar.AddWarning(resp.ErrorMessage);
                }
                await grid.ReloadServerData();
            }
        }
    }

    public async Task Delete()
    {
        throw new NotImplementedException();
    }

    public async Task CreateGroup()
    {
        if (SelectedCommunity != null)
        {
            var p = new DialogParameters<CreateGroupDialog> { { d => d.Model, new CreateGroupCommandModel { Community = this.SelectedItem } } };
            DialogOptions opts = new() { MaxWidth = MaxWidth.Medium, FullWidth = false, NoHeader = false };
            var dlg = await dialog.ShowAsync<CreateGroupDialog>(T["Create a new group"], options: opts, parameters: p);
            var res = await dlg.Result;
            var cmd = res?.Data as CreateGroupCommand;
            if (cmd != null)
            {
                var resp = await api.CreateGroup(cmd);
                if (!resp.IsSuccessful)
                {
                    snackbar.AddWarning(resp.ErrorMessage);
                }
                else
                {
                    await LoadCommunity(SelectedItem.Id);
                }
            }
        }
    }


    public async Task AddGroup(GroupListDto group)
    {
        if (SelectedCommunity != null) {
            var cmd = new AssignGroupToCommunityCommand(CommunityId: SelectedCommunity.Id, GroupId: group.Id, Remove: false);
            var resp = await api.AssignGroupToCommunity(cmd);

            if (resp.IsSuccessful)
            {
                await LoadCommunity(SelectedItem.Id);
            }
            else
            {
                snackbar.AddWarning(resp.ErrorMessage);
            }
        }
    }

    public async Task RemnoveGroup(GroupListDto group)
    {
        if (SelectedCommunity != null)
        {
            var cmd = new AssignGroupToCommunityCommand(CommunityId: SelectedCommunity.Id, GroupId: group.Id, Remove: true);
            var resp = await api.AssignGroupToCommunity(cmd);

            if (resp.IsSuccessful)
            {
                await LoadCommunity(SelectedItem.Id);
            }
            else
            {
                snackbar.AddWarning(resp.ErrorMessage);
            }
        }
    }
}



public class CommunityEditModel
{
    public Guid? Id { get; set; }

    [Required]
    public string Name { get; set; } = "";

    public string Description { get; set; } = "";
    public string BaseUrl { get; set; } = "";

    public UserListDto? Owner { get; set; }
}