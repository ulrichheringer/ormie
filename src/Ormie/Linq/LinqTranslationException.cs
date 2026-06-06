namespace Ormie.Linq;

public sealed class LinqTranslationException : Exception
{
    public LinqTranslationException(string message)
        : base(message)
    {
    }

    public LinqTranslationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
