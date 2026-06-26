using MiningFleetOps.Client.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MiningFleetOps.Client.Pages;

public sealed partial class LoginModel : PageModel
{
    private readonly ApiSession _api;

    public LoginModel(ApiSession api)
    {
        _api = api;
    }

    [BindProperty]
    public string UserName { get; set; } = "";

    [BindProperty]
    public string Password { get; set; } = "";

    public string Message { get; private set; } = "";
    public string ResponseBody { get; private set; } = "";
    public string ErrorMessage { get; private set; } = "";
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public async Task OnPostAsync(CancellationToken ct)
    {
        var result = await _api.LoginAsync(UserName, Password, ct);
        Message = result.Success ? "Signed in." : "Login failed.";
        ResponseBody = result.Body;
        ErrorMessage = result.UserMessage;
    }

    public async Task<IActionResult> OnPostLogoutAsync(CancellationToken ct)
    {
        await _api.LogoutAsync(ct);
        return RedirectToPage("/Index");
    }
}