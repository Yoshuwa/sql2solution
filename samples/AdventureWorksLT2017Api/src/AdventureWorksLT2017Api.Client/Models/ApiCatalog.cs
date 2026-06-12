namespace AdventureWorksLT2017Api.Client.Models;

public sealed partial record ApiField(string Name, string ClrType, bool IsPrimaryKey, bool IsNullable, string InputKind, string LookupResourceKey, string LookupRoute, string LookupValueField, bool IsAuditField, string AuditKind)
{
    public ApiField(string name, string clrType, bool isPrimaryKey, bool isNullable)
        : this(name, clrType, isPrimaryKey, isNullable, ResolveInputKind(clrType), "", "", "", false, "")
    {
    }

    public bool HasLookup => !string.IsNullOrWhiteSpace(LookupRoute) && !string.IsNullOrWhiteSpace(LookupValueField);

    private static string ResolveInputKind(string clrType)
    {
        return clrType switch
        {
            "bool" => "checkbox",
            "DateOnly" => "date",
            "DateTime" or "DateTimeOffset" => "datetime-local",
            "TimeOnly" => "time",
            "byte" or "short" or "int" or "long" or "float" or "double" or "decimal" => "number",
            _ => "text"
        };
    }
}
public sealed partial record ApiWorkflowAction(string Action, string From, string To, IReadOnlyList<string> Roles);
public sealed partial record ApiResource(string Key, string DisplayName, string Route, string PrimaryKey, string DeleteMode, bool CanDelete, bool HardDeleteRequiresConfirm, bool SupportsBulkActions, int DefaultPageSize, int MaxPageSize, string WorkflowStatusField, IReadOnlyList<ApiWorkflowAction> WorkflowActions, IReadOnlyList<ApiField> Fields)
{
    public bool UsesSoftDelete => string.Equals(DeleteMode, "Soft", StringComparison.OrdinalIgnoreCase);
    public string DeleteLabel => UsesSoftDelete ? "Soft delete" : "Hard delete";
    public string DeleteSuccessMessage => UsesSoftDelete ? "Soft delete request completed." : "Hard delete request completed.";
    public bool HasWorkflow => !string.IsNullOrWhiteSpace(WorkflowStatusField) && WorkflowActions.Count > 0;
}
public sealed partial record LookupOption(string Value, string Label);
public sealed partial record FieldInputModel(ApiField Field, IReadOnlyList<LookupOption> LookupOptions, string Prefix);

