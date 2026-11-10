namespace Pickarr.Core.Catalog;

public sealed class WatchmodeQuotaExceededException : Exception
{
    public WatchmodeQuotaExceededException(string message)
        : base(message)
    {
    }

    public WatchmodeQuotaExceededException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
