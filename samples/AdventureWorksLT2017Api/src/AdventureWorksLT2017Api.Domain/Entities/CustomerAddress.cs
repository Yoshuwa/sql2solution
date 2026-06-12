namespace AdventureWorksLT2017Api.Domain.Entities;

public sealed partial class CustomerAddress
{
    public int CustomerID { get; set; }

    public int AddressID { get; set; }

    public string AddressType { get; set; } = string.Empty;

    public Guid Rowguid { get; set; }

    public DateTime ModifiedDate { get; set; }

}
