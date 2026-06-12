using AdventureWorksLT2017Api.Client.Models;
using AdventureWorksLT2017Api.Client.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace AdventureWorksLT2017Api.Client.Pages;

public sealed partial class UserManagementModel : PageModel
{
    private readonly ApiSession _api;

    public UserManagementModel(ApiSession api)
    {
        _api = api;
    }

    [BindProperty] public string UserName { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty] public string TenantId { get; set; } = "";
    [BindProperty] public string Role { get; set; } = "User";
    [BindProperty] public string TargetUserId { get; set; } = "";
    [BindProperty] public string TargetRoleId { get; set; } = "";
    [BindProperty] public string RoleName { get; set; } = "";
    [BindProperty] public string PermissionName { get; set; } = "";
    [BindProperty] public string Roles { get; set; } = "";
    [BindProperty] public string Permissions { get; set; } = "";
    [BindProperty] public List<string> SelectedRoles { get; set; } = new();
    [BindProperty] public List<string> SelectedPermissions { get; set; } = new();
    [BindProperty] public List<string> SelectedRolePermissions { get; set; } = new();

    public string UsersJson { get; private set; } = "";
    public string RolesJson { get; private set; } = "";
    public string PermissionsJson { get; private set; } = "";
    public IReadOnlyList<SelectOption> TenantOptions { get; private set; } = Array.Empty<SelectOption>();
    public IReadOnlyList<UserOption> UserOptions { get; private set; } = Array.Empty<UserOption>();
    public IReadOnlyList<NamedOption> RoleOptions { get; private set; } = Array.Empty<NamedOption>();
    public IReadOnlyList<NamedOption> PermissionOptions { get; private set; } = Array.Empty<NamedOption>();
    public string ResponseBody { get; private set; } = "";
    public string ErrorMessage { get; private set; } = "";
    public int LastStatusCode { get; private set; }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool ImpersonationEnabled => false;
    public bool IsImpersonating => _api.IsImpersonating;
    public string CurrentUserName => _api.UserName ?? "";
    public string OriginalUserName => _api.OriginalUserName ?? "";

    public async Task OnGetAsync(CancellationToken ct) => await LoadAllAsync(ct);
    public async Task OnPostLoadAsync(CancellationToken ct) => await LoadAllAsync(ct);
    public async Task OnPostCreateUserAsync(CancellationToken ct)
    {
        var body = new Dictionary<string, object?> { ["userName"] = UserName, ["password"] = Password, ["role"] = string.IsNullOrWhiteSpace(Role) ? "User" : Role.Trim() };
        if (!string.IsNullOrWhiteSpace(TenantId))
            body["tenantId"] = TenantId.Trim();
        await SendAsync(HttpMethod.Post, "api/auth/users", body, ct);
    }

    public async Task OnPostCreateRoleAsync(CancellationToken ct) => await SendAsync(HttpMethod.Post, "api/auth/roles", new { name = RoleName }, ct);
    public async Task OnPostCreatePermissionAsync(CancellationToken ct) => await SendAsync(HttpMethod.Post, "api/auth/permissions", new { name = PermissionName }, ct);
    public async Task OnPostSetUserRolesAsync(CancellationToken ct) => await SendAsync(HttpMethod.Post, "api/auth/users/" + Uri.EscapeDataString(TargetUserId) + "/roles", new { roles = SelectedRoles }, ct);
    public async Task OnPostSetUserPermissionsAsync(CancellationToken ct) => await SendAsync(HttpMethod.Post, "api/auth/users/" + Uri.EscapeDataString(TargetUserId) + "/permissions", new { permissions = SelectedPermissions }, ct);
    public async Task OnPostSetRolePermissionsAsync(CancellationToken ct) => await SendAsync(HttpMethod.Post, "api/auth/roles/" + Uri.EscapeDataString(TargetRoleId) + "/permissions", new { permissions = SelectedRolePermissions }, ct);
    public async Task OnPostEnableUserAsync(CancellationToken ct) => await SendAsync(HttpMethod.Post, "api/auth/users/" + Uri.EscapeDataString(TargetUserId) + "/enable", null, ct);
    public async Task OnPostDisableUserAsync(CancellationToken ct) => await SendAsync(HttpMethod.Post, "api/auth/users/" + Uri.EscapeDataString(TargetUserId) + "/disable", null, ct);
    public async Task OnPostImpersonateUserAsync(CancellationToken ct)
    {
        if (!ImpersonationEnabled)
        {
            LastStatusCode = 404;
            ErrorMessage = "User impersonation is not enabled for this generated client.";
            await LoadAllAsync(ct);
            return;
        }

        var result = await _api.ImpersonateAsync(TargetUserId, ct);
        LastStatusCode = result.StatusCode;
        ResponseBody = result.Body;
        ErrorMessage = result.UserMessage;
        await LoadAllAsync(ct);
    }

