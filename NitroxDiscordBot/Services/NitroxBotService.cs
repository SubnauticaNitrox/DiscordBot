using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using NitroxDiscordBot.Configuration;
using NitroxDiscordBot.Core;

namespace NitroxDiscordBot.Services;

/// <summary>
///     Connects to the Discord API as a bot and provides an abstraction over the Discord API for other services.
/// </summary>
internal sealed class NitroxBotService : IHostedService, IDisposable
{
    private readonly DiscordSocketClient client;
    private readonly IOptionsMonitor<NitroxBotConfig> config;
    private readonly InteractionService interactionService;
    private readonly ILogger log;
    private readonly IServiceProvider serviceProvider;

    public NitroxBotService(IOptionsMonitor<NitroxBotConfig> config,
        ILogger<NitroxBotService> log,
        IServiceProvider serviceProvider)
    {
        this.config = config;
        this.log = log;
        this.serviceProvider = serviceProvider;
        client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
        });
        interactionService = new InteractionService(client, new InteractionServiceConfig
        {
            DefaultRunMode = RunMode.Async,
            UseCompiledLambda = true
        });
        client.Log += ClientLogReceived;
        client.Ready += ClientOnReady;
        client.JoinedGuild += ClientOnJoinedGuild;
        client.MessageReceived += BotOnMessageReceived;
    }

    public bool IsConnected => client.ConnectionState == ConnectionState.Connected;

    public void Dispose()
    {
        client.Log -= ClientLogReceived;
        client.Dispose();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await client.LoginAsync(TokenType.Bot, config.CurrentValue.Token);
        await client.StartAsync();
        await WaitForReadyAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await client.StopAsync();
    }

    private async Task ClientOnJoinedGuild(SocketGuild guild)
    {
        if (guild.Id != config.CurrentValue.GuildId)
        {
            log.UnexpectedBotJoinGuildAttempt(guild.Name, guild.Id, config.CurrentValue.GuildId);
            return;
        }
        await interactionService.RegisterCommandsToGuildAsync(guild.Id);
    }

    public event EventHandler<SocketMessage>? MessageReceived;

    private async Task ClientOnReady()
    {
        await interactionService.AddModulesAsync(Assembly.GetAssembly(typeof(NitroxBotService)), serviceProvider);
        await interactionService.RegisterCommandsToGuildAsync(config.CurrentValue.GuildId);

        client.InteractionCreated += async interaction =>
        {
            SocketInteractionContext ctx = new(client, interaction);
            try
            {
                await interactionService.ExecuteCommandAsync(ctx, serviceProvider);
            }
            catch (Exception ex)
            {
                log.UserInteractionError(ex, interaction.User.Id, interaction.User.Username);
            }
        };
    }

    private Task BotOnMessageReceived(SocketMessage message)
    {
        // Only handle messages from the expected guild (aka Discord server).
        if (message.GetGuildId() != config.CurrentValue.GuildId)
        {
            return Task.CompletedTask;
        }
        OnMessageReceived(message);
        return Task.CompletedTask;
    }

    public async Task DeleteOldMessagesAsync(ulong channelId,
        TimeSpan ageThreshold,
        CancellationToken cancellationToken)
    {
        static bool IsSuitableForBulkDelete(DateTimeOffset timestamp, TimeSpan ageThreshold)
        {
            var differenceFromNow = DateTimeOffset.UtcNow - timestamp;
            // Note: messages older than 2 weeks can't be bulk-deleted.
            return differenceFromNow >= ageThreshold && differenceFromNow.TotalDays <= 13;
        }

        var channel = await GetChannelAsync<IMessageChannel>(channelId);
        if (channel == null)
        {
            return;
        }

        var count = 0;
        log.StartingChannelCleanup(channelId, channel.Name);
        await foreach (IReadOnlyCollection<IMessage> buffer in channel
                           .GetMessagesAsync(DiscordConstants.EarliestSnowflakeId, Direction.After)
                           .WithCancellation(cancellationToken))
        {
            if (buffer == null || buffer.Count < 1)
            {
                continue;
            }
            // Messages from API seem to be in reverse chronological order. But in case the API changes, let's order it ourselves again.
            var chronologicalMessages = buffer.Reverse().OrderBy(m => m.Timestamp).ToArray();

            // 1. If channel supports bulk delete, do this first.
            if (channel is ITextChannel textChannel)
            {
                var messagesToBulkDelete = chronologicalMessages
                    .TakeWhile(m => IsSuitableForBulkDelete(m.Timestamp, ageThreshold)).ToArray();
                if (messagesToBulkDelete.Length > 0)
                {
                    var messagesSummary = string.Join(Environment.NewLine,
                        messagesToBulkDelete.Select(m =>
                            $@"{m.Timestamp}:{Environment.NewLine}{m.Content.Replace("\n", "\t" + Environment.NewLine)}"));
                    log.BulkDeletingMessages(messagesSummary);
                    await textChannel.DeleteMessagesAsync(messagesToBulkDelete);
                    count += messagesToBulkDelete.Length;

                    // Remove the already deleted messages for step 2.
                    chronologicalMessages = chronologicalMessages.Except(messagesToBulkDelete).ToArray();
                }
            }
            // 2. Remove remaining messages one-by-one (e.g. when channel does not support it or message(s) are older than 2 weeks).
            foreach (var message in chronologicalMessages)
            {
                if (message.Timestamp + ageThreshold < DateTimeOffset.UtcNow)
                {
                    log.DeletingMessage(message.Content, message.Timestamp);
                    await message.DeleteAsync();
                    count++;
                }
                else
                {
                    // Exit early instead of iterating all messages in channel.
                    goto chronologicallyDone;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        chronologicallyDone:

        if (count > 0)
        {
            log.CleanupSummary(count, ageThreshold, channel.Name);
        }
        else
        {
            log.CleanupDidNothingSummary(channel.Name);
        }
    }

    public async Task CreateOrUpdateMessage(ulong channelId, int index, Embed embed)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        var channel = await GetChannelAsync<IMessageChannel>(channelId);
        if (channel == null)
        {
            return;
        }

        // Send message if index is outside total messages.
        var messages = await GetMessagesAsync(channel, index + 2);
        if (index >= messages.Length)
        {
            await channel.SendMessageAsync(null, false, embed);
            return;
        }
        // If author of current message is different, we can't edit it.
        if (messages[index].Author.Id != client.CurrentUser?.Id)
        {
            log.UnableToModifyMessageByDifferentAuthor(index, messages[index].Author.Id,
                messages[index].Author.Username);
            return;
        }

        // Modify message that was authored by this bot to the new content.
        await channel.ModifyMessageAsync(messages[index].Id, props =>
        {
            props.Content = "";
            props.Embed = embed;
        });
    }

    public IEnumerable<SocketRole> GetRolesByIds(SocketGuild? guild, ArraySegment<ulong> roles)
    {
        if (guild == null)
        {
            yield break;
        }
        foreach (var role in guild.Roles)
        foreach (var roleId in roles)
            if (role.Id == roleId)
            {
                yield return role;
            }
    }

    public IEnumerable<SocketGuildUser> GetUsersWithAnyRoles(SocketGuild guild, ArraySegment<ulong> roles)
    {
        Dictionary<ulong, SocketGuildUser> result = [];
        foreach (var role in guild.Roles)
        foreach (var roleId in roles)
            if (role.Id == roleId)
            {
                foreach (var member in role.Members) result[member.Id] = member;
            }
        return result.Values;
    }

    public async Task<List<IGuildUser>> GetUsersByIdsAsync(IGuild? guild, ArraySegment<ulong> userIds)
    {
        if (guild == null)
        {
            return [];
        }
        if (userIds is [])
        {
            return [];
        }
        List<IGuildUser> users = [];
        foreach (var userId in userIds)
        {
            var user = await guild.GetUserAsync(userId);
            if (user != null)
            {
                users.Add(user);
            }
        }
        return users;
    }

    private async Task<IMessage[]> GetMessagesAsync(IMessageChannel channel, int limit = 100, bool sorted = true)
    {
        IEnumerable<IMessage> messages = await channel
            .GetMessagesAsync(DiscordConstants.EarliestSnowflakeId, Direction.After, limit).FlattenAsync();
        if (sorted)
        {
            messages = messages.OrderBy(m => m.Timestamp);
        }
        return messages.ToArray();
    }

    public async Task<T?> GetChannelAsync<T>(ulong channelId) where T : class, IChannel
    {
        var channel = await client.GetChannelAsync(channelId) as T;
        if (channel == null)
        {
            log.ChannelNotFound(typeof(T), channelId);
        }
        return channel;
    }

    public async Task WaitForReadyAsync(CancellationToken cancellationToken)
    {
        var enteredLoop = false;
        while (!cancellationToken.IsCancellationRequested && !IsConnected)
        {
            enteredLoop = true;
            await Task.Delay(100, cancellationToken);
        }

        // Wait some extra time before trying to use API
        if (enteredLoop && IsConnected)
        {
            await Task.Delay(1000, cancellationToken);
        }
    }

    /// <summary>
    ///     Handler that receives log messages from the Discord client API.
    /// </summary>
    private Task ClientLogReceived(LogMessage logEntry)
    {
        switch (logEntry)
        {
            case { Exception: not null and not OperationCanceledException }:
                log.LogError(logEntry.Exception, $"[{nameof(Discord)}.{nameof(Discord.Net).ToUpperInvariant()}] error");
                break;
            case { Message.Length: > 0 }:
                log.LogInformation($"[{nameof(Discord)}.{nameof(Discord.Net).ToUpperInvariant()}]: {logEntry.Message}");
                break;
        }

        return Task.CompletedTask;
    }

    private void OnMessageReceived(SocketMessage e)
    {
        MessageReceived?.Invoke(this, e);
    }
}