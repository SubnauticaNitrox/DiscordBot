using NitroxDiscordBot.Configuration;
using NitroxDiscordBot.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
IServiceCollection services = builder.Services;
ConfigurationManager config = builder.Configuration;

// Add configuration providers
config.AddJsonFile("appsettings.json", true, true);
if (builder.Environment.IsDevelopment())
{
    config.AddJsonFile("appsettings.Development.json", true, true);
}
services.AddOptions<NitroxBotConfig>().Bind(config).ValidateDataAnnotations();
services.AddOptions<ChannelCleanupConfig>().Bind(config).ValidateDataAnnotations();
services.AddOptions<MotdConfig>().Bind(config).ValidateDataAnnotations();

// Standard services for blazer server SPA
services.AddRazorPages();
services.AddServerSideBlazor();
// --- Custom services ---
services.AddLogging(opt => opt.AddSimpleConsole(c => c.TimestampFormat = "HH:mm:ss.fff "));
// TODO Discord integration: services.AddScoped<AuthenticationStateProvider, DiscordAuthenticationStateProvider>();
services.AddSingleton<NitroxBotService>().AddHostedService(provider => provider.GetRequiredService<NitroxBotService>());
services.AddHostedService<ChannelCleanupService>();
// services.AddHostedService<MotdService>();

// Configure the HTTP request pipeline.
WebApplication app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
