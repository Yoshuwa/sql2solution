namespace AdventureWorksLT2017Api.Application.Common;

public sealed record ApiResponse<T>(
    int Code,
    string Message,
    T? Body)
{
    public const int SuccessCode = 0;
    public const int WarningCode = 1;
    public const int ErrorCode = 2;

    public static ApiResponse<T> Success(string message, T? body) => new(SuccessCode, message, body);
    public static ApiResponse<T> Warning(string message, T? body = default) => new(WarningCode, message, body);
    public static ApiResponse<T> Error(string message, T? body = default) => new(ErrorCode, message, body);
}