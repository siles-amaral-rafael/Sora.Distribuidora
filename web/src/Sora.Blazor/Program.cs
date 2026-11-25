using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Sora.Blazor;
using Sora.Blazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var configuredApiBase = builder.Configuration["ApiBaseUrl"];
var apiBaseAddress = Uri.TryCreate(configuredApiBase, UriKind.Absolute, out var absoluteApiBase) &&
                     absoluteApiBase.Scheme is "http" or "https"
    ? absoluteApiBase
    : new Uri(new Uri(builder.HostEnvironment.BaseAddress), configuredApiBase ?? "");
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = apiBaseAddress });
builder.Services.AddScoped<ApiSession>();
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<UiPreferencesService>();

await builder.Build().RunAsync();
