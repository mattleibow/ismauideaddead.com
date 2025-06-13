using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using IsMauiDeadDead;
using IsMauiDeadDead.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient for external requests - back to simpler approach for Blazor WASM
builder.Services.AddScoped(sp => new HttpClient());
builder.Services.AddScoped<IStatusService, StatusService>();

await builder.Build().RunAsync();
