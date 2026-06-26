using System.Text.Json;
using System.Text.Json.Nodes;
using MiningFleetOps.Client.Models;
using MiningFleetOps.Client.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MiningFleetOps.Client.Pages;

public sealed partial class ResourceModel : PageModel
{
    private readonly ApiSession _api;

    public ResourceModel(ApiSession api)
    {
        _api = api;
    }

    [BindProperty(SupportsGet = true)]
    public string Key { get; set; } = "";
    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;
    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; }
    [BindProperty(SupportsGet = true)]
    public string SearchText { get; set; } = "";
    [BindProperty(SupportsGet = true)]
    public string FilterField { get; set; } = "";
    [BindProperty(SupportsGet = true)]
    public string FilterValue { get; set; } = "";
    [BindProperty(SupportsGet = true)]
    public string SortBy { get; set; } = "";
    [BindProperty(SupportsGet = true)]
    public string SortDirection { get; set; } = "asc";
    [BindProperty]
    public string BulkField { get; set; } = "";
    [BindProperty]
    public string BulkValue { get; set; } = "";

    public ApiResource? Resource { get; private set; }
    public IReadOnlyList<ApiField> DisplayFields => Resource is null
        ? Array.Empty<ApiField>()
        : Resource.Fields
            .Where(apiField => ApiCatalog.ShowAuditFields || !apiField.IsAuditField)
            .ToList();
    public IReadOnlyList<ApiField> EditFields => Resource is null
        ? Array.Empty<ApiField>()
        : Resource.Fields
            .Where(apiField => !apiField.IsPrimaryKey && (ApiCatalog.ShowAuditFields || !apiField.IsAuditField))
            .ToList();
    public IReadOnlyList<ApiField> AuditFields => Resource is null
        ? Array.Empty<ApiField>()
        : Resource.Fields
            .Where(apiField => !apiField.IsPrimaryKey && apiField.IsAuditField)
            .ToList();
    public IReadOnlyList<ApiField> BulkEditableFields => EditFields;
    public string ResponseBody { get; private set; } = "";
    public string ErrorMessage { get; private set; } = "";
    public string EndpointPerformance { get; private set; } = "";
    public int LastStatusCode { get; private set; }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool CanRead => Resource is not null && _api.CanRead(Resource);
    public bool CanWrite => Resource is not null && _api.CanWrite(Resource);
    public bool CanUpdate => Resource is not null && _api.CanUpdate(Resource);
    public bool CanDelete => Resource is not null && _api.CanDelete(Resource);
    public int TotalCount { get; private set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)Math.Max(1, PageSize)));
    public List<Dictionary<string, object?>> Rows { get; } = new();
    public List<Dictionary<string, object?>> HistoryRows { get; } = new();
    public Dictionary<string, IReadOnlyList<LookupOption>> LookupOptions { get; } = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<LookupOption> LookupOptionsFor(string fieldName) =>
        LookupOptions.TryGetValue(fieldName, out var options) ? options : Array.Empty<LookupOption>();

    public async Task OnGetAsync(CancellationToken ct)
    {
        Resource = ApiCatalog.Find(Key);
        EnsurePageSize();
        if (string.IsNullOrWhiteSpace(SortBy))
            SortBy = Resource?.PrimaryKey ?? "";
        if (CanRead)
            await LoadLookupOptionsAsync(ct);
        else if (Resource is not null)
            await LoadReadDeniedMessageAsync(ct);
    }

    public async Task OnPostLoadAsync(bool clearQuery, CancellationToken ct)
    {
        if (clearQuery)
        {
            SearchText = "";
            FilterField = "";
            FilterValue = "";
            SortBy = "";
            SortDirection = "asc";
            CurrentPage = 1;
        }

        Resource = ApiCatalog.Find(Key);
        EnsurePageSize();
        if (!CanRead)
        {
            await LoadReadDeniedMessageAsync(ct);
            return;
        }

        await SendAsync(HttpMethod.Get, "", null, ct);
    }

    public async Task OnPostGetByIdAsync(string id, CancellationToken ct)
    {
        Resource = ApiCatalog.Find(Key);
        EnsurePageSize();
        if (!CanRead)
        {
            Deny("You do not have read permission for this resource.");
            return;
        }

        await SendAsync(HttpMethod.Get, "/" + Uri.EscapeDataString(id ?? ""), null, ct);
    }

    public async Task OnPostCreateAsync([FromForm] Dictionary<string, string> values, CancellationToken ct)
    {
        Resource = ApiCatalog.Find(Key);
        EnsurePageSize();
        if (!CanWrite)
        {
            Deny("You do not have write permission for this resource.");
            return;
        }

        await SendAsync(HttpMethod.Post, "", BuildBody(ReadFormValues(values), isCreate: true), ct);
    }

    public async Task OnPostHistoryAsync(string id, CancellationToken ct)
    {
        Resource = ApiCatalog.Find(Key);
        if (!CanRead)
        {
            Deny("You do not have read permission for this resource history.");
            return;
        }

        await SendAsync(HttpMethod.Get, "/" + Uri.EscapeDataString(id ?? "") + "/history", null, ct);
        if (!HasError)
        {
            HistoryRows.Clear();
            HistoryRows.AddRange(ParseRows(ResponseBody));
            ResponseBody = $"Loaded activity for {Resource?.DisplayName} record '{id}'." + Environment.NewLine + ResponseBody;
        }
    }

    public async Task OnPostUpdateAsync(string id, [FromForm] Dictionary<string, string> values, CancellationToken ct)
    {
        Resource = ApiCatalog.Find(Key);
        if (!CanUpdate)
        {
            Deny("You do not have update permission for this resource.");
            return;
        }

        await SendAsync(HttpMethod.Put, "/" + Uri.EscapeDataString(id ?? ""), BuildBody(ReadFormValues(values), isCreate: false), ct);
    }

    public async Task OnPostDeleteAsync(string id, CancellationToken ct)
    {
        Resource = ApiCatalog.Find(Key);
        if (!CanDelete)
        {
            Deny("You do not have delete permission for this resource.");
            return;
        }

        await SendAsync(HttpMethod.Delete, "/" + Uri.EscapeDataString(id ?? ""), null, ct);
        if (Resource is not null && !HasError)
            ResponseBody = Resource.DeleteSuccessMessage + Environment.NewLine + ResponseBody;
        if (!HasError)
            await SendAsync(HttpMethod.Get, "", null, ct);
    }

    public async Task OnPostBulkAsync(string bulkAction, [FromForm] List<string> selectedIds, CancellationToken ct)
    {
        Resource = ApiCatalog.Find(Key);
        if (Resource?.SupportsBulkActions != true)
            return;

        selectedIds = selectedIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (selectedIds.Count == 0)
        {
            ErrorMessage = "Select at least one row.";
            await LoadLookupOptionsAsync(ct);
            return;
        }

        if (string.Equals(bulkAction, "export", StringComparison.OrdinalIgnoreCase))
        {
            if (!CanRead)
            {
                Deny("You do not have read permission for this resource.");
                return;
            }

            await SendAsync(HttpMethod.Post, "/bulk/export", new { ids = selectedIds }, ct);
            return;
        }

        if (string.Equals(bulkAction, "update", StringComparison.OrdinalIgnoreCase))
        {
            if (!CanUpdate)
            {
                Deny("You do not have update permission for this resource.");
                return;
            }

            if (string.IsNullOrWhiteSpace(BulkField))
            {
                ErrorMessage = "Choose a field to update.";
                await LoadLookupOptionsAsync(ct);
                return;
            }

            await SendAsync(HttpMethod.Patch, "/bulk", new { ids = selectedIds, field = BulkField, value = BulkValue }, ct);
            if (!HasError)
                await SendAsync(HttpMethod.Get, "", null, ct);
            return;
        }

        if (string.Equals(bulkAction, "delete", StringComparison.OrdinalIgnoreCase))
        {
            if (!CanDelete)
            {
                Deny("You do not have delete permission for this resource.");
                return;
            }

            await SendAsync(HttpMethod.Post, "/bulk/delete", new { ids = selectedIds }, ct);
            if (Resource is not null && !HasError)
                ResponseBody = Resource.DeleteSuccessMessage + Environment.NewLine + ResponseBody;
            if (!HasError)
                await SendAsync(HttpMethod.Get, "", null, ct);
        }
    }

    private async Task SendAsync(HttpMethod method, string suffix, object? body, CancellationToken ct)
    {
        Resource = ApiCatalog.Find(Key);
        if (Resource is null)
            return;
        EnsurePageSize();

        if (method == HttpMethod.Get && !_api.CanRead(Resource))
        {
            await LoadReadDeniedMessageAsync(ct);
            return;
        }

        CurrentPage = Math.Max(1, CurrentPage);
        PageSize = Math.Clamp(PageSize, 1, Resource.MaxPageSize);
        if (string.IsNullOrWhiteSpace(SortBy))
            SortBy = Resource.PrimaryKey;
        var requestPath = method == HttpMethod.Get && string.IsNullOrWhiteSpace(suffix)
            ? Resource.Route + BuildQueryString()
            : Resource.Route + suffix;
        var result = await _api.SendAsync(method, requestPath, body, ct);
        LastStatusCode = result.StatusCode;
        EndpointPerformance = FormatPerformance(method.Method, requestPath, result);
        ResponseBody = result.Body;
        ErrorMessage = result.Success ? "" : result.UserMessage;
        if (method == HttpMethod.Get && string.IsNullOrWhiteSpace(suffix))
        {
            if (result.Success)
                ApplyPagedRows(result.Body);
            else
            {
                Rows.Clear();
                TotalCount = 0;
            }
        }
        else
            Rows.Clear();
        await LoadLookupOptionsAsync(ct);
    }

    private async Task LoadReadDeniedMessageAsync(CancellationToken ct)
    {
        if (Resource is null)
            return;

        EnsurePageSize();
        var result = await _api.SendAsync(HttpMethod.Get, Resource.Route + BuildQueryString(), null, ct);
        LastStatusCode = result.StatusCode;
        EndpointPerformance = FormatPerformance("GET", Resource.Route + BuildQueryString(), result);
        ResponseBody = result.Body;
        ErrorMessage = string.IsNullOrWhiteSpace(result.UserMessage) ? "Access denied." : result.UserMessage;
        Rows.Clear();
        await LoadLookupOptionsAsync(ct);
    }

    private void EnsurePageSize()
    {
        if (Resource is null)
            return;

        PageSize = PageSize <= 0
            ? Resource.DefaultPageSize
            : Math.Clamp(PageSize, 1, Resource.MaxPageSize);
    }

    private static string FormatPerformance(string method, string path, ApiResult result) =>
        $"{method} {path} loaded in {FormatDuration(result.ElapsedMilliseconds)}";

    private static string FormatDuration(long elapsedMilliseconds) =>
        elapsedMilliseconds < 1000
            ? $"{elapsedMilliseconds} ms"
            : $"{elapsedMilliseconds / 1000d:0.00} s";

    private string BuildQueryString()
    {
        var values = new Dictionary<string, string?>
        {
            ["page"] = CurrentPage.ToString(),
            ["pageSize"] = PageSize.ToString(),
            ["search"] = SearchText,
            ["filterField"] = FilterField,
            ["filterValue"] = FilterValue,
            ["sortBy"] = SortBy,
            ["sortDirection"] = SortDirection
        };

        return "?" + string.Join("&", values
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value!)));
    }

    private JsonObject BuildBody(Dictionary<string, string> values, bool isCreate)
    {
        var body = new JsonObject();
        if (Resource is null)
            return body;

        foreach (var field in EditFields)
        {
            if (!values.TryGetValue(field.Name, out var raw) || string.IsNullOrWhiteSpace(raw))
                continue;

            body[field.Name] = ParseValue(raw, field.ClrType);
        }

        foreach (var field in AuditFields)
        {
            if (body.ContainsKey(field.Name))
                continue;

            var auditValue = ResolveAuditValue(field, isCreate);
            if (auditValue is not null)
                body[field.Name] = auditValue;
        }

        return body;
    }

    private Dictionary<string, string> ReadFormValues(Dictionary<string, string>? boundValues)
    {
        var result = new Dictionary<string, string>(boundValues ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var entry in Request.Form)
        {
            const string prefix = "values[";
            if (!entry.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !entry.Key.EndsWith("]", StringComparison.Ordinal))
                continue;

            var key = entry.Key[prefix.Length..^1];
            if (!string.IsNullOrWhiteSpace(key))
                result[key] = entry.Value.ToString();
        }

        return result;
    }

    private JsonNode? ResolveAuditValue(ApiField field, bool isCreate)
    {
        return field.AuditKind switch
        {
            "tenant" => CreateTypedJsonValue(_api.TenantId, field.ClrType),
            "createdBy" when isCreate => CreateTypedJsonValue(_api.UserId ?? _api.UserName, field.ClrType),
            "createdOn" when isCreate => CreateCurrentTimestampValue(field.ClrType),
            "modifiedBy" => CreateTypedJsonValue(_api.UserId ?? _api.UserName, field.ClrType),
            "modifiedOn" => CreateCurrentTimestampValue(field.ClrType),
            "isDeleted" when isCreate => JsonValue.Create(false),
            _ => null
        };
    }

    private static JsonNode? CreateCurrentTimestampValue(string type)
    {
        if (type == "DateOnly")
            return JsonValue.Create(DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"));
        if (type == "DateTime" || type == "DateTimeOffset")
            return JsonValue.Create(DateTimeOffset.UtcNow.ToString("O"));

        return null;
    }

    private static JsonNode? CreateTypedJsonValue(string? raw, string type)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        if ((type is "int" or "long" or "short" or "byte") && long.TryParse(raw, out var integer))
            return JsonValue.Create(integer);
        if ((type is "decimal" or "double" or "float") && decimal.TryParse(raw, out var number))
            return JsonValue.Create(number);
        if (type == "bool" && TryParseBool(raw, out var boolean))
            return JsonValue.Create(boolean);

        if (type is "Guid" or "string")
            return JsonValue.Create(raw);

        return null;
    }

    private static JsonNode? ParseValue(string raw, string type)
    {
        if ((type is "int" or "long" or "short" or "byte") && long.TryParse(raw, out var integer))
            return JsonValue.Create(integer);
        if ((type is "decimal" or "double" or "float") && decimal.TryParse(raw, out var number))
            return JsonValue.Create(number);
        if (type == "bool" && TryParseBool(raw, out var boolean))
            return JsonValue.Create(boolean);

        return JsonValue.Create(raw);
    }

    private async Task LoadLookupOptionsAsync(CancellationToken ct)
    {
        if (Resource is null || !CanRead)
            return;

        var lookups = EditFields
            .Select(field => new { Field = field, Lookup = ResolveLookup(field) })
            .Where(item => item.Lookup is not null)
            .ToList();
        if (lookups.Count == 0)
            return;

        var loaded = await Task.WhenAll(lookups.Select(async item =>
        {
            var lookup = item.Lookup!;
            var result = await _api.SendAsync(HttpMethod.Get, lookup.Route + "?page=1&pageSize=200", null, ct);
            if (!result.Success || string.IsNullOrWhiteSpace(result.Body))
                return (item.Field.Name, Options: Array.Empty<LookupOption>());

            return (item.Field.Name, Options: BuildLookupOptions(result.Body, lookup.ValueField));
        }));

        foreach (var item in loaded)
        {
            if (item.Options.Count > 0)
                LookupOptions[item.Name] = item.Options;
        }
    }

    private void ApplyPagedRows(string json)
    {
        Rows.Clear();
        var pageInfo = ParsePagedRows(json);
        Rows.AddRange(pageInfo.Rows);
        CurrentPage = pageInfo.Page;
        PageSize = pageInfo.PageSize;
        TotalCount = pageInfo.TotalCount;
    }

    private static IEnumerable<Dictionary<string, object?>> ParseRows(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<Dictionary<string, object?>>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = UnwrapApiResponseBody(doc.RootElement);
            var source = root.ValueKind == JsonValueKind.Array
                ? root
                : root.TryGetProperty("items", out var items) ? items : default;
            return source.ValueKind == JsonValueKind.Array
                ? ReadRows(source)
                : Array.Empty<Dictionary<string, object?>>();
        }
        catch (JsonException)
        {
            return Array.Empty<Dictionary<string, object?>>();
        }
    }

    public string Value(Dictionary<string, object?> row, string field) =>
        row.TryGetValue(field, out var value) ? value?.ToString() ?? "" : "";

    public string FormatChanges(string changesJson)
    {
        if (string.IsNullOrWhiteSpace(changesJson))
            return "";

        try
        {
            using var doc = JsonDocument.Parse(changesJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return changesJson;

            var changes = new List<string>();
            foreach (var change in doc.RootElement.EnumerateArray())
            {
                if (change.ValueKind != JsonValueKind.Object)
                    continue;

                var column = JsonText(change, "column");
                if (string.IsNullOrWhiteSpace(column))
                    continue;

                changes.Add($"{column}: {JsonText(change, "before")} -> {JsonText(change, "after")}");
            }

            return changes.Count == 0 ? "" : string.Join("; ", changes);
        }
        catch
        {
            return changesJson;
        }
    }

    private static string JsonText(JsonElement element, string name)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                return property.Value.ValueKind == JsonValueKind.Null ? "<null>" : property.Value.ToString();
        }

        return "";
    }

    private void Deny(string message)
    {
        ErrorMessage = message;
        ResponseBody = "";
        LastStatusCode = 403;
        Rows.Clear();
    }

    private ApiLookup? ResolveLookup(ApiField field)
    {
        if (field.HasLookup)
            return new ApiLookup(field.LookupResourceKey, field.LookupRoute, field.LookupValueField);

        var resource = ApiCatalog.Resources.FirstOrDefault(candidate =>
            Resource is not null &&
            !string.Equals(candidate.Key, Resource.Key, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.PrimaryKey, field.Name, StringComparison.OrdinalIgnoreCase));

        return resource is null
            ? null
            : new ApiLookup(resource.Key, resource.Route, resource.PrimaryKey);
    }

    private static IReadOnlyList<LookupOption> BuildLookupOptions(string json, string valueField)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var source = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement
                : doc.RootElement.TryGetProperty("items", out var wrappedItems) ? wrappedItems : default;
            if (source.ValueKind != JsonValueKind.Array)
                return Array.Empty<LookupOption>();

            var options = new List<LookupOption>();
            foreach (var item in source.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || !TryGetProperty(item, valueField, out var valueElement))
                    continue;

                var value = valueElement.ToString() ?? "";
                var label = ResolveLookupLabel(item, valueField, value);
                options.Add(new LookupOption(value, label));
            }

            return options;
        }
        catch
        {
            return Array.Empty<LookupOption>();
        }
    }

    private static PageEnvelope ParsePagedRows(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new PageEnvelope(new List<Dictionary<string, object?>>(), 1, 50, 0);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = UnwrapApiResponseBody(doc.RootElement);
            if (root.ValueKind == JsonValueKind.Array)
            {
                var rows = ReadRows(root);
                return new PageEnvelope(rows, 1, rows.Count, rows.Count);
            }

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("items", out var items) ||
                items.ValueKind != JsonValueKind.Array)
            {
                return new PageEnvelope(new List<Dictionary<string, object?>>(), 1, 50, 0);
            }

            var page = ReadInt(root, "page", 1);
            var pageSize = ReadInt(root, "pageSize", 50);
            var totalCount = ReadInt(root, "totalCount", items.GetArrayLength());
            return new PageEnvelope(ReadRows(items), page, pageSize, totalCount);
        }
        catch (JsonException)
        {
            return new PageEnvelope(new List<Dictionary<string, object?>>(), 1, 50, 0);
        }
    }

    private static JsonElement UnwrapApiResponseBody(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("body", out var body))
            return body;

        return root;
    }

    private static List<Dictionary<string, object?>> ReadRows(JsonElement source)
    {
        var rows = new List<Dictionary<string, object?>>();
        foreach (var item in source.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            rows.Add(item.EnumerateObject().ToDictionary(property => property.Name, property => (object?)property.Value.ToString(), StringComparer.OrdinalIgnoreCase));
        }

        return rows;
    }

    private static int ReadInt(JsonElement element, string name, int fallback)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                continue;

            if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out var number))
                return number;
            if (property.Value.ValueKind == JsonValueKind.String && int.TryParse(property.Value.GetString(), out number))
                return number;
        }

        return fallback;
    }

    private static bool TryGetProperty(JsonElement item, string name, out JsonElement value)
    {
        if (item.TryGetProperty(name, out value))
            return true;

        foreach (var property in item.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
        }

        value = default;
        return false;
    }

    private static string ResolveLookupLabel(JsonElement item, string valueField, string fallback)
    {
        foreach (var property in item.EnumerateObject())
        {
            if (string.Equals(property.Name, valueField, StringComparison.OrdinalIgnoreCase))
                continue;

            if (property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number)
            {
                var label = property.Value.ToString();
                if (!string.IsNullOrWhiteSpace(label))
                    return $"{label} ({fallback})";
            }
        }

        return fallback;
    }

    private static bool TryParseBool(string raw, out bool value)
    {
        if (bool.TryParse(raw, out value))
            return true;

        var tokens = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            if (bool.TryParse(token, out var parsed) && parsed)
            {
                value = true;
                return true;
            }
        }

        value = false;
        return false;
    }

    private sealed record ApiLookup(string ResourceKey, string Route, string ValueField);
    private sealed record PageEnvelope(List<Dictionary<string, object?>> Rows, int Page, int PageSize, int TotalCount);
}