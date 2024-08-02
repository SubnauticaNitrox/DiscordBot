using Discord.WebSocket;

namespace NitroxDiscordBot.Core.Extensions;

public static class SocketMessageComponentDataExtensions
{
    public static ReadOnlySpan<char> GetCapturedHandleId(this SocketMessageComponentData data)
    {
        if (data is null)
        {
            return [];
        }
        ReadOnlySpan<char> customId = data.CustomId.AsSpan();
        return customId[..customId.IndexOf('-')];
    }

    public static ReadOnlySpan<char> GetCapturedCustomId(this SocketMessageComponentData data)
    {
        if (data is null)
        {
            return [];
        }
        ReadOnlySpan<char> customId = data.CustomId.AsSpan();
        return customId[(customId.IndexOf('-') + 1)..];
    }

    public static string[] GetValuesOrEmpty(this SocketMessageComponentData data)
    {
        return data.Values?.ToArray() ?? (data.Value == null
            ? []
            : data.Value.Split(", ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
    }
}