namespace DriverTime.Application.DDD.Exceptions;

public class DuplicateDddFileException : InvalidOperationException
{
    public const string DefaultMessage = "Plik został już wcześniej zaimportowany.";

    public DuplicateDddFileException()
        : base(DefaultMessage)
    {
    }
}
