namespace AdventureWorksLT2017Api.Application.Dtos;

public sealed partial record CreateCustomerAddressRequest
(
    string AddressType,
    Guid Rowguid,
    DateTime ModifiedDate
);
