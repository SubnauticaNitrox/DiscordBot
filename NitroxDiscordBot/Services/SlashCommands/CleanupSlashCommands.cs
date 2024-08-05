﻿using System.Text;
using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using NitroxDiscordBot.Core;
using NitroxDiscordBot.Db;
using NitroxDiscordBot.Db.Models;
using NitroxDiscordBot.Services.SlashCommands.Preconditions;

namespace NitroxDiscordBot.Services.SlashCommands;

[RequireBotDeveloper(Group = "Permission")]
[RequireUserPermission(GuildPermission.Administrator, Group = "Permission")]
[Group("cleanup", "Configures periodic cleanup schedules on channels")]
public class CleanupSlashCommands : InteractionModuleBase
{
    private readonly NitroxBotService bot;
    private readonly BotContext db;

    public CleanupSlashCommands(NitroxBotService bot, BotContext db)
    {
        this.bot = bot;
        this.db = db;
    }

    [SlashCommand("create", "Creates a cleanup schedule to periodically remove old messages")]
    public async Task CreateCleanupAsync([ChannelTypes(ChannelType.Text)]IChannel channel)
    {
        if (db.Cleanups.Any(d => d.ChannelId == channel.Id))
        {
            await RespondAsync($"A cleanup schedule already exists for '{channel.Name}'", ephemeral: true);
            return;
        }

        await db.Cleanups.AddAsync(new Cleanup()
        {
            ChannelId = channel.Id
        });
        if (await db.SaveChangesAsync() < 1)
        {
            await RespondAsync($"Failed to make changes to the database. No cleanup schedule was added for channel '{channel.Name}'", ephemeral: true);
            return;
        }

        await RespondAsync($"Created cleanup schedule for {channel.Name}", ephemeral: true);
    }

    [SlashCommand("remove", "Removes cleanup schedules from the given channel")]
    public async Task RemoveCleanupsAsync([ChannelTypes(ChannelType.Text)] IChannel channel)
    {
        int schedulesDeleted = await db.Cleanups.Where(c => c.ChannelId == channel.Id).ExecuteDeleteAsync();
        if (schedulesDeleted > 0)
        {
            await RespondAsync($"Removed {schedulesDeleted} cleanup schedules for channel '{channel.Name}'", ephemeral: true);
        }
        else
        {
            await RespondAsync($"Nothing to remove. No cleanup schedules for channel '{channel.Name}'", ephemeral: true);
        }
    }

    [SlashCommand("list", "Shows active cleanup schedules")]
    public async Task ListAsync()
    {
        StringBuilder sb = new("Active cleanup schedules:");
        sb.AppendLine();
        await foreach (Cleanup definition in db.Cleanups.AsAsyncEnumerable())
        {
            ITextChannel channel = await bot.GetChannelAsync<ITextChannel>(definition.ChannelId);
            if (channel == null)
            {
                continue;
            }
            sb.Append("- Channel ")
                .Append(channel.Mention)
                .Append(" cleans up messages older than ")
                .Append(definition.AgeThreshold.TotalDays)
                .AppendLine(" days");
        }
        await RespondAsync(sb.ToString(), ephemeral: true, allowedMentions: DiscordConstants.NoMentions);
    }
}