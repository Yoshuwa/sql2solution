using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdventureWorksLT2017Api.Tests;

public sealed class GeneratedApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly bool RbacEnabled = false;
    private static readonly bool TestAuthenticationFlow = false;
    private static readonly bool TestTenantIsolation = false;
    private static readonly bool TestForbiddenCrossTenantAccess = false;
    private const string MasterUserName = "admin@example.com";
    private const string MasterPassword = "ChangeMe!123";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly WebApplicationFactory<Program> _factory;

    public GeneratedApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task health_endpoint_returns_success()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/health");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task anonymous_crud_request_is_rejected_when_rbac_is_enabled()
    {
        if (!RbacEnabled)
            return;

        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/errorLogs");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task master_admin_can_login_and_receive_a_bearer_token()
    {
        if (!TestAuthenticationFlow)
            return;

        using var client = _factory.CreateClient();
        var login = await LoginAsync(client, MasterUserName, MasterPassword);

        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        Assert.Contains("MasterAdmin", login.Roles);
    }

    [Fact]
    public async Task authenticated_requests_include_a_tenant_context()
    {
        if (!TestTenantIsolation)
            return;

        using var client = _factory.CreateClient();
        var login = await LoginAsync(client, MasterUserName, MasterPassword);

        Assert.NotEqual(default, login.TenantId);
    }

    [Fact]
    public async Task generated_endpoint_matrix_writes_html_report()
    {
        var results = new List<TestResultRow>();
        using var anonymousClient = _factory.CreateClient();
        using var authenticatedClient = _factory.CreateClient();

        LoginResult? login = null;
        if (RbacEnabled)
        {
            login = await CaptureLoginFlowAsync(results, authenticatedClient, MasterUserName, MasterPassword, "Master admin");
            authenticatedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
            await CaptureAuthenticatedAccessFlowAsync(results, authenticatedClient, login, "/api/errorLogs");
            await CaptureRefreshAndLogoutFlowAsync(results, authenticatedClient, login);
            await CaptureTenantBoundaryFlowAsync(results, login, "/api/errorLogs");
        }

        var endpoints = DiscoverEndpoints().ToList();

        foreach (var endpoint in endpoints)
        {
            if (RbacEnabled && endpoint.RequiresAuthentication)
            {
                await CaptureEndpointAsync(
                    results,
                    endpoint.Name + " anonymous rejected",
                    "Anonymous request must be rejected with 401 Unauthorized when the endpoint requires authentication.",
                    "Verify protected endpoints cannot be used without a bearer token.",
                    "401 Unauthorized. This is a passing result because the endpoint correctly blocks anonymous access.",
                    endpoint,
                    anonymousClient,
                    new[] { HttpStatusCode.Unauthorized });
            }

            await CaptureEndpointAsync(
                results,
                endpoint.Name + " authenticated contract",
                "Authenticated request must return one of the generated contract statuses.",
                "Verify an authenticated caller can exercise the generated endpoint without an unhandled server exception.",
                "One of the documented contract statuses. A 5xx response is a validation finding because it usually indicates a mapping, key, constraint, or generated endpoint issue.",
                endpoint,
                RbacEnabled && endpoint.RequiresAuthentication ? authenticatedClient : anonymousClient,
                endpoint.ExpectedAuthenticatedStatuses);
        }

        var reportPath = WriteHtmlReport(results, login);
        Assert.True(File.Exists(reportPath), "Expected generated HTML report at " + reportPath);
    }

    private static async Task<LoginResult> LoginAsync(HttpClient client, string userName, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { userName, password });
        response.EnsureSuccessStatusCode();

        var login = await response.Content.ReadFromJsonAsync<LoginResult>(JsonOptions);
        Assert.NotNull(login);
        return login!;
    }

    private static async Task<LoginResult> CaptureLoginFlowAsync(
        List<TestResultRow> results,
        HttpClient client,
        string userName,
        string password,
        string actor,
        string stepName = "Security flow 01 - login with username/password")
    {
        var started = DateTimeOffset.UtcNow;
        var requestBody = JsonSerializer.Serialize(new { userName, password = Mask(password) }, JsonOptions);
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { userName, password });
        var responseBody = await ReadResponsePreviewAsync(response);
        var login = response.IsSuccessStatusCode
            ? JsonSerializer.Deserialize<LoginResult>(responseBody, JsonOptions)
            : null;
        var passed = response.StatusCode == HttpStatusCode.OK && login is not null && !string.IsNullOrWhiteSpace(login.AccessToken);
        var detail = login is null
            ? "Login did not return a readable token payload."
            : $"LOGIN USERNAME/PASSWORD :: username={login.UserName}; tenant={login.TenantId}; roles={string.Join(", ", login.Roles)}; accessToken={(string.IsNullOrWhiteSpace(login.AccessToken) ? "missing" : "issued")}; refreshToken={(string.IsNullOrWhiteSpace(login.RefreshToken) ? "not issued" : "issued")}.";

        results.Add(new TestResultRow(
            stepName,
            "POST",
            "/api/auth/login",
            passed,
            $"{actor} submits LOGIN USERNAME/PASSWORD.",
            "Verify valid credentials return a bearer token, tenant context, and role list.",
            "HTTP 200 OK with accessToken, userName, tenantId, and roles.",
            "OK (200)",
            response.StatusCode + " (" + (int)response.StatusCode + ")",
            requestBody,
            Limit(RedactTokens(responseBody), 12000),
            detail,
            (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds));

        Assert.True(passed, "Expected login to return a bearer token.");
        return login!;
    }

    private static async Task CaptureAuthenticatedAccessFlowAsync(
        List<TestResultRow> results,
        HttpClient client,
        LoginResult login,
        string path)
    {
        await CaptureHttpFlowStepAsync(
            results,
            "Security flow 02 - access data with bearer token",
            "GET",
            path,
            null,
            client,
            new[] { HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.Forbidden },
            $"User {login.UserName} from tenant {login.TenantId} calls a generated API with Bearer token.",
            "Verify authenticated access reaches the generated endpoint and returns a controlled contract status.",
            "OK, NotFound, BadRequest, or Forbidden. A 5xx response indicates a generated mapping or data-contract issue.");
    }

    private static async Task CaptureRefreshAndLogoutFlowAsync(
        List<TestResultRow> results,
        HttpClient client,
        LoginResult login)
    {
        if (!string.IsNullOrWhiteSpace(login.RefreshToken))
        {
            await CaptureHttpFlowStepAsync(
                results,
                "Security flow 03 - refresh token",
                "POST",
                "/api/auth/refresh",
                new { refreshToken = login.RefreshToken },
                client,
                new[] { HttpStatusCode.OK },
                $"User {login.UserName} submits refreshToken.",
                "Verify the refresh endpoint exchanges a valid refresh token for a new login payload.",
                "HTTP 200 OK with a refreshed access token payload.",
                redactRequest: true);
        }
        else
        {
            AddSkippedFlowRow(
                results,
                "Security flow 03 - refresh token",
                "POST",
                "/api/auth/refresh",
                $"User {login.UserName} has no refresh token because refresh tokens are disabled or not issued.");
        }

        await CaptureHttpFlowStepAsync(
            results,
            "Security flow 04 - logout",
            "POST",
            "/api/auth/logout",
            new { refreshToken = login.RefreshToken },
            client,
            new[] { HttpStatusCode.NoContent, HttpStatusCode.OK },
            $"User {login.UserName} logs out.",
            "Verify logout completes and revokes refresh token state when configured.",
            "HTTP 204 NoContent or 200 OK.",
            redactRequest: true);
    }

    private async Task CaptureTenantBoundaryFlowAsync(
        List<TestResultRow> results,
        LoginResult master,
        string path)
    {
        if (!TestTenantIsolation)
        {
            AddSkippedFlowRow(
                results,
                "Security flow 05 - tenant boundary",
                "GET",
                path,
                "Tenant isolation tests are disabled in this generation config.");
            return;
        }

        if (!TestForbiddenCrossTenantAccess)
        {
            AddSkippedFlowRow(
                results,
                "Security flow 05 - tenant boundary",
                "GET",
                path,
                "Cross-tenant access scenario reporting is disabled in this generation config.");
            return;
        }

        using var setupClient = _factory.CreateClient();
        setupClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", master.AccessToken);
        var tenantName = "Flow Tenant DEF " + Guid.NewGuid().ToString("N");
        var tenantResponse = await setupClient.PostAsJsonAsync("/api/auth/tenants", new { name = tenantName });
        if (!tenantResponse.IsSuccessStatusCode)
        {
            AddSkippedFlowRow(
                results,
                "Security flow 05 - tenant boundary",
                "GET",
                path,
                "Could not create DEF tenant for the scenario. Actual status: " + tenantResponse.StatusCode + " (" + (int)tenantResponse.StatusCode + ").");
            return;
        }

        var tenant = await tenantResponse.Content.ReadFromJsonAsync<TenantResult>(JsonOptions);
        if (tenant is null)
        {
            AddSkippedFlowRow(results, "Security flow 05 - tenant boundary", "GET", path, "Create tenant response was empty.");
            return;
        }

        var otherUserName = "tenant-def-" + Guid.NewGuid().ToString("N") + "@example.com";
        var otherPassword = "ChangeMe!123";
        var registerResponse = await setupClient.PostAsJsonAsync("/api/auth/users", new { userName = otherUserName, password = otherPassword, tenantId = tenant.Id, role = "User" });
        if (!registerResponse.IsSuccessStatusCode)
        {
            AddSkippedFlowRow(
                results,
                "Security flow 05 - tenant boundary",
                "GET",
                path,
                "Could not create DEF tenant user for the scenario. Actual status: " + registerResponse.StatusCode + " (" + (int)registerResponse.StatusCode + ").");
            return;
        }

        using var otherClient = _factory.CreateClient();
        var otherLogin = await CaptureLoginFlowAsync(
            results,
            otherClient,
            otherUserName,
            otherPassword,
            "Tenant DEF user",
            "Security flow 05 - tenant DEF login with username/password");
        otherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherLogin.AccessToken);
        await CaptureHttpFlowStepAsync(
            results,
            "Security flow 06 - tenant DEF user accesses tenant-scoped data",
            "GET",
            path,
            null,
            otherClient,
            new[] { HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.Forbidden },
            $"Tenant data isolation: user {otherLogin.UserName} from DEF tenant {tenant.Id} with roles {string.Join(", ", otherLogin.Roles)} tries to access generated resource path {path}.",
            $"Verify a DEF tenant principal receives only controlled tenant-scoped results when accessing resource data that may include ABC tenant data owned by {master.UserName}.",
            "OK with tenant-filtered data, NotFound, BadRequest, or Forbidden. A 5xx response indicates a generated isolation or contract issue.");
    }

    private sealed record LoginResult(
        string AccessToken,
        DateTimeOffset ExpiresAtUtc,
        string? RefreshToken,
        DateTimeOffset? RefreshTokenExpiresAtUtc,
        string UserName,
        Guid TenantId,
        IReadOnlyList<string> Roles);

    private sealed record TenantResult(Guid Id, string Name, bool IsEnabled);
    private sealed record EndpointCase(string Name, string Method, string Path, object? Body, bool RequiresAuthentication, HttpStatusCode[] ExpectedAuthenticatedStatuses);
    private sealed record TestResultRow(
        string Name,
        string Method,
        string Path,
        bool Passed,
        string TestPerformed,
        string Goal,
        string ExpectedResult,
        string ExpectedStatus,
        string ActualStatus,
        string RequestBody,
        string ResponseBody,
        string Detail,
        long ElapsedMilliseconds);

    private static IEnumerable<EndpointCase> DiscoverEndpoints()
    {
        foreach (var controller in typeof(Program).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .OrderBy(type => type.Name, StringComparer.Ordinal))
        {
            var controllerRoute = controller.GetCustomAttributes<RouteAttribute>().FirstOrDefault()?.Template ?? "";
            var controllerRequiresAuth = controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any();
            var controllerAllowsAnonymous = controller.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any();

            foreach (var method in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .OrderBy(method => method.Name, StringComparer.Ordinal))
            {
                var httpAttributes = method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).ToList();
                if (httpAttributes.Count == 0)
                    continue;

                var requiresAuth = !controllerAllowsAnonymous &&
                    (controllerRequiresAuth || method.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any()) &&
                    !method.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any();

                foreach (var http in httpAttributes)
                {
                    foreach (var verb in http.HttpMethods)
                    {
                        var path = NormalizeRoute(CombineRoute(controllerRoute, http.Template));
                        yield return new EndpointCase(
                            controller.Name.Replace("Controller", "", StringComparison.Ordinal) + "." + method.Name,
                            verb.ToUpperInvariant(),
                            path,
                            CreateBodyFor(verb, method),
                            requiresAuth,
                            ExpectedStatusesFor(verb, requiresAuth));
                    }
                }
            }
        }
    }

    private static object? CreateBodyFor(string verb, MethodInfo method)
    {
        if (!verb.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
            !verb.Equals("PUT", StringComparison.OrdinalIgnoreCase) &&
            !verb.Equals("PATCH", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var bodyParameter = method.GetParameters()
            .FirstOrDefault(parameter =>
                parameter.ParameterType != typeof(CancellationToken) &&
                !parameter.ParameterType.IsPrimitive &&
                parameter.ParameterType != typeof(string) &&
                parameter.ParameterType != typeof(Guid));
        if (bodyParameter is null)
            return new { };

        return CreateSampleBody(bodyParameter.ParameterType);
    }

    private static object CreateSampleBody(Type type)
    {
        if (type.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type != typeof(string))
        {
            var itemType = type.GetGenericArguments().FirstOrDefault() ?? typeof(object);
            return new[] { CreateSampleObject(itemType) };
        }

        return CreateSampleObject(type);
    }

    private static Dictionary<string, object?> CreateSampleObject(Type type)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetIndexParameters().Length == 0))
        {
            values[property.Name] = CreateSampleValue(
                property.PropertyType,
                property.Name,
                GetMaxLengthForRequestProperty(type, property.Name),
                GetIsPrimaryKeyForRequestProperty(type, property.Name));
        }

        return values;
    }

    private static int? GetMaxLengthForRequestProperty(Type requestType, string propertyName)
    {
        var requestProperty = requestType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        var requestMaxLength = requestProperty?.GetCustomAttribute<MaxLengthAttribute>()?.Length
            ?? requestProperty?.GetCustomAttribute<StringLengthAttribute>()?.MaximumLength;
        if (requestMaxLength is > 0)
            return requestMaxLength;

        var entityName = requestType.Name;
        if (entityName.StartsWith("Create", StringComparison.Ordinal))
            entityName = entityName["Create".Length..];
        if (entityName.StartsWith("Update", StringComparison.Ordinal))
            entityName = entityName["Update".Length..];
        if (entityName.EndsWith("Request", StringComparison.Ordinal))
            entityName = entityName[..^"Request".Length];

        var entityType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(GetLoadableTypes)
            .FirstOrDefault(type => type.Name.Equals(entityName, StringComparison.Ordinal));
        var entityProperty = entityType?.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        return entityProperty?.GetCustomAttribute<MaxLengthAttribute>()?.Length
            ?? entityProperty?.GetCustomAttribute<StringLengthAttribute>()?.MaximumLength;
    }

    private static bool GetIsPrimaryKeyForRequestProperty(Type requestType, string propertyName)
    {
        var entityProperty = GetEntityPropertyForRequestProperty(requestType, propertyName);
        return entityProperty?.GetCustomAttribute<KeyAttribute>() is not null;
    }

    private static PropertyInfo? GetEntityPropertyForRequestProperty(Type requestType, string propertyName)
    {
        var entityName = requestType.Name;
        if (entityName.StartsWith("Create", StringComparison.Ordinal))
            entityName = entityName["Create".Length..];
        if (entityName.StartsWith("Update", StringComparison.Ordinal))
            entityName = entityName["Update".Length..];
        if (entityName.EndsWith("Request", StringComparison.Ordinal))
            entityName = entityName[..^"Request".Length];

        var entityType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(GetLoadableTypes)
            .FirstOrDefault(type => type.Name.Equals(entityName, StringComparison.Ordinal));
        return entityType?.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type is not null)!;
        }
    }

    private static object? CreateSampleValue(Type type, string name, int? maxLength, bool isPrimaryKey)
    {
        var targetType = Nullable.GetUnderlyingType(type) ?? type;
        if (targetType == typeof(string))
        {
            if (name.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("key", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("id", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("key", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("code", StringComparison.OrdinalIgnoreCase))
            {
                return CreateUniqueSampleString(name, maxLength);
            }

            var value = name.Contains("email", StringComparison.OrdinalIgnoreCase) || name.Contains("user", StringComparison.OrdinalIgnoreCase)
                ? "generated-" + Guid.NewGuid().ToString("N") + "@example.com"
                : "generated-" + name;
            return TrimSampleString(value, maxLength);
        }
        if (targetType == typeof(Guid))
            return Guid.NewGuid();
        if (targetType == typeof(DateTime))
            return DateTime.UtcNow;
        if (targetType == typeof(DateTimeOffset))
            return DateTimeOffset.UtcNow;
        if (targetType == typeof(bool))
            return false;
        if (targetType == typeof(byte[]))
            return Array.Empty<byte>();
        if (targetType.IsEnum)
            return Enum.GetValues(targetType).GetValue(0);
        if (targetType == typeof(decimal))
            return 1m;
        if (targetType == typeof(double))
            return 1d;
        if (targetType == typeof(float))
            return 1f;
        if (targetType == typeof(long) || targetType == typeof(ulong))
            return isPrimaryKey ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() : 1L;
        if (targetType == typeof(short) || targetType == typeof(ushort))
            return isPrimaryKey ? (short)Random.Shared.Next(1000, short.MaxValue) : (short)1;
        if (targetType == typeof(byte) || targetType == typeof(sbyte))
            return (byte)1;
        if (targetType == typeof(int) || targetType == typeof(uint))
            return isPrimaryKey ? Random.Shared.Next(100000, int.MaxValue) : 1;

        return null;
    }

    private static string CreateUniqueSampleString(string name, int? maxLength)
    {
        var token = Guid.NewGuid().ToString("N");
        if (maxLength is > 0)
        {
            if (maxLength.Value <= 1)
                return token[..maxLength.Value];

            var prefix = char.IsLetter(name.FirstOrDefault()) ? char.ToLowerInvariant(name[0]).ToString() : "g";
            return (prefix + token)[..Math.Min(maxLength.Value, prefix.Length + token.Length)];
        }

        return "generated-" + name + "-" + token;
    }

    private static string TrimSampleString(string value, int? maxLength)
    {
        if (maxLength is not > 0 || value.Length <= maxLength.Value)
            return value;

        return maxLength.Value == 1 ? value[..1] : value[..maxLength.Value];
    }

    private static HttpStatusCode[] ExpectedStatusesFor(string verb, bool requiresAuth)
    {
        if (verb.Equals("GET", StringComparison.OrdinalIgnoreCase))
            return new[] { HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden };
        if (verb.Equals("DELETE", StringComparison.OrdinalIgnoreCase))
            return new[] { HttpStatusCode.NoContent, HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden };
        return new[] { HttpStatusCode.OK, HttpStatusCode.Created, HttpStatusCode.NoContent, HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.Conflict, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden };
    }

    private static string CombineRoute(string controllerRoute, string? actionRoute)
    {
        var combined = string.Join("/", new[] { controllerRoute, actionRoute ?? "" }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part.Trim('/')));
        return string.IsNullOrWhiteSpace(combined) ? "/" : "/" + combined;
    }

    private static string NormalizeRoute(string route)
    {
        var value = route;
        value = System.Text.RegularExpressions.Regex.Replace(value, "\\{[^}:]+:guid\\}", Guid.Empty.ToString());
        value = System.Text.RegularExpressions.Regex.Replace(value, "\\{[^}:]+:(int|long)\\}", "-1");
        value = System.Text.RegularExpressions.Regex.Replace(value, "\\{[^}]+\\}", "-1");
        return value.Replace("//", "/");
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string path, object? body)
    {
        return method switch
        {
            "GET" => await client.GetAsync(path),
            "POST" => await client.PostAsJsonAsync(path, body ?? new { }),
            "PUT" => await client.PutAsJsonAsync(path, body ?? new { }),
            "PATCH" => await client.PatchAsJsonAsync(path, body ?? new { }),
            "DELETE" => await client.DeleteAsync(path),
            _ => throw new NotSupportedException("Unsupported generated test method: " + method)
        };
    }

    private static async Task CaptureHttpFlowStepAsync(
        List<TestResultRow> results,
        string name,
        string method,
        string path,
        object? body,
        HttpClient client,
        IReadOnlyCollection<HttpStatusCode> expectedStatuses,
        string testPerformed,
        string goal,
        string expectedResult,
        bool redactRequest = false)
    {
        var started = DateTimeOffset.UtcNow;
        var requestBody = body is null
            ? "(none)"
            : JsonSerializer.Serialize(body, JsonOptions);
        if (redactRequest)
            requestBody = RedactTokens(requestBody);

        var expectedStatusText = string.Join(", ", expectedStatuses.Select(status => status + " (" + (int)status + ")"));
        try
        {
            using var response = await SendAsync(client, method, path, body);
            var responseBody = await ReadResponsePreviewAsync(response);
            var actualStatus = response.StatusCode + " (" + (int)response.StatusCode + ")";
            var passed = expectedStatuses.Contains(response.StatusCode);
            results.Add(new TestResultRow(
                name,
                method,
                path,
                passed,
                testPerformed,
                goal,
                expectedResult,
                expectedStatusText,
                actualStatus,
                Limit(requestBody, 4000),
                Limit(RedactTokens(string.IsNullOrWhiteSpace(responseBody) ? "(empty)" : responseBody), 12000),
                passed ? "Flow step completed with an expected result." : "Flow step returned an unexpected status.",
                (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds));
        }
        catch (Exception ex)
        {
            results.Add(new TestResultRow(
                name,
                method,
                path,
                false,
                testPerformed,
                goal,
                expectedResult,
                expectedStatusText,
                "(exception)",
                Limit(requestBody, 4000),
                "(no response)",
                Limit(ex.ToString(), 12000),
                (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds));
        }
    }

    private static void AddSkippedFlowRow(List<TestResultRow> results, string name, string method, string path, string reason)
    {
        results.Add(new TestResultRow(
            name,
            method,
            path,
            true,
            "Flow step was skipped because its prerequisite is not available.",
            reason,
            "Skipped steps are informational and do not fail validation.",
            "(skipped)",
            "(skipped)",
            "(none)",
            "(none)",
            reason,
            0));
    }

    private static string Mask(string value) =>
        string.IsNullOrWhiteSpace(value) ? "(empty)" : new string('*', Math.Min(12, value.Length));

    private static string RedactTokens(string value)
    {
        value = System.Text.RegularExpressions.Regex.Replace(value, "(\"accessToken\"\\s*:\\s*\")[^\"]+(\")", "$1<redacted>$2", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        value = System.Text.RegularExpressions.Regex.Replace(value, "(\"refreshToken\"\\s*:\\s*\")[^\"]+(\")", "$1<redacted>$2", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        value = System.Text.RegularExpressions.Regex.Replace(value, "(Bearer\\s+)[A-Za-z0-9._~+\\-/=]+", "$1<redacted>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return value;
    }

    private static async Task CaptureEndpointAsync(
        List<TestResultRow> results,
        string name,
        string testPerformed,
        string goal,
        string expectedResult,
        EndpointCase endpoint,
        HttpClient client,
        IReadOnlyCollection<HttpStatusCode> expectedStatuses)
    {
        var started = DateTimeOffset.UtcNow;
        var requestBody = Limit(SerializeBody(endpoint.Body), 4000);
        var expectedStatusText = string.Join(", ", expectedStatuses.Select(status => status + " (" + (int)status + ")"));
        try
        {
            using var response = await SendAsync(client, endpoint.Method, endpoint.Path, endpoint.Body);
            var responseBody = await ReadResponsePreviewAsync(response);
            var passed = expectedStatuses.Contains(response.StatusCode);
            var actualStatus = response.StatusCode + " (" + (int)response.StatusCode + ")";
            var detail = passed
                ? "Passed"
                : "Unexpected response status. Review the request and response captured below.";

            results.Add(new TestResultRow(
                name,
                endpoint.Method,
                endpoint.Path,
                passed,
                testPerformed,
                goal,
                expectedResult,
                expectedStatusText,
                actualStatus,
                requestBody,
                string.IsNullOrWhiteSpace(responseBody) ? "(empty)" : Limit(responseBody, 12000),
                detail,
                (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds));
        }
        catch (Exception ex)
        {
            results.Add(new TestResultRow(
                name,
                endpoint.Method,
                endpoint.Path,
                false,
                testPerformed,
                goal,
                expectedResult,
                expectedStatusText,
                "(exception)",
                requestBody,
                "(no response)",
                Limit(ex.ToString(), 12000),
                (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds));
        }
    }

    private static async Task CaptureAsync(List<TestResultRow> results, string name, string method, string path, Func<Task> action)
    {
        var started = DateTimeOffset.UtcNow;
        try
        {
            await action();
            results.Add(new TestResultRow(name, method, path, true, "Custom generated test action.", "Run the generated custom validation action.", "The action completes without throwing an exception.", "(not captured)", "(not captured)", "(not captured)", "(not captured)", "Passed", (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds));
        }
        catch (Exception ex)
        {
            results.Add(new TestResultRow(name, method, path, false, "Custom generated test action.", "Run the generated custom validation action.", "The action completes without throwing an exception.", "(not captured)", "(exception)", "(not captured)", "(not captured)", ex.ToString(), (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds));
        }
    }

    private static string SerializeBody(object? body) =>
        body is null ? "(none)" : JsonSerializer.Serialize(body, JsonOptions);

    private static async Task<string> ReadResponsePreviewAsync(HttpResponseMessage response)
    {
        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength.HasValue && contentLength.Value > 50000)
            return "(response body omitted because it is " + contentLength.Value + " bytes)";

        return Limit(await response.Content.ReadAsStringAsync(), 12000);
    }

    private static string Limit(string value, int maxLength) =>
        value.Length <= maxLength
            ? value
            : value[..maxLength] + Environment.NewLine + "... truncated " + (value.Length - maxLength) + " characters";

    private static string WriteHtmlReport(IReadOnlyList<TestResultRow> results, LoginResult? login)
    {
        var outputDir = Path.Combine(AppContext.BaseDirectory, "TestResults");
        Directory.CreateDirectory(outputDir);
        var reportPath = Path.Combine(outputDir, "generated-api-test-report.html");
        SimpleHtmlReport.Write(reportPath, "Generated API validation", results, login?.UserName ?? "Anonymous");
        return reportPath;
    }

    private static class SimpleHtmlReport
    {
        public static void Write(string path, string title, IReadOnlyList<TestResultRow> results, string actor)
        {
            var passed = results.Count(result => result.Passed);
            var failed = results.Count - passed;
            var flowRows = results
                .Where(result => result.Name.StartsWith("Security flow", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var builder = new StringBuilder();
            builder.AppendLine("<!doctype html>");
            builder.AppendLine("<html lang=\"en\">");
            builder.AppendLine("<head>");
            builder.AppendLine("<meta charset=\"utf-8\">");
            builder.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            builder.Append("<title>").Append(Html(title)).AppendLine("</title>");
            builder.AppendLine("<style>");
            builder.AppendLine(":root{color-scheme:dark;--bg:#0e1424;--panel:#111827;--header:#22304c;--text:#eef4ff;--muted:#c8d3e6;--line:#2d3a55;--pass:#2f7d47;--fail:#a12630;--pass-card:#4dbb70;--fail-card:#e23e4a;}");
            builder.AppendLine("*{box-sizing:border-box} body{margin:0;background:var(--bg);color:var(--text);font-family:Segoe UI,Arial,sans-serif;font-size:15px;}");
            builder.AppendLine("header{display:flex;justify-content:space-between;gap:24px;align-items:center;padding:28px 40px 22px;background:var(--header);}");
            builder.AppendLine("h1{margin:0 0 8px;font-size:30px;font-weight:800;letter-spacing:0}.meta{color:var(--muted);font-size:15px}.metrics{display:flex;gap:18px;flex-wrap:wrap}.metric{min-width:128px;padding:10px 18px;color:white}.metric.pass{background:var(--pass-card)}.metric.fail{background:var(--fail-card)}.metric span{display:block;font-size:12px;font-weight:800;text-transform:uppercase}.metric strong{font-size:28px;line-height:1}");
            builder.AppendLine("main{padding:28px 40px}.toolbar{display:grid;grid-template-columns:minmax(220px,1fr) 170px 170px auto auto;gap:10px;align-items:center;margin-bottom:18px}.toolbar input,.toolbar select,.toolbar button{background:#0a1020;color:var(--text);border:1px solid var(--line);border-radius:6px;padding:10px 12px;font:inherit}.toolbar button{cursor:pointer;font-weight:700}.summary{margin-bottom:14px;color:var(--muted)}.flow-section{margin:0 0 22px}.flow-section h2{margin:0 0 12px;font-size:20px}.flow-list{display:grid;gap:10px}.flow-card{display:grid;grid-template-columns:48px 1fr auto;gap:14px;align-items:start;background:#101b2a;border:1px solid var(--line);border-radius:6px;padding:14px}.flow-card.fail{background:#1b1620}.flow-index{width:34px;height:34px;border-radius:50%;display:grid;place-items:center;background:#22304c;color:#eaf1ff;font-weight:800}.flow-title{font-weight:800}.flow-meta{margin-top:4px;color:var(--muted)}.flow-detail{margin-top:8px;color:#dbe7ff}.flow-status{white-space:nowrap;color:#dbe7ff;font-weight:800}table{width:100%;border-collapse:separate;border-spacing:0 8px}th{color:#c4d3f3;text-align:left;padding:8px 10px;font-weight:800}td{background:var(--panel);border-top:1px solid var(--line);border-bottom:1px solid var(--line);padding:12px 10px;vertical-align:top}td:first-child{border-left:1px solid var(--line);border-radius:6px 0 0 6px}td:last-child{border-right:1px solid var(--line);border-radius:0 6px 6px 0}.badge{display:inline-block;min-width:76px;text-align:center;color:white;font-weight:800;padding:6px 12px}.badge.pass{background:var(--pass)}.badge.fail{background:var(--fail)}.method{font-weight:800}.endpoint{font-family:Consolas,monospace;word-break:break-all}.fail-row td{background:#1b1620}.pass-row td{background:#101b2a}.test-name{font-weight:800}.muted{color:var(--muted)}details{margin-top:8px}summary{cursor:pointer;color:#bfd0ef;font-weight:800}.detail-grid{display:grid;grid-template-columns:1fr 1fr;gap:12px;margin-top:10px}.detail-block{background:#080d19;border:1px solid var(--line);border-radius:6px;padding:10px}.detail-block h3{font-size:13px;margin:0 0 8px;color:#c4d3f3}pre{margin:0;white-space:pre-wrap;word-break:break-word;font-family:Consolas,monospace;color:#eef4ff}.hidden{display:none!important}");
            builder.AppendLine("@media(max-width:900px){.toolbar{grid-template-columns:1fr 1fr}.toolbar input{grid-column:1/-1}.detail-grid{grid-template-columns:1fr}.flow-card{grid-template-columns:40px 1fr}.flow-status{grid-column:2}" + "}");
            builder.AppendLine("</style>");
            builder.AppendLine("</head>");
            builder.AppendLine("<body>");
            builder.AppendLine("<header>");
            builder.AppendLine("<div>");
            builder.Append("<h1>").Append(Html(title)).AppendLine("</h1>");
            builder.Append("<div class=\"meta\">Actor: ").Append(Html(actor)).Append(" &nbsp; Generated: ").Append(Html(DateTimeOffset.UtcNow.ToString("u"))).AppendLine("</div>");
            builder.AppendLine("</div>");
            builder.AppendLine("<div class=\"metrics\">");
            builder.Append("<div class=\"metric pass\"><span>Passed</span><strong>").Append(passed).AppendLine("</strong></div>");
            builder.Append("<div class=\"metric fail\"><span>Failed</span><strong>").Append(failed).AppendLine("</strong></div>");
            builder.AppendLine("</div>");
            builder.AppendLine("</header>");
            builder.AppendLine("<main>");
            builder.Append("<div class=\"summary\">Generated endpoint contract checks: ").Append(results.Count).AppendLine("</div>");
            if (flowRows.Count > 0)
            {
                builder.AppendLine("<section class=\"flow-section\">");
                builder.AppendLine("<h2>Security Flow</h2>");
                builder.AppendLine("<div class=\"flow-list\">");
                for (var i = 0; i < flowRows.Count; i++)
                {
                    var row = flowRows[i];
                    var state = row.Passed ? "pass" : "fail";
                    builder.Append("<article class=\"flow-card ").Append(state).AppendLine("\">");
                    builder.Append("<div class=\"flow-index\">").Append(i + 1).AppendLine("</div>");
                    builder.AppendLine("<div>");
                    builder.Append("<div class=\"flow-title\">").Append(Html(row.Name)).AppendLine("</div>");
                    builder.Append("<div class=\"flow-meta\">").Append(Html(row.Method)).Append(" ").Append(Html(row.Path)).Append(" :: ").Append(Html(row.ActualStatus)).AppendLine("</div>");
                    builder.Append("<div class=\"flow-detail\">").Append(Html(row.TestPerformed)).AppendLine("</div>");
                    if (!string.IsNullOrWhiteSpace(row.Detail))
                        builder.Append("<div class=\"flow-detail\">").Append(Html(row.Detail)).AppendLine("</div>");
                    builder.AppendLine("</div>");
                    builder.Append("<div class=\"flow-status\">").Append(row.Passed ? "PASS" : "FAIL").AppendLine("</div>");
                    builder.AppendLine("</article>");
                }

                builder.AppendLine("</div>");
                builder.AppendLine("</section>");
            }

            builder.AppendLine("<div class=\"toolbar\">");
            builder.AppendLine("<input id=\"searchBox\" type=\"search\" placeholder=\"Search endpoint, method, status, request, response...\">");
            builder.AppendLine("<select id=\"resultFilter\"><option value=\"all\">All results</option><option value=\"pass\">Passed</option><option value=\"fail\">Failed</option></select>");
            builder.AppendLine("<select id=\"methodFilter\"><option value=\"all\">All methods</option>");
            foreach (var method in results.Select(result => result.Method).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(method => method, StringComparer.OrdinalIgnoreCase))
                builder.Append("<option value=\"").Append(Html(method.ToLowerInvariant())).Append("\">").Append(Html(method)).AppendLine("</option>");
            builder.AppendLine("</select>");
            builder.AppendLine("<button type=\"button\" id=\"expandAll\">Expand all</button>");
            builder.AppendLine("<button type=\"button\" id=\"collapseAll\">Collapse all</button>");
            builder.AppendLine("</div>");
            builder.AppendLine("<table>");
            builder.AppendLine("<thead><tr><th>Result</th><th>Method</th><th>Endpoint</th><th>Duration</th><th>Test performed</th></tr></thead>");
            builder.AppendLine("<tbody>");
            foreach (var row in results)
            {
                var state = row.Passed ? "pass" : "fail";
                var searchText = Limit(string.Join(" ", row.Name, row.Method, row.Path, row.TestPerformed, row.Goal, row.ExpectedResult, row.ExpectedStatus, row.ActualStatus, row.RequestBody, row.ResponseBody, row.Detail), 4000).ToLowerInvariant();
                builder.Append("<tr class=\"test-row ").Append(state).Append("-row\" data-result=\"").Append(state).Append("\" data-method=\"").Append(Html(row.Method.ToLowerInvariant())).Append("\" data-search=\"").Append(Html(searchText)).AppendLine("\">");
                builder.Append("<td data-label=\"Result\"><span class=\"badge ").Append(state).Append("\">").Append(row.Passed ? "PASS" : "FAIL").AppendLine("</span></td>");
                builder.Append("<td data-label=\"Method\" class=\"method\">").Append(Html(row.Method)).AppendLine("</td>");
                builder.Append("<td data-label=\"Endpoint\" class=\"endpoint\">").Append(Html(row.Path)).AppendLine("</td>");
                builder.Append("<td data-label=\"Duration\">").Append(row.ElapsedMilliseconds).AppendLine(" ms</td>");
                builder.Append("<td data-label=\"Test performed\"><div class=\"test-name\">").Append(Html(row.Name)).AppendLine("</div>");
                builder.Append("<div class=\"muted\">").Append(Html(row.TestPerformed)).AppendLine("</div>");
                builder.Append("<div class=\"muted\"><strong>Goal:</strong> ").Append(Html(row.Goal)).AppendLine("</div>");
                builder.Append("<div class=\"muted\"><strong>Expected:</strong> ").Append(Html(row.ExpectedResult)).AppendLine("</div>");
                builder.AppendLine("<details>");
                builder.AppendLine("<summary>View request and response</summary>");
                builder.AppendLine("<div class=\"detail-grid\">");
                builder.Append("<div class=\"detail-block\"><h3>Goal</h3><pre>").Append(Html(row.Goal)).AppendLine("</pre></div>");
                builder.Append("<div class=\"detail-block\"><h3>Expected result</h3><pre>").Append(Html(row.ExpectedResult)).AppendLine("</pre></div>");
                builder.Append("<div class=\"detail-block\"><h3>Expected status</h3><pre>").Append(Html(row.ExpectedStatus)).AppendLine("</pre></div>");
                builder.Append("<div class=\"detail-block\"><h3>Actual status</h3><pre>").Append(Html(row.ActualStatus)).AppendLine("</pre></div>");
                builder.Append("<div class=\"detail-block\"><h3>Request</h3><pre>").Append(Html(row.Method + " " + row.Path + Environment.NewLine + row.RequestBody)).AppendLine("</pre></div>");
                builder.Append("<div class=\"detail-block\"><h3>Response</h3><pre>").Append(Html(row.ResponseBody)).AppendLine("</pre></div>");
                builder.Append("<div class=\"detail-block\"><h3>Detail</h3><pre>").Append(Html(row.Detail)).AppendLine("</pre></div>");
                builder.AppendLine("</div>");
                builder.AppendLine("</details></td>");
                builder.AppendLine("</tr>");
            }

            builder.AppendLine("</tbody></table>");
            builder.AppendLine("<script>");
            builder.AppendLine("const rows=[...document.querySelectorAll('.test-row')];");
            builder.AppendLine("const searchBox=document.getElementById('searchBox');");
            builder.AppendLine("const resultFilter=document.getElementById('resultFilter');");
            builder.AppendLine("const methodFilter=document.getElementById('methodFilter');");
            builder.AppendLine("function applyFilters(){const q=searchBox.value.trim().toLowerCase();const result=resultFilter.value;const method=methodFilter.value;for(const row of rows){const show=(result==='all'||row.dataset.result===result)&&(method==='all'||row.dataset.method===method)&&(!q||row.dataset.search.includes(q));row.classList.toggle('hidden',!show);}" + "}");
            builder.AppendLine("searchBox.addEventListener('input',applyFilters);resultFilter.addEventListener('change',applyFilters);methodFilter.addEventListener('change',applyFilters);");
            builder.AppendLine("document.getElementById('expandAll').addEventListener('click',()=>document.querySelectorAll('details').forEach(d=>d.open=true));");
            builder.AppendLine("document.getElementById('collapseAll').addEventListener('click',()=>document.querySelectorAll('details').forEach(d=>d.open=false));");
            builder.AppendLine("</script>");
            builder.AppendLine("</main></body></html>");
            File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
        }

        private static string Html(string value) => WebUtility.HtmlEncode(value);
    }
}