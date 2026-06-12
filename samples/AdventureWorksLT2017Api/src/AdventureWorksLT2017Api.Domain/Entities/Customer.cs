namespace AdventureWorksLT2017Api.Domain.Entities;

public sealed partial class Customer
{
    public int CustomerID { get; set; }

    public bool NameStyle { get; set; }

    public string? Title { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string? MiddleName { get; set; }

    public string LastName { get; set; } = string.Empty;

    public string? Suffix { get; set; }

    public string? CompanyName { get; set; }

    public string? SalesPerson { get; set; }

    public string? EmailAddress { get; set; }

    public string? Phone { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    public string PasswordSalt { get; set; } = string.Empty;

    public Guid Rowguid { get; set; }

    public DateTime ModifiedDate { get; set; }

}
