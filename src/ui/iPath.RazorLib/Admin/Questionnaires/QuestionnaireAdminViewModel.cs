using Ardalis.GuardClauses;
using FluentResults;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace iPath.Blazor.Componenents.Admin.Questionnaires;

public class QuestionnaireAdminViewModel(ISnackbar snackbar, IDialogService dialog, IPathApi api, IStringLocalizer T, NavigationManager nm)
    : IViewModel
{
    public MudDataGrid<QuestionnaireListDto> grid;

    public bool ShowInactive { 
        get => field;
        set
        {
            if (field != value)
            {
                field = value;
                grid.ReloadServerData();
            }
        }
    }

    public List<BreadcrumbItem> BreadCrumbs
    {
        get
        {
            var ret = new List<BreadcrumbItem> { new(T["Administration"], href: "admin") };
            if (SelectedQuestionnaire is null)
            {
                ret.Add(new(T["Questionnaires"], href: null, disabled: true));
            }
            else
            {
                ret.Add(new(T["Questionnaires"], href: "admin/questionnaires"));
                ret.Add(new(SelectedQuestionnaire.Name, href: null, disabled: true));
            }
            return ret;
        }
    }


    public async Task<GridData<QuestionnaireListDto>> GetData(GridState<QuestionnaireListDto> state, CancellationToken ct = default)
    {
        var query = state.BuildQuery(new GetQuestionnaireListQuery { AllVersions = ShowInactive });
        var resp = await api.GetQuestionnnaires(query);
        if (resp.IsSuccessful) return resp.Content.ToGridData();
        snackbar.AddError(resp.ErrorMessage);
        return new GridData<QuestionnaireListDto>();
    }


    public async Task Create()
    {
        var dlg = await dialog.ShowAsync<DlgEditQuestionnaire>();
        var res = await dlg.Result;
        if (res?.Data is EditQuestionnaireModel)
        {
            var m = (EditQuestionnaireModel)res.Data;
            var resp = await api.CreateQuestionnaire(new UpdateQuestionnaireCommand(m.QuestionnaireId, m.Name, m.Resource, Settings: null, IsActive: true, insert: true));
            if (resp.IsSuccessful)
            {
                await grid.ReloadServerData();
            }
            else
            {
                snackbar.AddError(resp.ErrorMessage);
            }
        }
    }


    public QuestionnaireEntity? SelectedQuestionnaire;
    public async Task Load(Guid Id)
    {
        var resp = await api.GetQuestionnaireById(Id);
        if (snackbar.CheckSuccess(resp))
        {
            SelectedQuestionnaire = resp.Content;
        }
    }


    public async Task Edit(QuestionnaireListDto item)
    {
        if (item != null)
        {
            var resp = await api.GetQuestionnaireById(item.Id);
            if (resp.IsSuccessful)
            {
                var m = new EditQuestionnaireModel(resp.Content);
                var p = new DialogParameters<DlgEditQuestionnaire> { { x => x.Model, m } };
                var dlg = await dialog.ShowAsync<DlgEditQuestionnaire>("...", parameters: p);
                var res = await dlg.Result;
                if (res?.Data is EditQuestionnaireModel)
                {
                    var r = (EditQuestionnaireModel)res.Data;

                    await Save(r);
                    await grid.ReloadServerData();
                    snackbar.Add("Questionnaire updated", Severity.Success);
                }
            }
        }
    }

    public async Task<bool> Save(EditQuestionnaireModel r)
    {
        // validate resource
        try
        {
            var options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);
            var q = JsonSerializer.Deserialize<Questionnaire>(r.Resource, options);
        }
        catch (Exception ex)
        {
            snackbar.AddError("Resource is not valid: " + ex.Message);
            return false;
        }

        r.Settings.Filename = r.ResourceFileName;
        var resp = await api.CreateQuestionnaire(new UpdateQuestionnaireCommand(r.QuestionnaireId, r.Name, r.Resource,
            Settings: r.Settings, IsActive: r.IsActive, insert: false));
        return snackbar.CheckSuccess(resp);
    }

    public async Task Delete(QuestionnaireListDto item)
    {
        snackbar.Add("not implemented yet", Severity.Info);
    }



    public iPath.LHCForms.LhcForm PreviewForm;
    public async Task RenderPreview()
    {
        if (PreviewForm is not null && SelectedQuestionnaire is not null)
        {
            await PreviewForm.LoadFormAsync(SelectedQuestionnaire.Resource, "");
        }
    }



    public const long maxMemFile = 100000;
    public async Task<Result> UploadFile(InputFileChangeEventArgs e, EditQuestionnaireModel Model)
    {
        if (e.File is not null)
        {
            Model.ResourceFileName = e.File.Name;

            if (e.File.Size < maxMemFile)
            {
                try
                {
                    using var memoryStream = new MemoryStream();
                    await e.File.OpenReadStream(maxMemFile).CopyToAsync(memoryStream);

                    // parse as FHIR Questionnaire
                    var fhir = System.Text.Encoding.Default.GetString(memoryStream.ToArray());
                    var options = new JsonSerializerOptions().ForFhir(Hl7.Fhir.Model.ModelInfo.ModelInspector);
                    var qr = JsonSerializer.Deserialize<Hl7.Fhir.Model.Questionnaire>(fhir, options);

                    // extract id & title
                    if (string.IsNullOrEmpty(Model.QuestionnaireId))
                    {
                        Model.QuestionnaireId = qr.Id;
                    }
                    if (string.IsNullOrEmpty(Model.Name))
                    {
                        Model.Name = qr.Title;
                    }

                    // serialize as JSON again
                    options = new JsonSerializerOptions().ForFhir(Hl7.Fhir.Model.ModelInfo.ModelInspector).Pretty();
                    Model.Resource = JsonSerializer.Serialize(qr, options);

                    var l = Model.Resource.Length;

                    return Result.Ok();
                }
                catch (Exception ex)
                {
                    return Result.Fail(T["The content provided is not a valid FHIR Quesionnaire"]);
                }
            }
        }
        return Result.Fail(T["no content uploaded"]);
    }
}

public class EditQuestionnaireModel
{
    public Guid? Id { get; init; }
    public int Version { get; init; }

    [Required]
    public string QuestionnaireId { get; set; }

    [Required]
    public string Name { get; set; }

    public bool IsActive { get; set; }

    public IBrowserFile? ResourceFile { get; set; }
    public string ResourceFileName { get; set; }

    public string? Resource { get; set; }

    public QuestionnaireSettings Settings { get; set; } = null;

    public EditQuestionnaireModel()
    {
    }

    public EditQuestionnaireModel(QuestionnaireEntity e)
    {
        Guard.Against.Null(e);
        Id = e.Id;
        QuestionnaireId = e.QuestionnaireId;
        Name = e.Name;
        Version = e.Version;
        IsActive = e.IsActive;
        Resource = e.Resource;
        Settings = e.Settings ?? new();
    }
}
