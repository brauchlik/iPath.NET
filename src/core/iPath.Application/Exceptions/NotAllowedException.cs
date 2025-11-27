namespace iPath.Application.Exceptions;

public class NotAllowedException : Exception
{
    public NotAllowedException()
    {
    }

    public NotAllowedException(string Message) : base(Message)
    {
    }
}

