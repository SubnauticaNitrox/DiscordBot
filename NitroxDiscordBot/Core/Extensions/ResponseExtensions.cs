using Discord;
using Discord.WebSocket;
using NitroxDiscordBot.Services;
using static NitroxDiscordBot.Db.Models.AutoResponse;
using static NitroxDiscordBot.Db.Models.AutoResponse.Response;

namespace NitroxDiscordBot.Core.Extensions;

public static class ResponseExtensions
{
    public static async Task<(string Error, string[] Values)> ValidateAsync(this Response response, NitroxBotService bot, IGuild guild, string value)
    {
        char[] valueSplitChars = response.Type switch
        {
            Types.MessageRoles or Types.MessageUsers => [',', ' '],
            _ => [',']
        };
        string[] values = value.Split(valueSplitChars,
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (values is null or { Length: 0 })
        {
            return ("Response value must not be empty", []);
        }

        // Validate values are compatible with response type
        switch (response.Type)
        {
            case Types.MessageRoles:
                IEnumerable<SocketRole> roles = bot.GetRolesByIds(guild as SocketGuild,
                    values.OfParsable<ulong>().ToArray());
                string[] missingRoles = values.ExceptBy(roles.Select(u => u.Id.ToString()), s => s).ToArray();
                if (missingRoles.Any())
                {
                    return ($"The following role ids are missing from this server `{string.Join(", ", missingRoles)}`",
                        []);
                }
                break;
            case Types.MessageUsers:
                List<IGuildUser> users = await bot.GetUsersByIdsAsync(guild,
                    values.OfParsable<ulong>().ToArray());
                string[] missingUsers = values.ExceptBy(users.Select(u => u.Id.ToString()), s => s).ToArray();
                if (missingUsers.Any())
                {
                    return ($"The following user ids are missing from this server `{string.Join(", ", missingUsers)}`",
                        []);
                }
                break;
            default:
                return ($"Unsupported value `{value}` for filter type `{response.Type}`", []);
        }

        return (null, values);
    }
}