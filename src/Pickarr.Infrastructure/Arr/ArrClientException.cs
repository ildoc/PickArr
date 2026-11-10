namespace Pickarr.Infrastructure.Arr;

public sealed class ArrClientException : Exception
{
    public ArrClientException(int statusCode, string message, IReadOnlyList<string>? errors = null)
        : base(message)
    {
        StatusCode = statusCode;
        Errors = errors ?? [];
    }

    public int StatusCode { get; }
    public IReadOnlyList<string> Errors { get; }
}
