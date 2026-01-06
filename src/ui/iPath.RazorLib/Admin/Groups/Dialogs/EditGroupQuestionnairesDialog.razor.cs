using iPath.Blazor.Componenents.Questionaiires;
using Microsoft.Extensions.Localization;

namespace iPath.Blazor.Componenents.Admin.Groups;

public partial class EditGroupQuestionnairesDialog(IPathApi api, IStringLocalizer T)
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; }

    [Parameter]
    public GroupDto Model { get; set; }

    bool saving;
    string ErrorMessage = "";
    QuestionnaireListDto selectedQ;

    List<GroupQuestionnareModel> Items = new();

    eQuestionnaireUsage[] Usages => QuestionnairesViewModel.Usages;

    protected override async Task OnParametersSetAsync()
    {
        foreach (var q in Model.Questionnaires)
        {
            var item = Items.FirstOrDefault(x => x.QuestionnaireId == q.qId);
            if (item is null)
            {
                item = new GroupQuestionnareModel(q.qId, q.QuestinnaireId, q.QuestinnaireName, Model.Id);
            }
            item.Usage[q.Usage] = true;
        }
    }

    void OnQuestionaireSelected()
    {
        if (selectedQ is not null)
        {
            var item = Items.FirstOrDefault(x => x.QuestionnaireId == selectedQ.Id);
            if (item is null)
            {
                item = new GroupQuestionnareModel(selectedQ.Id, selectedQ.QuestionnaireId, selectedQ.Name , Model.Id);
                Items.Add(item);
            }
            selectedQ = null;
            StateHasChanged();
        }
    }

    private void Cancel() => MudDialog.Cancel();

    private async Task Save(GroupQuestionnareModel model, eQuestionnaireUsage change)
    {
        saving = true;
        try
        {
            bool remove = !model.Usage[change];
            var cmd = new AssignQuestionnaireToGroupCommand(model.QuestionnaireId, model.GrouppId, change, remove);
            var resp = await api.AssignQuestionnaireToGroup(cmd);
        }
        catch(Exception ex)
        {
        }
        saving = false;
    }
}
