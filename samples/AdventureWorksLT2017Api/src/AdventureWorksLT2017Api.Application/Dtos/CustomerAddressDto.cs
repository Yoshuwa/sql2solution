namespace AdventureWorksLT2017Api.Application.Dtos;

public sealed partial record CustomerAddressDto
(
    int CustomerID,
    int AddressID,
    string AddressType,
    Guid Rowguid,
    DateTime ModifiedDate
);
