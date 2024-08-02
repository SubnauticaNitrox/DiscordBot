using Discord.WebSocket;

namespace NitroxDiscordBot.Core.Extensions;

public static class SocketModalDataExtensions
{
    public static ReadOnlySpan<char> GetCapturedHandleId(this SocketModalData data)
    {
        if (data is null)
        {
            return [];
        }
        ReadOnlySpan<char> customId = data.CustomId.AsSpan();
        return customId[..customId.IndexOf('-')];
    }

    public static ReadOnlySpan<char> GetCapturedCustomId(this SocketModalData data)
    {
        if (data is null)
        {
            return [];
        }
        ReadOnlySpan<char> customId = data.CustomId.AsSpan();
        return customId[(customId.IndexOf('-')+1)..];
    }
}