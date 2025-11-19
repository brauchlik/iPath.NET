namespace iPath.Blazor.Componenents.Extensions;

public static class SnackbarExtension
{
    public static void AddError(this ISnackbar snack, string message) => snack.Add(message, Severity.Error);
    public static void AddWarning(this ISnackbar snack, string message) => snack.Add(message, Severity.Warning);
    public static void AddInfo(this ISnackbar snack, string message) => snack.Add(message, Severity.Info);
}
