namespace AdventureWorksLT2017Api.Application.Dtos;

public sealed partial record CreateCustomerRequest
(
    bool NameStyle,
    string? Title,
    string FirstName,
    string? MiddleName,
    string LastName,
    string? Suffix,
    string? CompanyName,
    string? SalesPerson,
    string? EmailAddress,
    string? Phone,
    string PasswordHash,
    string PasswordSalt,
    Guid Rowguid,
    DateTime ModifiedDate
);
