namespace AdventureWorksLT2017Api.Application.Dtos;

public sealed partial record ErrorLogDto
(
    int ErrorLogID,
    DateTime ErrorTime,
    string UserName,
    int ErrorNumber,
    int? ErrorSeverity,
    int? ErrorState,
    string? ErrorProcedure,
    int? ErrorLine,
    string ErrorMessage
);
