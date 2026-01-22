namespace iPath.Application;

public static class StringExtension
{
    public static string ShortenTo(this string? Text, int Maxlength, bool MakeOneLine = false)
    {
        if (string.IsNullOrEmpty(Text)) return "";

        string tmp = MakeOneLine ?
            Text.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim() :
            Text.Trim();

        if ( tmp.Length < Maxlength  )
        {
            return tmp;
        }
        else
        {
            return tmp.Substring( 0, Maxlength ) + "...";
        }
    }


    public static string Append(this string value, string append, string separator = ", ")
    {
        if (!string.IsNullOrEmpty(value))
        {
            value += separator;
        }
        value += append;

        return value;
    }
}
