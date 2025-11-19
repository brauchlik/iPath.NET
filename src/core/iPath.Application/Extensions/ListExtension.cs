namespace iPath.Application;

public static class ListExtension
{
    public static bool IsEmpty<T>(this IReadOnlyList<T>? list)
    {
        return list is null || !list.Any();
    }

    public static bool IsEmpty<T>(this IList<T>? list)
    {
        return list is null || !list.Any();
    }

    public static bool IsEmpty<T>(this IEnumerable<T>? list)
    {
        return list is null || !list.Any();
    }


    public static bool IsEmpty<T>(this HashSet<T>? list)
    {
        return list is null || !list.Any();
    }
}