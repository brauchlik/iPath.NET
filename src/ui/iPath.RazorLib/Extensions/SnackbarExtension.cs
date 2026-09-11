using iPath.Blazor.ServiceLib.ApiClient;
using Refit;

namespace iPath.Blazor.Componenents.Extensions;

public static class SnackbarExtension
{
    public static void AddError(this ISnackbar snack, string message) => snack.Add(message, Severity.Error);
    public static void AddWarning(this ISnackbar snack, string message) => snack.Add(message, Severity.Warning);
    public static void AddInfo(this ISnackbar snack, string message) => snack.Add(message, Severity.Info);


    // ErrorText() reads the ProblemDetails "detail" the API returns. Refit's own
    // ErrorMessage is the raw HTTP body, which used to surface as JSON in the snackbar.
    public static void ShowIfError(this ISnackbar snack, IApiResponse resp)
    {
        if (!resp.IsSuccessful) snack.Add(resp.ErrorText(), Severity.Error);
    }

    /// <summary>
    /// Checks if response returned success. If no, show warning in Snackbar
    /// </summary>
    /// <param name="snackbar"></param>
    /// <param name="resp"></param>
    /// <param name="SuccessMessage"></param>
    /// <returns>true if response was successfull</returns>
    public static bool CheckSuccess(this ISnackbar snackbar, IApiResponse resp, string? SuccessMessage = null)
    {
        if (resp.IsSuccessful)
        {
            if (!string.IsNullOrEmpty(SuccessMessage)) snackbar.Add(SuccessMessage, Severity.Success);
            return true;
        }
        else
        {
            snackbar.AddError(resp.ErrorText());
            return false;
        }
    }
}
