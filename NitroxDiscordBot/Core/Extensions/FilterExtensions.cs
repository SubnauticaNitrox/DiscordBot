using Discord;
using NitroxDiscordBot.Db.Models;
using NitroxDiscordBot.Services;
using static NitroxDiscordBot.Db.Models.AutoResponse.Filter;

namespace NitroxDiscordBot.Core.Extensions;

public static class FilterExtensions
{
    public static async Task<(string Error, string[] ParsedValues)> ValidateAsync(this AutoResponse.Filter filter, NitroxBotService bot, string value)
    {
        static string[] ParseAsFilterValues(Types type, string newFilterValue)
        {
            char[] valueSplitChars = type switch
            {
                Types.AnyChannel => [',', ' '],
                _ => [',']
            };
            string[] values = newFilterValue.Split(valueSplitChars,
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return values is null or { Length: 0 } ? [] : values;
        }

        string[] values = ParseAsFilterValues(filter.Type, value);
        if (values is [])
        {
            return ("Filter value must not be empty", []);
        }

        switch (filter.Type)
        {
            case Types.AnyChannel when values is [_, ..] &&
                                                           values.OfParsable<ulong>() is
                                                               [_, ..] channelIds:
                foreach (ulong channelId in channelIds)
                {
                    if (await bot.GetChannelAsync<ITextChannel>(channelId) == null)
                    {
                        return ($"No text channel was found that has id `{channelId}`", []);
                    }
                }
                break;
            case Types.UserJoinAge when values is [_] && TimeSpan.TryParse(values[0], out TimeSpan _):
                break;
            case Types.MessageWordOrder when values is [_, ..]:
                try
                {
                    values.CreateRegexesForAnyWordGroupInOrderInSentence();
                }
                catch (Exception ex)
                {
                    return ($"Error occurred parsing filter value filter type `{Types.MessageWordOrder.ToString()}`: **{ex.Message}**", []);
                }
                break;
            default:
                return ($"Unsupported value `{value}` for filter type `{filter.Type}`", []);
        }
        return (null, values);
    }
}