using NitroxDiscordBot.Configuration;
using NitroxDiscordBot.Core;
using NitroxDiscordBot.Services;

#if DEBUG
Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
#else
Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Production");
#endif

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

IServiceCollection services = builder.Services;
ConfigurationManager config = builder.Configuration;

// Configuration
config.AddJsonFile("appsettings.json", true, true);
if (builder.Environment.IsDevelopment())
{
    config.AddJsonFile("appsettings.Development.json", true, true);
}
// Validation
services.AddOptions<NitroxBotConfig>().Bind(config).ValidateDataAnnotations().ValidateOnStart();
services.AddOptions<ChannelCleanupConfig>().Bind(config).ValidateDataAnnotations().ValidateOnStart();
services.AddOptions<MotdConfig>().Bind(config).ValidateDataAnnotations().ValidateOnStart();

// Services
services.AddLogging(opt => opt.AddSimpleConsole(c => c.TimestampFormat = "HH:mm:ss.fff "));
// TODO: Discord integration: services.AddScoped<AuthenticationStateProvider, DiscordAuthenticationStateProvider>();
services.AddHostedSingleton<NitroxBotService>();
services.AddSingleton<CommandHandlerService>();
services.AddHostedSingleton<CommandHandlerService>();
services.AddHostedSingleton<ChannelCleanupService>();

// TODO: Create login page (with Discord OAUTH) and dashboard for managing the bot remotely.
// WebApplication app = builder.Build();
// if (!app.Environment.IsDevelopment())
// {
//     app.UseExceptionHandler("/Error");
//     // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
//     app.UseHsts();
// }
// app.UseHttpsRedirection();
// app.UseStaticFiles();
// app.UseRouting();
// app.UseAuthentication();
// app.UseAuthorization();
// app.MapBlazorHub();
// app.MapFallbackToPage("/_Host");

builder.Build().Run();
