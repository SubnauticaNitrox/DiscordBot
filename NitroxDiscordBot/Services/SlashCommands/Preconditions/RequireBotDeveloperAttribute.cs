using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Options;
using NitroxDiscordBot.Configuration;

namespace NitroxDiscordBot.Services.SlashCommands.Preconditions;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class RequireBotDeveloperAttribute : PreconditionAttribute
{
    public override Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
    {
        if (context.User is not IGuildUser guildUser)
        {
            return Task.FromResult(PreconditionResult.FromError("Command must be used in a guild channel."));
        }
        if (services.GetService<IOptionsMonitor<NitroxBotConfig>>() is not { } config)
        {
            return Task.FromResult(PreconditionResult.FromError($"Unable to get configuration in {nameof(RequireBotDeveloperAttribute)} to check permission"));
        }
        ulong[] developerUserIds = config.CurrentValue.Developers;
        if (developerUserIds is null or [] || !developerUserIds.Contains(guildUser.Id))
        {
            return Task.FromResult(PreconditionResult.FromError("You're not a developer of this bot"));
        }
        return Task.FromResult<PreconditionResult>(PreconditionGroupResult.FromSuccess());
    }
}