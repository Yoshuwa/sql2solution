namespace AdventureWorksLT2017Api.Domain.Entities;

public sealed partial class ProductDescription
{
    public int ProductDescriptionID { get; set; }

    public string Description { get; set; } = string.Empty;

    public Guid Rowguid { get; set; }

    public DateTime ModifiedDate { get; set; }

}
