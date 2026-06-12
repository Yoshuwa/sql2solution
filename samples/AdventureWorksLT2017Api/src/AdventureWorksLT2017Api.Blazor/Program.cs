using AdventureWorksLT2017Api.Blazor.Components;
using AdventureWorksLT2017Api.Blazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddDataProtection();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromHours(8);
});
builder.Services.AddScoped<ApiSession>();
var allowInvalidDevelopmentCertificates = builder.Environment.IsDevelopment() &&
    builder.Configuration.GetValue<bool>("Api:AllowInvalidDevelopmentCertificates");
builder.Services.AddHttpClient("Api", client =>
{
    var baseUrl = builder.Configuration["Api:BaseUrl"] ?? "https://localhost:5001/";
    client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    if (allowInvalidDevelopmentCertificates)
        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

    return handler;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.Run();