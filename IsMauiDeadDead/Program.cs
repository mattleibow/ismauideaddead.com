using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using IsMauiDeadDead;
using IsMauiDeadDead.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient for external requests
builder.Services.AddScoped(sp => new HttpClient());
builder.Services.AddScoped<IStatusService, StatusService>();

await builder.Build().RunAsync();
