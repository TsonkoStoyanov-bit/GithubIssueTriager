using GithubIssueTriager.Web.Components;
using GithubIssueTriager.Web.Services;
using Microsoft.AspNetCore.DataProtection;
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

// Data Protection keys are ephemeral (in-memory) by default. In a container
// that means every restart generates a brand-new key ring, so any
// antiforgery cookie a browser still holds from before the restart can no
// longer be decrypted -- "key was not found in the key ring". Persisting
// keys to a mounted volume (see docker-compose.yml's web-keys volume) keeps
// the key ring stable across restarts/rebuilds.
var keysPath = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true"
    ? "/keys"
    : Path.Combine(builder.Environment.ContentRootPath, "DataProtection-Keys");
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
    .SetApplicationName("GithubIssueTriager.Web");

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
app.UseAntiforgery();

// .NET 9/10 serve framework static web assets (notably _framework/blazor.web.js,
// the script that boots the interactive Server circuit) through the static-assets
// endpoint manifest, not the old physical-file UseStaticFiles middleware. With only
// UseStaticFiles, wwwroot/app.css and RCL _content/* still resolve, but
// _framework/blazor.web.js 404s -> no circuit -> buttons/selects do nothing and
// "nothing shows" in the UI. MapStaticAssets serves all of them.
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