public static partial class ApiCatalog
{
    public const bool HasAuthentication = false;
    public const bool RequireAuthenticationForMenus = true;
    public const bool IncludeSecurityPages = false;
    public const string MenuStyle = "Sidebar";
    public const bool IsTopNavigation = false;
    public const bool ShowAuditFields = false;
    public const bool HasPermissionManagement = false;
    public static IReadOnlyList<ApiResource> Resources { get; } = new[]
    {
    new ApiResource(
        "Address",
        "Address",
        "api/address",
        "AddressID",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("AddressID", "int", true, false, "number", "", "", "", false, ""),
            new ApiField("AddressLine1", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("AddressLine2", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("City", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("StateProvince", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("CountryRegion", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("PostalCode", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("Rowguid", "Guid", false, false, "text", "", "", "", false, ""),
            new ApiField("ModifiedDate", "DateTime", false, false, "datetime-local", "", "", "", false, "")
        }),
    new ApiResource(
        "Customer",
        "Customer",
        "api/customers",
        "CustomerID",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("CustomerID", "int", true, false, "number", "", "", "", false, ""),
            new ApiField("NameStyle", "bool", false, false, "checkbox", "", "", "", false, ""),
            new ApiField("Title", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("FirstName", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("MiddleName", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("LastName", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("Suffix", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("CompanyName", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("SalesPerson", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("EmailAddress", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("Phone", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("PasswordHash", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("PasswordSalt", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("Rowguid", "Guid", false, false, "text", "", "", "", false, ""),
            new ApiField("ModifiedDate", "DateTime", false, false, "datetime-local", "", "", "", false, "")
        }),
    new ApiResource(
        "CustomerAddress",
        "CustomerAddress",
        "api/customerAddress",
        "CustomerID",
        "Soft",
        true,
        true,
        false,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("CustomerID", "int", true, false, "number", "", "", "", false, ""),
            new ApiField("AddressID", "int", true, false, "number", "", "", "", false, ""),
            new ApiField("AddressType", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("Rowguid", "Guid", false, false, "text", "", "", "", false, ""),
            new ApiField("ModifiedDate", "DateTime", false, false, "datetime-local", "", "", "", false, "")
        }),
    new ApiResource(
        "ErrorLog",
        "ErrorLog",
        "api/errorLogs",
        "ErrorLogID",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("ErrorLogID", "int", true, false, "number", "", "", "", false, ""),
            new ApiField("ErrorTime", "DateTime", false, false, "datetime-local", "", "", "", false, ""),
            new ApiField("UserName", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("ErrorNumber", "int", false, false, "number", "", "", "", false, ""),
            new ApiField("ErrorSeverity", "int", false, true, "number", "", "", "", false, ""),
            new ApiField("ErrorState", "int", false, true, "number", "", "", "", false, ""),
            new ApiField("ErrorProcedure", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("ErrorLine", "int", false, true, "number", "", "", "", false, ""),
            new ApiField("ErrorMessage", "string", false, false, "text", "", "", "", false, "")
        }),
    new ApiResource(
        "Product",
        "Product",
        "api/products",
        "ProductID",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("ProductID", "int", true, false, "number", "", "", "", false, ""),
            new ApiField("Name", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("ProductNumber", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("Color", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("StandardCost", "decimal", false, false, "number", "", "", "", false, ""),
            new ApiField("ListPrice", "decimal", false, false, "number", "", "", "", false, ""),
            new ApiField("Size", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("Weight", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("ProductCategoryID", "int", false, true, "number", "", "", "", false, ""),
            new ApiField("ProductModelID", "int", false, true, "number", "", "", "", false, ""),
            new ApiField("SellStartDate", "DateTime", false, false, "datetime-local", "", "", "", false, ""),
            new ApiField("SellEndDate", "DateTime", false, true, "datetime-local", "", "", "", false, ""),
            new ApiField("DiscontinuedDate", "DateTime", false, true, "datetime-local", "", "", "", false, ""),
            new ApiField("ThumbNailPhoto", "byte[]", false, true, "text", "", "", "", false, ""),
            new ApiField("ThumbnailPhotoFileName", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("Rowguid", "Guid", false, false, "text", "", "", "", false, ""),
            new ApiField("ModifiedDate", "DateTime", false, false, "datetime-local", "", "", "", false, "")
        }),
    new ApiResource(
        "ProductCategory",
        "ProductCategory",
        "api/productCategories",
        "ProductCategoryID",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("ProductCategoryID", "int", true, false, "number", "", "", "", false, ""),
            new ApiField("ParentProductCategoryID", "int", false, true, "number", "", "", "", false, ""),
            new ApiField("Name", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("Rowguid", "Guid", false, false, "text", "", "", "", false, ""),
            new ApiField("ModifiedDate", "DateTime", false, false, "datetime-local", "", "", "", false, "")
        }),
    new ApiResource(
        "ProductDescription",
        "ProductDescription",
        "api/productDescriptions",
        "ProductDescriptionID",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("ProductDescriptionID", "int", true, false, "number", "", "", "", false, ""),
            new ApiField("Description", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("Rowguid", "Guid", false, false, "text", "", "", "", false, ""),
            new ApiField("ModifiedDate", "DateTime", false, false, "datetime-local", "", "", "", false, "")
        }),
    new ApiResource(
        "ProductModel",
        "ProductModel",
        "api/productModels",
        "ProductModelID",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("ProductModelID", "int", true, false, "number", "", "", "", false, ""),
            new ApiField("Name", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("CatalogDescription", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("Rowguid", "Guid", false, false, "text", "", "", "", false, ""),
            new ApiField("ModifiedDate", "DateTime", false, false, "datetime-local", "", "", "", false, "")
        }),
    new ApiResource(
        "ProductModelProductDescription",
        "ProductModelProductDescription",
        "api/productModelProductDescriptions",
        "ProductModelID",
        "Soft",
        true,
        true,
        false,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("ProductModelID", "int", true, false, "number", "", "", "", false, ""),
            new ApiField("ProductDescriptionID", "int", true, false, "number", "", "", "", false, ""),
            new ApiField("Culture", "string", true, false, "text", "", "", "", false, ""),
            new ApiField("Rowguid", "Guid", false, false, "text", "", "", "", false, ""),
            new ApiField("ModifiedDate", "DateTime", false, false, "datetime-local", "", "", "", false, "")
        }),
    new ApiResource(
        "SalesOrderDetail",
        "SalesOrderDetail",
        "api/salesOrderDetails",
        "SalesOrderID",
        "Soft",
        true,
        true,
        false,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("SalesOrderID", "int", true, false, "number", "", "", "", false, ""),
            new ApiField("SalesOrderDetailID", "int", true, false, "number", "", "", "", false, ""),
            new ApiField("OrderQty", "short", false, false, "number", "", "", "", false, ""),
            new ApiField("ProductID", "int", false, false, "number", "", "", "", false, ""),
            new ApiField("UnitPrice", "decimal", false, false, "number", "", "", "", false, ""),
            new ApiField("UnitPriceDiscount", "decimal", false, false, "number", "", "", "", false, ""),
            new ApiField("LineTotal", "decimal", false, false, "number", "", "", "", false, ""),
            new ApiField("Rowguid", "Guid", false, false, "text", "", "", "", false, ""),
            new ApiField("ModifiedDate", "DateTime", false, false, "datetime-local", "", "", "", false, "")
        }),
    new ApiResource(
        "SalesOrderHeader",
        "SalesOrderHeader",
        "api/salesOrderHeaders",
        "SalesOrderID",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("SalesOrderID", "int", true, false, "number", "", "", "", false, ""),
            new ApiField("RevisionNumber", "byte", false, false, "number", "", "", "", false, ""),
            new ApiField("OrderDate", "DateTime", false, false, "datetime-local", "", "", "", false, ""),
            new ApiField("DueDate", "DateTime", false, false, "datetime-local", "", "", "", false, ""),
            new ApiField("ShipDate", "DateTime", false, true, "datetime-local", "", "", "", false, ""),
            new ApiField("Status", "byte", false, false, "number", "", "", "", false, ""),
            new ApiField("OnlineOrderFlag", "bool", false, false, "checkbox", "", "", "", false, ""),
            new ApiField("SalesOrderNumber", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("PurchaseOrderNumber", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("AccountNumber", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("CustomerID", "int", false, false, "number", "", "", "", false, ""),
            new ApiField("ShipToAddressID", "int", false, true, "number", "", "", "", false, ""),
            new ApiField("BillToAddressID", "int", false, true, "number", "", "", "", false, ""),
            new ApiField("ShipMethod", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("CreditCardApprovalCode", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("SubTotal", "decimal", false, false, "number", "", "", "", false, ""),
            new ApiField("TaxAmt", "decimal", false, false, "number", "", "", "", false, ""),
            new ApiField("Freight", "decimal", false, false, "number", "", "", "", false, ""),
            new ApiField("TotalDue", "decimal", false, false, "number", "", "", "", false, ""),
            new ApiField("Comment", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("Rowguid", "Guid", false, false, "text", "", "", "", false, ""),
            new ApiField("ModifiedDate", "DateTime", false, false, "datetime-local", "", "", "", false, "")
        })
    };
    public static IReadOnlyList<ApiResource> WorkflowResources { get; } = Resources.Where(resource => resource.HasWorkflow).ToList();
    public static bool HasWorkflowResources => WorkflowResources.Count > 0;

    public static ApiResource? Find(string key) =>
        Resources.FirstOrDefault(resource => string.Equals(resource.Key, key, StringComparison.OrdinalIgnoreCase));
}