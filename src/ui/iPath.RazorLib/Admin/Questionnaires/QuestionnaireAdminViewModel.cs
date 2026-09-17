using Ardalis.GuardClauses;
using FluentResults;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using iPath.Application.Features.Questionnaires;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace iPath.Blazor.Componenents.Admin.Questionnaires;

public class QuestionnaireAdminViewModel(ISnackbar snackbar, IDialogService dialog, IPathApi api, IStringLocalizer T, NavigationManager nm, IServiceProvider sp, IQuestionnaireToTextServiceRegistry previewRegistry)
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
        snackbar.AddError(resp.ErrorText());
        return new GridData<QuestionnaireListDto>();
    }


    public async Task Create()
    {
        var dlg = await dialog.ShowAsync<DlgEditQuestionnaire>();
        var res = await dlg.Result;
        if (res?.Data is EditQuestionnaireModel)
        {
            var m = (EditQuestionnaireModel)res.Data;

            // carry the picked filename into the settings - the entity only stores the name there
            m.Settings ??= new();
            m.Settings.Filename = m.ResourceFileName;

            var resp = await api.CreateQuestionnaire(new UpdateQuestionnaireCommand(m.QuestionnaireId, m.Name, m.Resource, Settings: m.Settings, IsActive: true, insert: true));
            if (resp.IsSuccessful)
            {
                await grid.ReloadServerData();
            }
            else
            {
                snackbar.AddError(resp.ErrorText());
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
            snackbar.AddError(T["Resource is not valid: {0}", ex.Message]);
            return false;
        }

        r.Settings.Filename = r.ResourceFileName;
        var resp = await api.CreateQuestionnaire(new UpdateQuestionnaireCommand(r.QuestionnaireId, r.Name, r.Resource,
            Settings: r.Settings, IsActive: r.IsActive, insert: false));
        return snackbar.CheckSuccess(resp);
    }

    public async Task Delete(QuestionnaireListDto item)
    {
        var r = await dialog.ShowMessageBoxAsync(
            title: T["Delete Questionnaire"],
            message: T["Delete this version permanently, or just deactivate it? Deactivating keeps its history and any existing cases that already used it are unaffected."],
            yesText: T["Delete"], noText: T["Deactivate"], cancelText: T["Cancel"]);

        if (r == true)
        {
            var resp = await api.DeleteQuestionnaire(item.Id);
            if (resp.IsSuccessful)
            {
                snackbar.Add(T["Questionnaire deleted"], Severity.Success);
                await grid.ReloadServerData();
            }
            else
            {
                snackbar.AddError(resp.ErrorText());
            }
        }
        else if (r == false)
        {
            var full = await api.GetQuestionnaireById(item.Id);
            if (snackbar.CheckSuccess(full))
            {
                var resp = await api.CreateQuestionnaire(new UpdateQuestionnaireCommand(
                    full.Content.QuestionnaireId, full.Content.Name, full.Content.Resource,
                    Settings: full.Content.Settings, IsActive: false, insert: false));
                if (snackbar.CheckSuccess(resp))
                {
                    snackbar.Add(T["Questionnaire deactivated"], Severity.Success);
                    await grid.ReloadServerData();
                }
            }
        }
    }



    public iPath.LHCForms.LhcForm PreviewForm;
    public async Task RenderPreview()
    {
        if (PreviewForm is not null && SelectedQuestionnaire is not null)
        {
            await PreviewForm.LoadFormAsync(SelectedQuestionnaire.Resource, "");
        }
    }
    public string PreviewText { get; private set; } = string.Empty;
    public string PreviewResponseJson { get; private set; } = string.Empty;

    public List<ConformityFinding> ConformityFindings { get; private set; } = new();
    public bool ConformityChecked { get; private set; }

    /// <summary>
    /// Runs the extraction conformity rules against the selected questionnaire definition. The rules
    /// live with the extractor on the server, so the checker reports exactly what extraction would do.
    /// </summary>
    public async Task CheckConformity()
    {
        ConformityFindings = new();
        if (SelectedQuestionnaire is null) return;

        try
        {
            var resp = await api.GetQuestionnaireConformity(SelectedQuestionnaire.QuestionnaireId, SelectedQuestionnaire.Version);
            if (resp.IsSuccessful && resp.Content is not null)
            {
                ConformityFindings = resp.Content;
            }
            else
            {
                snackbar.AddWarning(resp.ErrorText());
            }
        }
        finally
        {
            ConformityChecked = true;
        }
    }

    public async Task<string> GetPreviewText()
    {
        try
        {
            var options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);
            var q = JsonSerializer.Deserialize<Questionnaire>(PreviewForm.Questionnaire, options);

            var qr = await PreviewForm.GetDataAsync();
            var r = JsonSerializer.Deserialize<QuestionnaireResponse>(qr, options);

            PreviewResponseJson = qr ?? string.Empty;

            // the questionnaire's own mode wins; otherwise the configured default applies
            var serviceKey = SelectedQuestionnaire?.Settings?.TextPreviewService;
            var fallbackKey = previewRegistry.GetDefault().Key;
            var q2t = (string.IsNullOrEmpty(serviceKey) ? null : sp.GetKeyedService<IQuestionnaireToTextService>(serviceKey))
                      ?? sp.GetRequiredKeyedService<IQuestionnaireToTextService>(fallbackKey);

            PreviewText = q2t.CreateText(r, q);
            return PreviewText;
        }
        catch(Exception ex)
        {
            PreviewResponseJson = string.Empty;
            return "Error: " + ex.Message;
        }        
    }



    public async Task<Result> UploadFile(InputFileChangeEventArgs e, EditQuestionnaireModel Model)
    {
        if (e.File is null)
        {
            return Result.Fail(T["no content uploaded"]);
        }

        Model.ResourceFileName = e.File.Name;

        // Read the file in full. The browser applies no size limit of its own, but
        // OpenReadStream defaults to 500 KB, so pass the file's own size.
        // No parsing or validation here - the handler is the only place content is trusted.
        try
        {
            using var memoryStream = new MemoryStream();
            await using (var stream = e.File.OpenReadStream(e.File.Size))
            {
                await stream.CopyToAsync(memoryStream);
            }

            Model.Resource = System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
        }
        catch (Exception ex)
        {
            return Result.Fail(T["The file could not be read"] + ": " + ex.Message);
        }

        Model.Settings ??= new();
        Model.Settings.Filename = Model.ResourceFileName;

        // Persist through the same handler the Save button uses: it checks the Id and creates a
        // new version carrying the uploaded resource. The API reports problems in the response,
        // which the page surfaces to the user.
        var resp = await api.CreateQuestionnaire(new UpdateQuestionnaireCommand(
            Model.QuestionnaireId, Model.Name, Model.Resource,
            Settings: Model.Settings, IsActive: true, insert: false));

        if (!resp.IsSuccessful)
        {
            return Result.Fail(resp.ErrorText());
        }

        // reload so the model (and the bound code viewer) shows the persisted version
        await Load(resp.Content);

        return Result.Ok();
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

    // never null: the page renders a default model while the entity is still loading, and the
    // tabs read Model.Settings directly
    public QuestionnaireSettings Settings { get; set; } = new();

    // The settings flag is nullable so that a questionnaire stored before the property existed
    // counts as enabled. A checkbox cannot express that, so it is surfaced as a plain bool with
    // null meaning "on" - otherwise opening and saving a form would silently switch extraction off.
    public bool ExtractAnswersEnabled
    {
        get => Settings.ExtractAnswers ?? true;
        set => Settings.ExtractAnswers = value;
    }

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

        // the picked file is not stored on the entity, only its name is (Settings.Filename).
        // Without this the bound field goes blank whenever the model is rebuilt from the entity.
        ResourceFileName = Settings.Filename ?? string.Empty;
    }
}
