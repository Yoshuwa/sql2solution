using AdventureWorksLT2017Api.Client.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdventureWorksLT2017Api.Client.Pages;

public sealed partial class TenantsModel : PageModel
{
    private readonly ApiSession _api;

    public TenantsModel(ApiSession api)
    {
        _api = api;
    }

    [BindProperty]
    public string TenantName { get; set; } = "";

    public string ResponseBody { get; private set; } = "";
    public string ErrorMessage { get; private set; } = "";
    public int LastStatusCode { get; private set; }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public async Task OnPostLoadAsync(CancellationToken ct) => await SendAsync(HttpMethod.Get, "api/auth/tenants", null, ct);
    public async Task OnPostCreateAsync(CancellationToken ct) => await SendAsync(HttpMethod.Post, "api/auth/tenants", new { name = TenantName }, ct);
    public async Task OnPostEnableAsync(string tenantId, CancellationToken ct) => await SendAsync(HttpMethod.Post, $"api/auth/tenants/{Uri.EscapeDataString(tenantId ?? "")}/enable", null, ct);
    public async Task OnPostDisableAsync(string tenantId, CancellationToken ct) => await SendAsync(HttpMethod.Post, $"api/auth/tenants/{Uri.EscapeDataString(tenantId ?? "")}/disable", null, ct);

    private async Task SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var result = await _api.SendAsync(method, path, body, ct);
        LastStatusCode = result.StatusCode;
        ResponseBody = result.Body;
        ErrorMessage = result.UserMessage;
    }
}