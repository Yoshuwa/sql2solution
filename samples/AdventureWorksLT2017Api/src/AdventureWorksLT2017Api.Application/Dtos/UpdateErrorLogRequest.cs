namespace AdventureWorksLT2017Api.Application.Dtos;

public sealed partial record UpdateErrorLogRequest
(
    DateTime ErrorTime,
    string UserName,
    int ErrorNumber,
    int? ErrorSeverity,
    int? ErrorState,
    string? ErrorProcedure,
    int? ErrorLine,
    string ErrorMessage
);
