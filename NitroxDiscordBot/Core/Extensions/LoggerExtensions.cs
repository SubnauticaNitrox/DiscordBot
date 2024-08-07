using NitroxDiscordBot.Db.Models;

namespace NitroxDiscordBot.Core.Extensions;

public static partial class LoggerExtensions
{
    [LoggerMessage(LogLevel.Information,
        Message =
            $"{nameof(AutoResponse)} '{{AutoResponseName}}' triggered for message {{MessageUrl}} by user '{{DiscordUsername}}' with user id {{DiscordUserId}}")]
    public static partial void AutoResponseTriggered(this ILogger logger,
        string autoResponseName,
        string messageUrl,
        string discordUsername,
        ulong discordUserId);

    [LoggerMessage(LogLevel.Information, Message = "Running old messages cleanup on channel '{ChannelName}' with channel id {ChannelId}")]
    public static partial void StartingChannelCleanup(this ILogger logger, ulong channelId, string channelName);

    [LoggerMessage(LogLevel.Information, Message = "Deleting messages:\n{MessagesContent}")]
    public static partial void BulkDeletingMessages(this ILogger logger, string messagesContent);

    [LoggerMessage(LogLevel.Information, Message = "Deleting message: '{MessageContent}' with timestamp: {MessageTimestamp}")]
    public static partial void DeletingMessage(this ILogger logger, string messageContent, DateTimeOffset messageTimestamp);

    [LoggerMessage(LogLevel.Information,
        Message = "Deleted {Count} message(s) older than {Age} from channel '{ChannelName}'")]
    public static partial void CleanupSummary(this ILogger logger, int count, TimeSpan age, string channelName);

    [LoggerMessage(LogLevel.Information, Message = "Added/updated MOTD in channel #{ChannelId} at index {Index}")]
    public static partial void MotdChanged(this ILogger logger, int index, ulong channelId);

    [LoggerMessage(LogLevel.Information, Message = "Nothing needed to be deleted from channel '{ChannelName}'")]
    public static partial void CleanupDidNothingSummary(this ILogger logger, string channelName);

    [LoggerMessage(LogLevel.Warning, Message = "Unhandled filter type '{FilterType}' with value '{FilterValue}'")]
    public static partial void UnhandledFilterType(this ILogger logger,
        AutoResponse.Filter.Types filterType,
        string[] filterValue);

    [LoggerMessage(LogLevel.Warning, Message = "Unhandled response type '{ResponseType}' with value '{ResponseValue}'")]
    public static partial void UnhandledResponseType(this ILogger logger,
        AutoResponse.Response.Types responseType,
        string[] responseValue);

    [LoggerMessage(LogLevel.Warning, Message = "No channel of type {ChannelType} with id {ChannelId} exists. Please make sure you've configured the bot correctly.")]
    public static partial void ChannelNotFound(this ILogger logger, Type channelType, ulong channelId);

    [LoggerMessage(LogLevel.Error, Message = "Unable to modify message at index {Index} because it is authored by another user: '{DiscordUsername}' ({DiscordUserId})")]
    public static partial void UnableToModifyMessageByDifferentAuthor(this ILogger logger,
        int index,
        ulong discordUserId,
        string discordUsername);

    [LoggerMessage(LogLevel.Error,
        Message =
            "Tried sending DM report about message '{MessageUrl}' to moderator '{DiscordUsername}' but failed")]
    public static partial void DmReportError(this ILogger logger,
        Exception exception,
        string messageUrl,
        string discordUsername);

    [LoggerMessage(LogLevel.Error, Message = "Error while handling command '{MessageContent}' by user: '{DiscordUsername}' with id {DiscordUserId}")]
    public static partial void CommandError(this ILogger logger,
        Exception exception,
        string messageContent,
        ulong discordUserId,
        string discordUsername);

    [LoggerMessage(LogLevel.Error, Message = "Error occurred handling interaction from user {DiscordUserId}: '{DiscordUsername}'")]
    public static partial void UserInteractionError(this ILogger logger,
        Exception exception,
        ulong discordUserId,
        string discordUsername);
}