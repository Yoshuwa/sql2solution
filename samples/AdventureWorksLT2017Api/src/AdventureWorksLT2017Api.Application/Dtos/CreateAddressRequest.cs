namespace AdventureWorksLT2017Api.Application.Dtos;

public sealed partial record CreateAddressRequest
(
    string AddressLine1,
    string? AddressLine2,
    string City,
    string StateProvince,
    string CountryRegion,
    string PostalCode,
    Guid Rowguid,
    DateTime ModifiedDate
);
