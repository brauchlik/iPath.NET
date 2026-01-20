using iPath.Blazor.Componenents.Admin.Groups;
using iPath.Blazor.Componenents.Admin.Users;
using iPath.Blazor.Componenents.Questionaiires;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

namespace iPath.Blazor.Componenents.Admin.Communities;

public class CommunityAdminViewModel(IPathApi api, 
    ISnackbar snackbar, 
    IDialogService dialog,
    IStringLocalizer T,
    NavigationManager nm,
    ILogger<CommunityAdminViewModel> logger)
    : IViewModel
{
    public Action OnChange { get; set; }


    public List<BreadcrumbItem> BreadCrumbs {
        get
        {
            var ret = new List<BreadcrumbItem> { new("Administration", href: "admin") };
            if (SelectedCommunity is null)
            {
                ret.Add(new("Communities", href: null, disabled: true));
            }
            else
            {
                ret.Add(new("Communities", href: "admin/communities"));
                ret.Add(new(SelectedCommunity.Name, href: null, disabled: true));
            }
            return ret;
        }
    }


    public MudDataGrid<CommunityListDto> grid;

    public async Task<GridData<CommunityListDto>> GetListAsync(GridState<CommunityListDto> state, CancellationToken ct = default)
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
    public async Task<CommunityDto> LoadCommunity(Guid? id)
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
        return SelectedCommunity;
    }

    public void GoToCommunity(CommunityListDto dto)
    {
        nm.NavigateTo($"admin/communities/{dto.Id}");
    }





    public async Task<List<CommunityMemberModel>> GetMembersAsync(Guid? communityId = null, CancellationToken ct = default)
    {
        communityId ??= SelectedCommunity?.Id;
        if (communityId.HasValue)
        {
            try
            {
                var resp = await api.GetCommunityMembers(communityId.Value);

                if (resp.IsSuccessful)
                {
                    return resp.Content.Items.Select(m => new CommunityMemberModel(dto: m, m.Communityname, m.UserId, m.Username)).ToList();
                }
                snackbar.AddWarning(resp.ErrorMessage);
            }
            catch(Exception ex)
            {
                snackbar.AddError(ex.Message);
            }
        }
        return new List<CommunityMemberModel>();
    }

    public async Task UpdateMember(CommunityMemberModel m)
    {
        var cmd = new AssignUserToCommunityCommand(communityId: m.CommunityId, userId: m.UserId, role: m.Role);

        var resp = await api.AssignUserToCommunity(cmd);
        if (!resp.IsSuccessful)
            snackbar.AddError(resp.ErrorMessage);

        OnChange?.Invoke();
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
            var cmd = new CreateCommunityCommand(Name: m.Name, OwnerId: m.Owner.Id, Description: m.Settings.Description, BaseUrl: m.Settings.BaseUrl);
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
            await LoadCommunity(SelectedCommunity.Id);

            var m = new CommunityEditModel
            {
                Id = SelectedCommunity.Id,
                Name = SelectedCommunity.Name,
                Owner = SelectedCommunity.Owner,
                Settings = SelectedCommunity.Settings.Clone()
            };

            var p = new DialogParameters<EditCommunityDialog> { { x => x.Model, m } };
            DialogOptions opts = new() { MaxWidth = MaxWidth.Medium, FullWidth = false, NoHeader = false };
            var dlg = await dialog.ShowAsync<EditCommunityDialog>(T["Edit communiy"], options: opts, parameters: p);
            var res = await dlg.Result;
            await UpdateCommunity(res?.Data as CommunityEditModel);           
        }
    }

    public async Task<bool> UpdateCommunity(CommunityEditModel r)
    {
        if (r != null && r.Id.HasValue)
        {
            var cmd = new UpdateCommunityCommand(Id: r.Id.Value, Name: r.Name, OwnerId: r.Owner.Id, Settings: r.Settings);
            var resp = await api.UpdateCommunity(cmd);
            if (!resp.IsSuccessful)
            {
                snackbar.AddWarning(resp.ErrorMessage);
            }
            if (grid is not null)
            {
                await grid.ReloadServerData();
            }
            return true;
        }
        return false;
    }


    public async Task Delete()
    {
        throw new NotImplementedException();
    }

    public async Task CreateGroup()
    {
        if (SelectedCommunity != null)
        {
            var p = new DialogParameters<CreateGroupDialog> {
                { d => d.Model, new CreateGroupCommandModel { Community = SelectedCommunity.ToListDto() } },
                { d => d.DisableCommunityInput, true }
            };
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
                    await LoadCommunity(SelectedCommunity.Id);
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

    public async Task RemoveGroup(GroupListDto group)
    {
        if (SelectedCommunity != null)
        {
            var cmd = new AssignGroupToCommunityCommand(CommunityId: SelectedCommunity.Id, GroupId: group.Id, Remove: true);
            var resp = await api.AssignGroupToCommunity(cmd);

            if (resp.IsSuccessful)
            {
                await LoadCommunity(SelectedCommunity.Id);
            }
            else
            {
                snackbar.AddWarning(resp.ErrorMessage);
            }
        }
    }




    public async Task SaveQuestionnaireUsage(CommunityQuestionnareModel model, eQuestionnaireUsage change)
    {
        try
        {
            bool remove = !model.Usage[change];
            var cmd = new AssignQuestionnaireCommand(Id: model.QuestionnaireId, change, remove, CommunityId: model.CommunityId);
            var resp = await api.AssignQuestionnaire(cmd);
            if (resp.IsSuccessful) return;

            snackbar.AddError(resp.ErrorMessage);
        }
        catch (Exception ex)
        {
            snackbar.AddError(ex.Message);
        }

        // on error => reset Usage
        model.Usage[change] = !model.Usage[change];
    }
}



public class CommunityEditModel
{
    public Guid? Id { get; set; }

    [Required]
    public string Name { get; set; } = "";

    public CommunitySettings Settings { get; set; } = new();

    public OwnerDto? Owner { get; set; }
}


public class CommunityQuestionnareModel
{
    public Guid QuestionnaireId { get; init; }

    public string Id { get; init; }
    public string Name { get; init; }

    public string NameAndId => $"{Name} [{Id}]";

    public Guid CommunityId { get; init; }

    public Dictionary<eQuestionnaireUsage, bool> Usage = new();

    public CommunityQuestionnareModel(Guid QuestionnaireId, string Id, string Name, Guid CommunityId)
    {
        this.QuestionnaireId = QuestionnaireId;
        this.Id = Id;
        this.Name = Name;
        this.CommunityId = CommunityId;

        foreach (var e in QuestionnairesViewModel.Usages)
        {
            Usage.Add((eQuestionnaireUsage)e, false);
        }
    }
}