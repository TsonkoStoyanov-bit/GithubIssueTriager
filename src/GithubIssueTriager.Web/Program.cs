using GithubIssueTriager.Web.Components;
using GithubIssueTriager.Web.Services;
using Microsoft.FluentUI.AspNetCore.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddFluentUIComponents();

var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "https://localhost:5101";
builder.Services.AddHttpClient<TriageApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Skip HTTPS redirection inside containers, where Kestrel is typically
// bound to plain HTTP only (no dev cert) and the middleware would otherwise
// just log a harmless-but-noisy warning on every request.
var runningInContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
if (!runningInContainer)
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
