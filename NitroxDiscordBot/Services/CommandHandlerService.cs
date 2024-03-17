using Discord.Commands;
using Discord.WebSocket;
using NitroxDiscordBot.Core;
using NitroxDiscordBot.Services.Commands;

namespace NitroxDiscordBot.Services;

public class CommandHandlerService : DiscordBotHostedService
{
    private readonly CommandService commands;
    private readonly IServiceProvider serviceProvider;

    public CommandHandlerService(NitroxBotService bot, ILogger<CommandHandlerService> log,
        IServiceProvider serviceProvider) : base(bot, log)
    {
        this.serviceProvider = serviceProvider;
        commands = new CommandService();
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        Bot.MessageReceived += BotOnMessageReceived;
        await commands.AddModuleAsync<InfoCommandModule>(serviceProvider);
        Log.LogInformation("Now listening for Discord user commands");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        Bot.MessageReceived -= BotOnMessageReceived;
        await commands.RemoveModuleAsync<InfoCommandModule>();
    }

    private async void BotOnMessageReceived(object sender, SocketMessage rawMessage)
    {
        if (rawMessage is not SocketUserMessage message)
        {
            return;
        }

        int argumentPos = 0;
        if (!message.HasCharPrefix('?', ref argumentPos) || message.HasMentionPrefix(Bot.User, ref argumentPos) ||
            message.Author.IsBot)
        {
            return;
        }

        try
        {
            await HandleMessageAsCommandAsync(message, argumentPos);
        }
        catch (Exception ex)
        {
            Log.LogError(ex, @"Error while handling command '{MessageContent}' by user: '{MessageAuthor}'",
                message.CleanContent, message.Author);
        }
    }

    private async Task HandleMessageAsCommandAsync(SocketUserMessage message, int argumentPosition)
    {
        await commands.ExecuteAsync(Bot.CreateCommandContext(message), argumentPosition, serviceProvider);
    }
}