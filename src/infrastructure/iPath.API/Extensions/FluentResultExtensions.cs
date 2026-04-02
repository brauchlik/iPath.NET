namespace iPath.API;

public static class FluentResultExtensions
{
    public static IResult ToMinimalApiResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
            return TypedResults.Ok(result.Value);

        // TODO: Hier die Logik für verschiedene Fehlertypen einbauen
        return TypedResults.BadRequest(result.Errors.Select(e => e.Message));
    }
}
