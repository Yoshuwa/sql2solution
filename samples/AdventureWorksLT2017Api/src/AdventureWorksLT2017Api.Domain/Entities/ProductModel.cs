namespace AdventureWorksLT2017Api.Domain.Entities;

public sealed partial class ProductModel
{
    public int ProductModelID { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? CatalogDescription { get; set; }

    public Guid Rowguid { get; set; }

    public DateTime ModifiedDate { get; set; }

}
