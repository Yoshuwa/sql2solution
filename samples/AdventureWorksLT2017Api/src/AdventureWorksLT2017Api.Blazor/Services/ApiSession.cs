using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AdventureWorksLT2017Api.Blazor.Models;
using Microsoft.JSInterop;

namespace AdventureWorksLT2017Api.Blazor.Services;

public sealed partial class ApiSession
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IJSRuntime _js;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private const string StorageKey = "generatedApiClient.auth";
    private bool _restoreAttempted;
    private string? _accessToken;
    private string? _refreshToken;
    private string? _userName;
    private string? _userId;
    private string? _tenantId;
    private DateTimeOffset? _tokenIssuedAtUtc;
    private DateTimeOffset? _tokenExpiresAtUtc;
    private DateTimeOffset? _refreshTokenStoredAtUtc;
    private string? _originalAccessToken;
    private string? _originalRefreshToken;
    private string? _originalUserName;
    private string? _originalUserId;
    private string? _originalTenantId;
    private DateTimeOffset? _originalTokenIssuedAtUtc;
    private DateTimeOffset? _originalTokenExpiresAtUtc;
    private DateTimeOffset? _originalRefreshTokenStoredAtUtc;
    private IReadOnlyList<string> _originalRoles = Array.Empty<string>();
    private IReadOnlyList<string> _originalPermissions = Array.Empty<string>();
    private IReadOnlyList<string> _roles = Array.Empty<string>();
    private IReadOnlyList<string> _permissions = Array.Empty<string>();

    public ApiSession(IHttpClientFactory httpClientFactory, IJSRuntime js)
    {
        _httpClientFactory = httpClientFactory;
        _js = js;
    }

    public string? AccessToken => _accessToken;
    public Uri? ApiBaseAddress => _httpClientFactory.CreateClient("Api").BaseAddress;
    public string? RefreshToken => _refreshToken;
    public string? UserName => _userName;
    public string? UserId => _userId;
    public string? TenantId => _tenantId;
    public DateTimeOffset? TokenIssuedAtUtc => _tokenIssuedAtUtc;
    public DateTimeOffset? TokenExpiresAtUtc => _tokenExpiresAtUtc;
    public DateTimeOffset? RefreshTokenStoredAtUtc => _refreshTokenStoredAtUtc;
    public string TokenIssuedDisplay => FormatDateTime(_tokenIssuedAtUtc);
    public string TokenExpiresDisplay => FormatDateTime(_tokenExpiresAtUtc);
    public string RefreshTokenStoredDisplay => FormatDateTime(_refreshTokenStoredAtUtc);
    public string TokenIssuedRelativeDisplay => FormatRelativeTime(_tokenIssuedAtUtc);
    public string TokenExpiresRelativeDisplay => FormatRelativeTime(_tokenExpiresAtUtc);
    public string RefreshTokenStoredRelativeDisplay => FormatRelativeTime(_refreshTokenStoredAtUtc);
    public IReadOnlyList<string> Roles => _roles;
    public IReadOnlyList<string> Permissions => _permissions;
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(_accessToken);
    public bool IsReady => !ApiCatalog.HasAuthentication || _restoreAttempted || IsAuthenticated;
    public bool IsImpersonating => !string.IsNullOrWhiteSpace(_originalAccessToken);
    public string? OriginalUserName => _originalUserName;
    public event Action? StateChanged;

    public bool HasRole(string role) =>
        Roles.Contains(role, StringComparer.OrdinalIgnoreCase);

    public bool HasPermission(ApiResource resource, string permission)
    {
        if (!ApiCatalog.HasAuthentication || !ApiCatalog.HasPermissionManagement)
            return true;
        if (!IsAuthenticated)
            return false;
        if (HasRole("MasterAdmin"))
            return true;

        var candidates = ResourcePermissionCandidates(resource, permission).ToList();
        return Permissions.Any(granted => PermissionMatches(granted, permission, candidates));
    }

    private static IEnumerable<string> ResourcePermissionCandidates(ApiResource resource, string permission)
    {
        foreach (var name in ResourcePermissionNames(resource))
        {
            yield return name + ":" + permission;
            yield return name + "." + permission;
            yield return name + "/" + permission;
            yield return permission + ":" + name;
        }
    }

    private static IEnumerable<string> ResourcePermissionNames(ApiResource resource)
    {
        yield return resource.DisplayName;
        yield return resource.Key;
        yield return resource.Route.Trim('/');

        var route = resource.Route.Trim('/');
        if (route.StartsWith("api/", StringComparison.OrdinalIgnoreCase))
            yield return route[4..];

        var lastSlash = route.LastIndexOf('/');
        if (lastSlash >= 0 && lastSlash < route.Length - 1)
            yield return route[(lastSlash + 1)..];
    }

    private static bool PermissionMatches(string granted, string permission, IReadOnlyList<string> candidates)
    {
        if (string.IsNullOrWhiteSpace(granted))
            return false;

        return string.Equals(granted, "*", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(granted, permission, StringComparison.OrdinalIgnoreCase) ||
               candidates.Contains(granted, StringComparer.OrdinalIgnoreCase);
    }

    public bool CanRead(ApiResource resource) => HasPermission(resource, "read");
    public bool CanWrite(ApiResource resource) => HasPermission(resource, "write");
    public bool CanUpdate(ApiResource resource) => HasPermission(resource, "update");
    public bool CanDelete(ApiResource resource) => resource.CanDelete && HasPermission(resource, "delete");
    public bool CanManageUsers => !ApiCatalog.HasAuthentication || HasRole("MasterAdmin") || HasRole("TenantAdmin");
    public bool CanManageTenants => !ApiCatalog.HasAuthentication || HasRole("MasterAdmin");

    public async Task<bool> RestoreAsync()
    {
        if (IsAuthenticated)
        {
            _restoreAttempted = true;
            return true;
        }

        if (_restoreAttempted)
            return IsAuthenticated;

        _restoreAttempted = true;
        try
        {
            var stored = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (string.IsNullOrWhiteSpace(stored))
                return false;

            var state = JsonSerializer.Deserialize<AuthState>(stored, JsonOptions);
            if (state is null || string.IsNullOrWhiteSpace(state.AccessToken))
                return false;

            _accessToken = state.AccessToken;
            _refreshToken = state.RefreshToken;
            _userName = state.UserName;
            _userId = state.UserId;
            _tenantId = state.TenantId;
            _tokenIssuedAtUtc = state.TokenIssuedAtUtc;
            _tokenExpiresAtUtc = state.TokenExpiresAtUtc;
            _refreshTokenStoredAtUtc = state.RefreshTokenStoredAtUtc;
            _originalAccessToken = state.OriginalAccessToken;
            _originalRefreshToken = state.OriginalRefreshToken;
            _originalUserName = state.OriginalUserName;
            _originalUserId = state.OriginalUserId;
            _originalTenantId = state.OriginalTenantId;
            _originalTokenIssuedAtUtc = state.OriginalTokenIssuedAtUtc;
            _originalTokenExpiresAtUtc = state.OriginalTokenExpiresAtUtc;
            _originalRefreshTokenStoredAtUtc = state.OriginalRefreshTokenStoredAtUtc;
            _roles = state.Roles ?? Array.Empty<string>();
            _permissions = state.Permissions ?? Array.Empty<string>();
            _originalRoles = state.OriginalRoles ?? Array.Empty<string>();
            _originalPermissions = state.OriginalPermissions ?? Array.Empty<string>();
            StoreTokenMetadata(_accessToken);
            NotifyStateChanged();
            return true;
        }
        catch (InvalidOperationException)
        {
            _restoreAttempted = false;
            return false;
        }
        catch
        {
            await ClearPersistedAuthAsync();
            return false;
        }
    }

    public async Task<ApiResult> SendAsync(HttpMethod method, string path, object? body = null, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("Api");
        var requestBody = body is null ? "" : JsonSerializer.Serialize(body, JsonOptions);
        var startedAt = DateTimeOffset.UtcNow;
        try
        {
            using var response = await SendHttpAsync(client, method, path, requestBody, body is not null, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized && CanRefresh(path) && ShouldRefreshAccessToken() && await RefreshAccessTokenAsync(client, ct))
            {
                using var retry = await SendHttpAsync(client, method, path, requestBody, body is not null, ct);
                return await BuildResultAsync(method, client.BaseAddress, path, requestBody, retry, ElapsedMilliseconds(startedAt), ct);
            }

            return await BuildResultAsync(method, client.BaseAddress, path, requestBody, response, ElapsedMilliseconds(startedAt), ct);
        }
        catch (Exception ex)
        {
            return ApiResult.Failure(
                "The API request could not be completed. Check the API URL and whether the API is running.",
                BuildDiagnosticLog(method, client.BaseAddress, path, ex));
        }
    }

    public async Task<ApiResult> LoginAsync(string userName, string password, CancellationToken ct = default)
    {
        var result = await SendAsync(HttpMethod.Post, "api/auth/login", new { userName, password }, ct);
        if (result.Success)
        {
            StoreLoginPayload(result.Body, userName);
            _restoreAttempted = true;
            await PersistAuthAsync();
            NotifyStateChanged();
        }

        return result;
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        if (IsAuthenticated)
            await SendAsync(HttpMethod.Post, "api/auth/logout", new { refreshToken = _refreshToken }, ct);

        ClearAuth();
        _restoreAttempted = true;
        await ClearPersistedAuthAsync();
        NotifyStateChanged();
    }

    public async Task<ApiResult> ImpersonateAsync(string userId, CancellationToken ct = default)
    {
        if (!HasRole("MasterAdmin"))
            return ApiResult.Failure("Only MasterAdmin users can impersonate another user.", "");
        if (string.IsNullOrWhiteSpace(userId))
            return ApiResult.Failure("Choose a user to impersonate.", "");
        if (string.Equals(userId, _userId, StringComparison.OrdinalIgnoreCase))
            return ApiResult.Failure("You are already signed in as this user.", "");

        var result = await SendAsync(HttpMethod.Post, "api/auth/users/" + Uri.EscapeDataString(userId) + "/impersonate", null, ct);
        if (result.Success)
        {
            StoreImpersonationPayload(result.Body);
            _restoreAttempted = true;
            await PersistAuthAsync();
            NotifyStateChanged();
        }

        return result;
    }

    public async Task StopImpersonatingAsync()
    {
        if (!IsImpersonating)
            return;

        _accessToken = _originalAccessToken;
        _refreshToken = _originalRefreshToken;
        _userName = _originalUserName;
        _userId = _originalUserId;
        _tenantId = _originalTenantId;
        _tokenIssuedAtUtc = _originalTokenIssuedAtUtc;
        _tokenExpiresAtUtc = _originalTokenExpiresAtUtc;
        _refreshTokenStoredAtUtc = _originalRefreshTokenStoredAtUtc;
        _roles = _originalRoles;
        _permissions = _originalPermissions;
        ClearOriginalAuth();
        _restoreAttempted = true;
        await PersistAuthAsync();
        NotifyStateChanged();
    }

    private async Task<HttpResponseMessage> SendHttpAsync(HttpClient client, HttpMethod method, string path, string requestBody, bool hasBody, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path.TrimStart('/'));
        if (!string.IsNullOrWhiteSpace(_accessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        if (hasBody)
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        return await client.SendAsync(request, ct);
    }

    private async Task<ApiResult> BuildResultAsync(HttpMethod method, Uri? baseAddress, string path, string requestBody, HttpResponseMessage response, long elapsedMilliseconds, CancellationToken ct)
    {
        var content = await response.Content.ReadAsStringAsync(ct);
        var responseBody = PrettyJson(content);
        var endpointMessage = ExtractApiMessage(content);
        var message = response.IsSuccessStatusCode
            ? ""
            : !string.IsNullOrWhiteSpace(endpointMessage)
                ? endpointMessage
                : response.StatusCode == HttpStatusCode.Unauthorized
                ? "Your sign-in session has expired or is no longer accepted by the API. Sign in again and retry."
                : $"The API returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}). Check the logs below for details.";
        var resultBody = response.IsSuccessStatusCode
            ? responseBody
            : BuildHttpErrorLog(method, baseAddress, path, requestBody, (int)response.StatusCode, response.ReasonPhrase, responseBody);

        return new ApiResult((int)response.StatusCode, response.IsSuccessStatusCode, resultBody, message, elapsedMilliseconds);
    }

    private static long ElapsedMilliseconds(DateTimeOffset startedAt) =>
        Math.Max(0, (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds);

    private bool ShouldRefreshAccessToken()
    {
        if (IsImpersonating || string.IsNullOrWhiteSpace(_refreshToken))
            return false;

        return _tokenExpiresAtUtc is null || _tokenExpiresAtUtc <= DateTimeOffset.UtcNow.AddMinutes(1);
    }

    private async Task<bool> RefreshAccessTokenAsync(HttpClient client, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_refreshToken))
            return false;

        var requestBody = JsonSerializer.Serialize(new { refreshToken = _refreshToken }, JsonOptions);
        using var response = await SendHttpAsync(client, HttpMethod.Post, "api/auth/refresh", requestBody, hasBody: true, ct);
        if (!response.IsSuccessStatusCode)
        {
            ClearAuth();
            await ClearPersistedAuthAsync();
            NotifyStateChanged();
            return false;
        }

        StoreLoginPayload(await response.Content.ReadAsStringAsync(ct), _userName ?? "");
        await PersistAuthAsync();
        NotifyStateChanged();
        return IsAuthenticated;
    }

    private static bool CanRefresh(string path)
    {
        var normalized = path.TrimStart('/');
        return !normalized.StartsWith("api/auth/login", StringComparison.OrdinalIgnoreCase) &&
               !normalized.StartsWith("api/auth/refresh", StringComparison.OrdinalIgnoreCase) &&
               !normalized.StartsWith("api/auth/logout", StringComparison.OrdinalIgnoreCase);
    }

    private void StoreLoginPayload(string body, string fallbackUserName)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        if (doc.RootElement.TryGetProperty("accessToken", out var accessToken))
        {
            _accessToken = accessToken.GetString() ?? "";
            StoreTokenMetadata(_accessToken);
        }
        if (doc.RootElement.TryGetProperty("refreshToken", out var refreshToken))
        {
            var nextRefreshToken = refreshToken.ValueKind == JsonValueKind.Null ? null : refreshToken.GetString();
            if (!string.Equals(_refreshToken, nextRefreshToken, StringComparison.Ordinal))
                _refreshTokenStoredAtUtc = string.IsNullOrWhiteSpace(nextRefreshToken) ? null : DateTimeOffset.UtcNow;
            _refreshToken = nextRefreshToken;
            if (!string.IsNullOrWhiteSpace(_refreshToken) && _refreshTokenStoredAtUtc is null)
                _refreshTokenStoredAtUtc = DateTimeOffset.UtcNow;
        }
        if (doc.RootElement.TryGetProperty("userName", out var signedInUserName))
            _userName = signedInUserName.GetString() ?? fallbackUserName;
        else if (string.IsNullOrWhiteSpace(_userName))
            _userName = fallbackUserName;
        if (doc.RootElement.TryGetProperty("tenantId", out var tenantId) && tenantId.ValueKind != JsonValueKind.Null)
            _tenantId = tenantId.ToString();
        if (doc.RootElement.TryGetProperty("userId", out var userId) && userId.ValueKind != JsonValueKind.Null)
            _userId = userId.ToString();
        if (doc.RootElement.TryGetProperty("roles", out var roles))
            _roles = ReadStringArray(roles);
        if (doc.RootElement.TryGetProperty("permissions", out var permissions))
            _permissions = ReadStringArray(permissions);
    }

    private void StoreImpersonationPayload(string body)
    {
        if (!IsImpersonating)
            CaptureOriginalAuth();

        StoreLoginPayload(body, _userName ?? "");
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        if (doc.RootElement.TryGetProperty("impersonatedByUserName", out var impersonatedByUserName) &&
            impersonatedByUserName.ValueKind != JsonValueKind.Null)
        {
            _originalUserName = impersonatedByUserName.GetString() ?? _originalUserName;
        }
    }

    private void CaptureOriginalAuth()
    {
        _originalAccessToken = _accessToken;
        _originalRefreshToken = _refreshToken;
        _originalUserName = _userName;
        _originalUserId = _userId;
        _originalTenantId = _tenantId;
        _originalTokenIssuedAtUtc = _tokenIssuedAtUtc;
        _originalTokenExpiresAtUtc = _tokenExpiresAtUtc;
        _originalRefreshTokenStoredAtUtc = _refreshTokenStoredAtUtc;
        _originalRoles = _roles.ToArray();
        _originalPermissions = _permissions.ToArray();
    }

    private void ClearOriginalAuth()
    {
        _originalAccessToken = null;
        _originalRefreshToken = null;
        _originalUserName = null;
        _originalUserId = null;
        _originalTenantId = null;
        _originalTokenIssuedAtUtc = null;
        _originalTokenExpiresAtUtc = null;
        _originalRefreshTokenStoredAtUtc = null;
        _originalRoles = Array.Empty<string>();
        _originalPermissions = Array.Empty<string>();
    }

    private void ClearAuth()
    {
        _accessToken = null;
        _refreshToken = null;
        _userName = null;
        _userId = null;
        _tenantId = null;
        _tokenIssuedAtUtc = null;
        _tokenExpiresAtUtc = null;
        _refreshTokenStoredAtUtc = null;
        ClearOriginalAuth();
        _roles = Array.Empty<string>();
        _permissions = Array.Empty<string>();
    }

    private async Task PersistAuthAsync()
    {
        if (string.IsNullOrWhiteSpace(_accessToken))
            return;

        var state = new AuthState(_accessToken, _refreshToken, _userName, _userId, _tenantId, _tokenIssuedAtUtc, _tokenExpiresAtUtc, _refreshTokenStoredAtUtc, _originalAccessToken, _originalRefreshToken, _originalUserName, _originalUserId, _originalTenantId, _originalTokenIssuedAtUtc, _originalTokenExpiresAtUtc, _originalRefreshTokenStoredAtUtc, _roles, _permissions, _originalRoles, _originalPermissions);
        await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, JsonSerializer.Serialize(state, JsonOptions));
    }

    private async Task ClearPersistedAuthAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        }
        catch
        {
        }
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        return element.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? "")
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();

    private void StoreTokenMetadata(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;

        try
        {
            var parts = token.Split('.');
            if (parts.Length < 2)
                return;

            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Base64UrlDecode(parts[1])));
            if (TryGetClaim(doc.RootElement, out var userId, "sub", "nameid", "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"))
                _userId = userId;
            if (TryGetClaim(doc.RootElement, out var tenantId, "tenantId", "tenant_id", "tenantid"))
                _tenantId = tenantId;
            if (TryGetClaim(doc.RootElement, out var userName, "unique_name", "name", "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"))
                _userName = userName;
            _tokenIssuedAtUtc = ReadUnixTimeClaim(doc.RootElement, "iat", "nbf");
            _tokenExpiresAtUtc = ReadUnixTimeClaim(doc.RootElement, "exp");
            _roles = MergeClaims(_roles, ReadClaimValues(doc.RootElement, "role", "roles", "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"));
            _permissions = MergeClaims(_permissions, ReadClaimValues(doc.RootElement, "permission", "permissions"));
        }
        catch
        {
        }
    }

    private static IReadOnlyList<string> ReadClaimValues(JsonElement payload, params string[] names)
    {
        var values = new List<string>();
        foreach (var property in payload.EnumerateObject())
        {
            if (!names.Any(name => string.Equals(name, property.Name, StringComparison.OrdinalIgnoreCase)))
                continue;

            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                values.AddRange(property.Value.EnumerateArray()
                    .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? "" : item.ToString()));
            }
            else if (property.Value.ValueKind != JsonValueKind.Null)
            {
                values.Add(property.Value.ToString());
            }
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> MergeClaims(IReadOnlyList<string> existing, IReadOnlyList<string> discovered) =>
        existing.Concat(discovered)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static bool TryGetClaim(JsonElement payload, out string value, params string[] names)
    {
        foreach (var name in names)
        {
            if (payload.TryGetProperty(name, out var claim) && claim.ValueKind != JsonValueKind.Null)
            {
                value = claim.ToString();
                return !string.IsNullOrWhiteSpace(value);
            }
        }

        foreach (var property in payload.EnumerateObject())
        {
            if (names.Any(name => string.Equals(name, property.Name, StringComparison.OrdinalIgnoreCase)) &&
                property.Value.ValueKind != JsonValueKind.Null)
            {
                value = property.Value.ToString();
                return !string.IsNullOrWhiteSpace(value);
            }
        }

        value = "";
        return false;
    }

    private static DateTimeOffset? ReadUnixTimeClaim(JsonElement payload, params string[] names)
    {
        foreach (var name in names)
        {
            if (!payload.TryGetProperty(name, out var claim) || claim.ValueKind == JsonValueKind.Null)
                continue;

            if (claim.ValueKind == JsonValueKind.Number && claim.TryGetInt64(out var seconds))
                return DateTimeOffset.FromUnixTimeSeconds(seconds);
            if (claim.ValueKind == JsonValueKind.String && long.TryParse(claim.GetString(), out seconds))
                return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }

        return null;
    }

    private static string FormatDateTime(DateTimeOffset? value) =>
        value is null ? "" : value.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    private static string FormatRelativeTime(DateTimeOffset? value)
    {
        if (value is null)
            return "";

        var difference = value.Value.ToLocalTime() - DateTimeOffset.Now;
        var isFuture = difference >= TimeSpan.Zero;
        var duration = difference.Duration();
        var parts = new List<string>();

        if (duration.TotalDays >= 1)
            parts.Add($"{(int)duration.TotalDays} d");
        if (duration.Hours > 0)
            parts.Add($"{duration.Hours} hr");
        if (duration.Minutes > 0 && parts.Count < 2)
            parts.Add($"{duration.Minutes} min");
        if (parts.Count == 0)
            parts.Add("less than 1 min");

        var text = string.Join(" ", parts.Take(2));
        return isFuture ? $"in {text}" : $"{text} ago";
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }

    private static string PrettyJson(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return "";

        try
        {
            using var doc = JsonDocument.Parse(content);
            return JsonSerializer.Serialize(doc.RootElement, JsonOptions);
        }
        catch
        {
            return content;
        }
    }

    private static string ExtractApiMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return "";

        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.String)
            {
                return message.GetString() ?? "";
            }
        }
        catch
        {
        }

        return "";
    }

    private static string BuildDiagnosticLog(HttpMethod method, Uri? baseAddress, string path, Exception ex)
    {
        var target = new Uri(baseAddress ?? new Uri("http://localhost/"), path.TrimStart('/'));
        var builder = new StringBuilder();
        builder.AppendLine("Request");
        builder.AppendLine($"  Method: {method}");
        builder.AppendLine($"  URL: {target}");
        builder.AppendLine();
        builder.AppendLine("Exception");
        builder.AppendLine($"  Type: {ex.GetType().FullName}");
        builder.AppendLine($"  Message: {ex.Message}");
        return builder.ToString();
    }

    private static string BuildHttpErrorLog(HttpMethod method, Uri? baseAddress, string path, string requestBody, int statusCode, string? reasonPhrase, string responseBody)
    {
        var target = new Uri(baseAddress ?? new Uri("http://localhost/"), path.TrimStart('/'));
        var builder = new StringBuilder();
        builder.AppendLine("Request");
        builder.AppendLine($"  Method: {method}");
        builder.AppendLine($"  URL: {target}");
        builder.AppendLine("  Body:");
        builder.AppendLine(string.IsNullOrWhiteSpace(requestBody) ? "    <empty>" : requestBody);
        builder.AppendLine();
        builder.AppendLine("Response");
        builder.AppendLine($"  HTTP {statusCode} {reasonPhrase}");
        builder.AppendLine("  Body:");
        builder.AppendLine(string.IsNullOrWhiteSpace(responseBody) ? "    <empty>" : responseBody);
        return builder.ToString();
    }
}

public sealed record AuthState(
    string? AccessToken,
    string? RefreshToken,
    string? UserName,
    string? UserId,
    string? TenantId,
    DateTimeOffset? TokenIssuedAtUtc,
    DateTimeOffset? TokenExpiresAtUtc,
    DateTimeOffset? RefreshTokenStoredAtUtc,
    string? OriginalAccessToken,
    string? OriginalRefreshToken,
    string? OriginalUserName,
    string? OriginalUserId,
    string? OriginalTenantId,
    DateTimeOffset? OriginalTokenIssuedAtUtc,
    DateTimeOffset? OriginalTokenExpiresAtUtc,
    DateTimeOffset? OriginalRefreshTokenStoredAtUtc,
    IReadOnlyList<string>? Roles,
    IReadOnlyList<string>? Permissions,
    IReadOnlyList<string>? OriginalRoles,
    IReadOnlyList<string>? OriginalPermissions);

public sealed record ApiResult(int StatusCode, bool Success, string Body, string UserMessage, long ElapsedMilliseconds)
{
    public static ApiResult Failure(string userMessage, string body) => new(0, false, body, userMessage, 0);
}