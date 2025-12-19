using Refit;

namespace iPath.Blazor.Componenents.Extensions;

public static class SnackbarExtension
{
    public static void AddError(this ISnackbar snack, string message) => snack.Add(message, Severity.Error);
    public static void AddWarning(this ISnackbar snack, string message) => snack.Add(message, Severity.Warning);
    public static void AddInfo(this ISnackbar snack, string message) => snack.Add(message, Severity.Info);


    public static void ShowIfError(this ISnackbar snack, IApiResponse resp)
    {
        if (!resp.IsSuccessful) snack.Add(resp.ErrorMessage, Severity.Error);
    }
}
