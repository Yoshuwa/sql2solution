namespace AdventureWorksLT2017Api.Domain.Entities;

public sealed partial class Address
{
    public int AddressID { get; set; }

    public string AddressLine1 { get; set; } = string.Empty;

    public string? AddressLine2 { get; set; }

    public string City { get; set; } = string.Empty;

    public string StateProvince { get; set; } = string.Empty;

    public string CountryRegion { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;

    public Guid Rowguid { get; set; }

    public DateTime ModifiedDate { get; set; }

}