    public async Task OnPostStopImpersonatingAsync(CancellationToken ct)
    {
        _api.StopImpersonating();
        LastStatusCode = 200;
        ResponseBody = "Returned to the original admin session.";
        await LoadAllAsync(ct);
    }

    private async Task SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var result = await _api.SendAsync(method, path, body, ct);
        LastStatusCode = result.StatusCode;
        ResponseBody = result.Body;
        ErrorMessage = result.UserMessage;
        await LoadAllAsync(ct);
    }

    private async Task LoadAllAsync(CancellationToken ct)
    {
        UsersJson = await LoadJsonAsync("api/auth/users", ct);
        RolesJson = await LoadJsonAsync("api/auth/roles", ct);
        PermissionsJson = ApiCatalog.HasPermissionManagement ? await LoadJsonAsync("api/auth/permissions", ct) : "";
        var tenantsJson = await LoadJsonAsync("api/auth/tenants", ct);
        UserOptions = BuildUserOptions(UsersJson);
        RoleOptions = BuildNamedOptions(RolesJson);
        PermissionOptions = ApiCatalog.HasPermissionManagement ? BuildNamedOptions(PermissionsJson) : Array.Empty<NamedOption>();
        TenantOptions = BuildTenantOptions(tenantsJson);
        if (TenantOptions.Count == 0 && !string.IsNullOrWhiteSpace(_api.TenantId))
            TenantOptions = new[] { new SelectOption(_api.TenantId, "Current tenant (" + _api.TenantId + ")") };
        if (string.IsNullOrWhiteSpace(TenantId) && TenantOptions.Count == 1)
            TenantId = TenantOptions[0].Value;
        if ((string.IsNullOrWhiteSpace(Role) || Role == "User") && RoleOptions.Count > 0)
            Role = RoleOptions.Any(role => string.Equals(role.Name, "User", StringComparison.OrdinalIgnoreCase)) ? "User" : RoleOptions[0].Name;
    }

    private async Task<string> LoadJsonAsync(string path, CancellationToken ct)
    {
        var result = await _api.SendAsync(HttpMethod.Get, path, null, ct);
        return result.Success ? result.Body : result.UserMessage + Environment.NewLine + result.Body;
    }

    private static IReadOnlyList<string> SplitCsv(string value) =>
        (value ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static IReadOnlyList<SelectOption> BuildTenantOptions(string json) =>
        ReadArray(json)
            .Select(item => new SelectOption(ReadString(item, "id"), ReadString(item, "name") + " (" + ReadString(item, "id") + ")"))
            .Where(option => !string.IsNullOrWhiteSpace(option.Value))
            .ToList();

    private static IReadOnlyList<UserOption> BuildUserOptions(string json) =>
        ReadArray(json)
            .Select(item => new UserOption(
                ReadString(item, "id"),
                ReadString(item, "userName") + " - " + ReadString(item, "tenantName"),
                ReadString(item, "tenantId")))
            .Where(option => !string.IsNullOrWhiteSpace(option.Id))
            .ToList();

    private static IReadOnlyList<NamedOption> BuildNamedOptions(string json) =>
        ReadArray(json)
            .Select(item => new NamedOption(ReadString(item, "id"), ReadString(item, "name")))
            .Where(option => !string.IsNullOrWhiteSpace(option.Id) && !string.IsNullOrWhiteSpace(option.Name))
            .ToList();

    private static IEnumerable<JsonElement> ReadArray(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("[", StringComparison.Ordinal))
            yield break;

        using var doc = JsonDocument.Parse(json);
        foreach (var item in doc.RootElement.EnumerateArray())
            yield return item.Clone();
    }

    private static string ReadString(JsonElement item, string name)
    {
        foreach (var property in item.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                return property.Value.ToString() ?? "";
        }

        return "";
    }

    public sealed record SelectOption(string Value, string Label);
    public sealed record UserOption(string Id, string Label, string TenantId);
    public sealed record NamedOption(string Id, string Name);
}