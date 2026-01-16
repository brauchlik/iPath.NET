namespace iPath.Application.Contracts;

public interface IValidatable
{
    Dictionary<string, string[]> Validate();
}
