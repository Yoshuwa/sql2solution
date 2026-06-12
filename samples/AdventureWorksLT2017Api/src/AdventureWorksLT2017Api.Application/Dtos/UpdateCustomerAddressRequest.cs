namespace AdventureWorksLT2017Api.Application.Dtos;

public sealed partial record UpdateCustomerAddressRequest
(
    string AddressType,
    Guid Rowguid,
    DateTime ModifiedDate
);
