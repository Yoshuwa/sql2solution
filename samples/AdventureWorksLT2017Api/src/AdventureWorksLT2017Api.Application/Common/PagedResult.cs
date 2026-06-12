namespace AdventureWorksLT2017Api.Application.Common;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);