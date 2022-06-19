using NitroxDiscordBot.Configuration;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add configuration providers
builder.Configuration.AddJsonFile("appsettings.json", true, true);
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Development.json", true, true);
}

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddOptions<NitroxBotConfig>().Bind(builder.Configuration).ValidateDataAnnotations();
builder.Services.AddOptions<ChannelCleanupConfig>().Bind(builder.Configuration).ValidateDataAnnotations();
builder.Services.AddOptions<MotdConfig>().Bind(builder.Configuration).ValidateDataAnnotations();
// builder.Services.AddSingleton<NitroxBotService>().AddHostedService(provider => provider.GetRequiredService<NitroxBotService>());
// builder.Services.AddHostedService<ChannelCleanupService>();
// builder.Services.AddHostedService<MotdService>();

WebApplication app = builder.Build();
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();