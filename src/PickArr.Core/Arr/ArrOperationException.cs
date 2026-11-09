namespace Pickarr.Core.Arr;

public sealed class ArrOperationException : Exception
{
    public ArrOperationException(
        string code,
        int statusCode,
        string message,
        IReadOnlyList<string>? errors = null)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        Errors = errors ?? [];
    }

    public string Code { get; }
    public int StatusCode { get; }
    public IReadOnlyList<string> Errors { get; }

    public static ArrOperationException NotConfigured() =>
        new("ARR_NOT_CONFIGURED", StatusCodes.ServiceUnavailable, "Arr instance is not configured.");

    public static ArrOperationException Unreachable(string detail) =>
        new("ARR_UNREACHABLE", StatusCodes.ServiceUnavailable, detail);

    public static ArrOperationException Unauthorized(string detail) =>
        new("ARR_UNAUTHORIZED", StatusCodes.BadGateway, detail);

    public static ArrOperationException IdUnmapped(string detail) =>
        new("ARR_ID_UNMAPPED", StatusCodes.UnprocessableEntity, detail);

    public static ArrOperationException AlreadyExists(string detail) =>
        new("ARR_ALREADY_EXISTS", StatusCodes.Conflict, detail);

    public static ArrOperationException Rejected(string detail, IReadOnlyList<string>? errors = null) =>
        new("ARR_REJECTED", StatusCodes.FailedDependency, detail, errors);

    public static ArrOperationException NotInLibrary() =>
        new("ARR_NOT_IN_LIBRARY", StatusCodes.NotFound, "Title is not in the Arr library cache.");

    public static ArrOperationException TitleNotFound(int titleId) =>
        new("TITLE_NOT_FOUND", StatusCodes.NotFound, $"Title {titleId} was not found.");

    private static class StatusCodes
    {
        public const int NotFound = 404;
        public const int Conflict = 409;
        public const int UnprocessableEntity = 422;
        public const int FailedDependency = 424;
        public const int BadGateway = 502;
        public const int ServiceUnavailable = 503;
    }
}
