using Microsoft.AspNetCore.DataProtection;
using MiningFleetOps.Client.Models;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MiningFleetOps.Client.Services;

public sealed partial class ApiSession
{
    private const string AccessTokenKey = "api.accessToken";
    private const string RefreshTokenKey = "api.refreshToken";
    private const string UserNameKey = "api.userName";
    private const string UserIdKey = "api.userId";
    private const string TenantIdKey = "api.tenantId";
    private const string TokenExpiresAtUtcKey = "api.tokenExpiresAtUtc";
    private const string RolesKey = "api.roles";
    private const string PermissionsKey = "api.permissions";
    private const string OriginalAccessTokenKey = "api.impersonation.originalAccessToken";
    private const string OriginalRefreshTokenKey = "api.impersonation.originalRefreshToken";
    private const string OriginalUserNameKey = "api.impersonation.originalUserName";
    private const string OriginalUserIdKey = "api.impersonation.originalUserId";
    private const string OriginalTenantIdKey = "api.impersonation.originalTenantId";
    private const string OriginalRolesKey = "api.impersonation.originalRoles";
    private const string OriginalPermissionsKey = "api.impersonation.originalPermissions";
    private static readonly string[] StoredKeys = { AccessTokenKey, RefreshTokenKey, UserNameKey, UserIdKey, TenantIdKey, TokenExpiresAtUtcKey, RolesKey, PermissionsKey, OriginalAccessTokenKey, OriginalRefreshTokenKey, OriginalUserNameKey, OriginalUserIdKey, OriginalTenantIdKey, OriginalRolesKey, OriginalPermissionsKey };
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDataProtector _protector;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public ApiSession(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor, IDataProtectionProvider dataProtectionProvider)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
        _protector = dataProtectionProvider.CreateProtector("GeneratedApiClient.ApiSession.PersistentAuth.v1");
    }

    public string? AccessToken => GetStoredString(AccessTokenKey);
    public string? RefreshToken => GetStoredString(RefreshTokenKey);
    public string? UserName => GetStoredString(UserNameKey);
    public string? UserId => GetStoredString(UserIdKey);
    public string? TenantId => GetStoredString(TenantIdKey);
    private DateTimeOffset? TokenExpiresAtUtc => DateTimeOffset.TryParse(GetStoredString(TokenExpiresAtUtcKey), out var value) ? value : null;
    public IReadOnlyList<string> Roles => GetStoredStringArray(RolesKey);
    public IReadOnlyList<string> Permissions => GetStoredStringArray(PermissionsKey);
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(AccessToken);
    public bool IsReady => true;
    public bool IsImpersonating => !string.IsNullOrWhiteSpace(GetStoredString(OriginalAccessTokenKey));
    public string? OriginalUserName => GetStoredString(OriginalUserNameKey);

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
                return await BuildApiResultAsync(method, client.BaseAddress, path, requestBody, retry, ElapsedMilliseconds(startedAt), ct);
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized && CanRefresh(path))
            {
                if (IsImpersonating)
                    StopImpersonating();
                else
                    ClearStoredAuth();
            }

            return await BuildApiResultAsync(method, client.BaseAddress, path, requestBody, response, ElapsedMilliseconds(startedAt), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            return ApiResult.Failure(
                "The API request timed out. Check that the API is running and try again.",
                BuildDiagnosticLog(method, client.BaseAddress, path, ex));
        }
        catch (HttpRequestException ex)
        {
            var message = ex.InnerException is SocketException
                ? "The API could not be reached. Make sure the API project is running and that Api:BaseUrl points to the correct address."
                : "The API request failed before a response was received. Check the logs below for details.";

            return ApiResult.Failure(message, BuildDiagnosticLog(method, client.BaseAddress, path, ex));
        }
    }

    private async Task<HttpResponseMessage> SendHttpAsync(HttpClient client, HttpMethod method, string path, string requestBody, bool hasBody, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, path.TrimStart('/'));
        if (!string.IsNullOrWhiteSpace(AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        if (hasBody)
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        return await client.SendAsync(request, ct);
    }

    private static async Task<ApiResult> BuildApiResultAsync(HttpMethod method, Uri? baseAddress, string path, string requestBody, HttpResponseMessage response, long elapsedMilliseconds, CancellationToken ct)
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
        if (IsImpersonating || string.IsNullOrWhiteSpace(RefreshToken))
            return false;

        return TokenExpiresAtUtc is null || TokenExpiresAtUtc <= DateTimeOffset.UtcNow.AddMinutes(1);
    }

    private async Task<bool> RefreshAccessTokenAsync(HttpClient client, CancellationToken ct)
    {
        if (IsImpersonating || string.IsNullOrWhiteSpace(RefreshToken))
            return false;

        var requestBody = JsonSerializer.Serialize(new { refreshToken = RefreshToken }, JsonOptions);
        using var response = await SendHttpAsync(client, HttpMethod.Post, "api/auth/refresh", requestBody, hasBody: true, ct);
        if (!response.IsSuccessStatusCode)
        {
            ClearStoredAuth();
            return false;
        }

        var content = await response.Content.ReadAsStringAsync(ct);
        StoreLoginPayload(content, fallbackUserName: UserName ?? "");
        return !string.IsNullOrWhiteSpace(AccessToken);
    }

    private static bool CanRefresh(string path)
    {
        var normalized = path.TrimStart('/');
        return !normalized.StartsWith("api/auth/login", StringComparison.OrdinalIgnoreCase) &&
               !normalized.StartsWith("api/auth/refresh", StringComparison.OrdinalIgnoreCase) &&
               !normalized.StartsWith("api/auth/logout", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ApiResult> LoginAsync(string userName, string password, CancellationToken ct = default)
    {
        var result = await SendAsync(HttpMethod.Post, "api/auth/login", new { userName, password }, ct);
        if (!result.Success)
            return result;

        StoreLoginPayload(result.Body, userName);
        return result;
    }

    public async Task<ApiResult> ImpersonateAsync(string userId, CancellationToken ct = default)
    {
        var result = await SendAsync(HttpMethod.Post, "api/auth/users/" + Uri.EscapeDataString(userId) + "/impersonate", null, ct);
        if (!result.Success)
            return result;

        StoreImpersonationPayload(result.Body);
        return result;
    }

    public void StopImpersonating()
    {
        RestoreStoredString(OriginalAccessTokenKey, AccessTokenKey);
        RestoreStoredString(OriginalRefreshTokenKey, RefreshTokenKey);
        RestoreStoredString(OriginalUserNameKey, UserNameKey);
        RestoreStoredString(OriginalUserIdKey, UserIdKey);
        RestoreStoredString(OriginalTenantIdKey, TenantIdKey);
        RestoreStoredString(OriginalRolesKey, RolesKey);
        RestoreStoredString(OriginalPermissionsKey, PermissionsKey);

        DeleteStoredKey(OriginalAccessTokenKey);
        DeleteStoredKey(OriginalRefreshTokenKey);
        DeleteStoredKey(OriginalUserNameKey);
        DeleteStoredKey(OriginalUserIdKey);
        DeleteStoredKey(OriginalTenantIdKey);
        DeleteStoredKey(OriginalRolesKey);
        DeleteStoredKey(OriginalPermissionsKey);
    }

    private void StoreImpersonationPayload(string body)
    {
        if (!IsImpersonating)
        {
            CopyStoredString(AccessTokenKey, OriginalAccessTokenKey);
            CopyStoredString(RefreshTokenKey, OriginalRefreshTokenKey);
            CopyStoredString(UserNameKey, OriginalUserNameKey);
            CopyStoredString(UserIdKey, OriginalUserIdKey);
            CopyStoredString(TenantIdKey, OriginalTenantIdKey);
            CopyStoredString(RolesKey, OriginalRolesKey);
            CopyStoredString(PermissionsKey, OriginalPermissionsKey);
        }

        StoreLoginPayload(body, UserName ?? "");
    }

    private void StoreLoginPayload(string body, string fallbackUserName)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        if (doc.RootElement.TryGetProperty("accessToken", out var accessToken))
        {
            var token = accessToken.GetString() ?? "";
            SetStoredString(AccessTokenKey, token);
            StoreTokenMetadata(token);
        }
        if (doc.RootElement.TryGetProperty("refreshToken", out var refreshToken))
        {
            if (refreshToken.ValueKind == JsonValueKind.Null)
                DeleteStoredKey(RefreshTokenKey);
            else
                SetStoredString(RefreshTokenKey, refreshToken.GetString() ?? "");
        }
        if (doc.RootElement.TryGetProperty("userName", out var signedInUserName))
            SetStoredString(UserNameKey, signedInUserName.GetString() ?? fallbackUserName);
        if (doc.RootElement.TryGetProperty("tenantId", out var tenantId) && tenantId.ValueKind != JsonValueKind.Null)
            SetStoredString(TenantIdKey, tenantId.ToString());
        if (doc.RootElement.TryGetProperty("userId", out var userId) && userId.ValueKind != JsonValueKind.Null)
            SetStoredString(UserIdKey, userId.ToString());
        if (doc.RootElement.TryGetProperty("roles", out var roles))
            SetStoredStringArray(RolesKey, ReadStringArray(roles));
        if (doc.RootElement.TryGetProperty("permissions", out var permissions))
            SetStoredStringArray(PermissionsKey, ReadStringArray(permissions));
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        if (IsAuthenticated)
            await SendAsync(HttpMethod.Post, "api/auth/logout", new { refreshToken = RefreshToken }, ct);

        Session.Clear();
        DeleteStoredAuth();
    }

    private void ClearStoredAuth()
    {
        Session.Clear();
        DeleteStoredAuth();
    }

    private HttpContext HttpContext => _httpContextAccessor.HttpContext
        ?? throw new InvalidOperationException("No active HTTP context.");

    private ISession Session => HttpContext.Session;

    private string? GetStoredString(string key)
    {
        var sessionValue = Session.GetString(key);
        if (!string.IsNullOrWhiteSpace(sessionValue))
            return sessionValue;

        if (!HttpContext.Request.Cookies.TryGetValue(CookieName(key), out var protectedValue) || string.IsNullOrWhiteSpace(protectedValue))
            return null;

        try
        {
            var value = _protector.Unprotect(protectedValue);
            if (!string.IsNullOrWhiteSpace(value))
                Session.SetString(key, value);

            return value;
        }
        catch
        {
            DeleteCookie(key);
            return null;
        }
    }

    private void SetStoredString(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        Session.SetString(key, value);
        HttpContext.Response.Cookies.Append(
            CookieName(key),
            _protector.Protect(value),
            new CookieOptions
            {
                HttpOnly = true,
                Secure = HttpContext.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                Expires = DateTimeOffset.UtcNow.AddDays(14)
            });
    }

    private IReadOnlyList<string> GetStoredStringArray(string key)
    {
        var value = GetStoredString(key);
        if (string.IsNullOrWhiteSpace(value))
            return Array.Empty<string>();

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<string>>(value, JsonOptions) ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private void SetStoredStringArray(string key, IReadOnlyList<string> values) =>
        SetStoredString(key, JsonSerializer.Serialize(values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(), JsonOptions));

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

    private void CopyStoredString(string sourceKey, string destinationKey)
    {
        var value = GetStoredString(sourceKey);
        if (!string.IsNullOrWhiteSpace(value))
            SetStoredString(destinationKey, value);
    }

    private void RestoreStoredString(string sourceKey, string destinationKey)
    {
        var value = GetStoredString(sourceKey);
        if (!string.IsNullOrWhiteSpace(value))
            SetStoredString(destinationKey, value);
        else
            DeleteStoredKey(destinationKey);
    }

    private void DeleteStoredKey(string key)
    {
        Session.Remove(key);
        DeleteCookie(key);
    }

    private void DeleteStoredAuth()
    {
        foreach (var key in StoredKeys)
            DeleteCookie(key);
    }

    private void DeleteCookie(string key) => HttpContext.Response.Cookies.Delete(CookieName(key));

    private static string CookieName(string key) => "__ApiClient." + key;

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

        if (ex.InnerException is not null)
        {
            builder.AppendLine();
            builder.AppendLine("Inner exception");
            builder.AppendLine($"  Type: {ex.InnerException.GetType().FullName}");
            builder.AppendLine($"  Message: {ex.InnerException.Message}");
        }

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
                SetStoredString(UserIdKey, userId);
            if (TryGetClaim(doc.RootElement, out var tenantId, "tenantId", "tenant_id", "tenantid"))
                SetStoredString(TenantIdKey, tenantId);
            if (TryGetClaim(doc.RootElement, out var userName, "unique_name", "name", "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"))
                SetStoredString(UserNameKey, userName);
            var expiresAtUtc = ReadUnixTimeClaim(doc.RootElement, "exp");
            if (expiresAtUtc is not null)
                SetStoredString(TokenExpiresAtUtcKey, expiresAtUtc.Value.UtcDateTime.ToString("O"));
        }
        catch
        {
        }
    }

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

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}

public sealed record ApiResult(int StatusCode, bool Success, string Body, string UserMessage, long ElapsedMilliseconds)
{
    public static ApiResult Failure(string userMessage, string body) => new(0, false, body, userMessage, 0);
}