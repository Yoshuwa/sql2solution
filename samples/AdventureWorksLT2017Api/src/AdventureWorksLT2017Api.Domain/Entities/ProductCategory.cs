namespace AdventureWorksLT2017Api.Domain.Entities;

public sealed partial class ProductCategory
{
    public int ProductCategoryID { get; set; }

    public int? ParentProductCategoryID { get; set; }

    public string Name { get; set; } = string.Empty;

    public Guid Rowguid { get; set; }

    public DateTime ModifiedDate { get; set; }

}
