using Discord.Commands;
using Discord.WebSocket;
using NitroxDiscordBot.Core;
using NitroxDiscordBot.Services.Commands;

namespace NitroxDiscordBot.Services;

public class CommandHandlerService : DiscordBotHostedService
{
    private readonly IServiceProvider serviceProvider;
    private readonly CommandService commands;

    public CommandHandlerService(NitroxBotService bot, ILogger<CommandHandlerService> log, IServiceProvider serviceProvider) : base(bot, log)
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

    private void BotOnMessageReceived(object sender, SocketMessage rawMessage)
    {
        SocketUserMessage message = rawMessage as SocketUserMessage;
        if (message == null)
        {
            return;
        }
        int argumentPos = 0;
        if (!message.HasCharPrefix('?', ref argumentPos) || message.HasMentionPrefix(Bot.UserOfBot, ref argumentPos) || message.Author.IsBot)
        {
            return;
        }

        _ = HandleMessageAsCommandAsync(message, argumentPos)
            .ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    Log.LogError(task.Exception, "Error while handling command \'{MessageCleanContent}\' by user: \'{MessageAuthor}\'", message.CleanContent, message.Author);
                }
            });
    }

    private async Task HandleMessageAsCommandAsync(SocketUserMessage message, int argumentPosition)
    {
        await commands.ExecuteAsync(Bot.CreateCommandContext(message), argumentPosition, serviceProvider);
    }
}
