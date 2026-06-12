using AdventureWorksLT2017Api.Client.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdventureWorksLT2017Api.Client.Pages;

public sealed partial class RegisterModel : PageModel
{
    private readonly ApiSession _api;

    public RegisterModel(ApiSession api)
    {
        _api = api;
    }

    [BindProperty]
    public string UserName { get; set; } = "";
    [BindProperty]
    public string Password { get; set; } = "";
    [BindProperty]
    public string TenantId { get; set; } = "";
    [BindProperty]
    public string Role { get; set; } = "User";

    public string ResponseBody { get; private set; } = "";
    public string ErrorMessage { get; private set; } = "";
    public int LastStatusCode { get; private set; }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public async Task OnPostAsync(CancellationToken ct)
    {
        object? tenantId = string.IsNullOrWhiteSpace(TenantId) ? null : TenantId;
        var body = new { userName = UserName, password = Password, tenantId, role = Role };
        var result = await _api.SendAsync(HttpMethod.Post, "api/auth/users", body, ct);
        LastStatusCode = result.StatusCode;
        ResponseBody = result.Body;
        ErrorMessage = result.UserMessage;
    }
}