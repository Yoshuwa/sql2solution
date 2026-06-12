namespace AdventureWorksLT2017Api.Domain.Entities;

public sealed partial class ProductModelProductDescription
{
    public int ProductModelID { get; set; }

    public int ProductDescriptionID { get; set; }

    public string Culture { get; set; } = string.Empty;

    public Guid Rowguid { get; set; }

    public DateTime ModifiedDate { get; set; }

}
