using iPath.Application.Features.Documents;

namespace iPath.Blazor.Componenents.Documents;

public class DocumentViewModel(IPathApi api, IStringLocalizer T, ISnackbar snackbar, IDialogService dialog, NavigationManager nm) : IViewModel
{
    public async Task ShowPropertiesDialog(DocumentDto dto)
    {
        var parameter = new DialogParameters<DocumentPropertiesDialog> { { x => x.Model, dto } };
        var dlg = await dialog.ShowAsync<DocumentPropertiesDialog>(T["Document Properties"], parameter);
    }
}
