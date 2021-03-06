﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;

namespace DiscordLurkerKiller
{
    public class DiscordAnnouncer
    {
        private readonly RestTextChannel _purgeChannel;
        private readonly RestGuild _guild;
        private readonly Dictionary<ulong, RestGuildUser> _userDict;
        private readonly string _warningText;

        public DiscordAnnouncer(DiscordRestClient discordClient, ulong guildId, ulong purgeLogChannelId, string warningText)
        {
            _warningText = warningText;
            _guild = discordClient.GetGuildAsync(guildId).Result;
            _purgeChannel = _guild.GetTextChannelAsync(purgeLogChannelId).Result;
            _userDict = _guild.GetUsersAsync().FlattenAsync().Result.ToDictionary(u => u.Id, u => u);
        }

        private string GetUserName(ulong userId)
        {
            var user = _userDict[userId];
            var nickName = user.Nickname;
            var nickNameString = !string.IsNullOrWhiteSpace(nickName) ? $" ({nickName})" : string.Empty;
            return $"{user.Username}#{user.Discriminator}{nickNameString}";
        }

        public async Task AnnounceAndSendWarningsAsync(List<ulong> accountsToBeWarned)
        {
            accountsToBeWarned = accountsToBeWarned.Where(a => !UserHasAlreadyBeenWarned(a)).ToList();
            foreach (var chunkOf50ToBeWarned in SplitList(accountsToBeWarned, 50).ToList())
            {
                var textToSend = "Warning Accounts:\n" + string.Join("\n", chunkOf50ToBeWarned.Select(GetUserName).ToList());
                var splitTextToSend = SplitTextInto2000CharChunk(textToSend);
                foreach (var text in splitTextToSend)
                {
                    await _purgeChannel.SendMessageAsync(text);
                }

                await SendWarningsAsync(chunkOf50ToBeWarned);
                await _purgeChannel.SendMessageAsync("Batch finished. Sleeping for 15 minutes. ZZzzz");
                Thread.Sleep(15 * 60 * 1000); //15 Minutes
            }
        }

        private static List<string> SplitTextInto2000CharChunk(string textToSend)
        {
            if(textToSend.Length <= 2000)
                return new List<string>{textToSend};

            var l = new List<string>();
            while (textToSend.Length > 0)
            {
                if (textToSend.Length <= 2000)
                {
                    l.Add(textToSend);
                    break;
                }

                var nextBreak = textToSend.Substring(0, 2000).LastIndexOf("\n");
                l.Add(textToSend.Substring(0, nextBreak + 1));
                textToSend = textToSend.Substring(nextBreak + 1);
            }

            return l;
        }

        private static IEnumerable<List<T>> SplitList<T>(List<T> list, int nSize)
        {
            for (int i = 0; i < list.Count; i += nSize)
            {
                yield return list.GetRange(i, Math.Min(nSize, list.Count - i));
            }
        }

        public async Task AnnounceTomorrowsKicksAsync(List<ulong> accountsGoingTomorrow)
        {
            var textToSend = "Kicking Tomorrow:\n" + string.Join("\n", accountsGoingTomorrow.Select(GetUserName).ToList());
            await _purgeChannel.SendMessageAsync(textToSend);
        }

        public async Task AnnounceKicksAsync(List<ulong> accountsToKick)
        {
            accountsToKick = accountsToKick.Where(UserHasAlreadyBeenWarned).ToList();
            var textToSend = "Kicking now:\n" + string.Join("\n", accountsToKick.Select(GetUserName).ToList());
            await _purgeChannel.SendMessageAsync(textToSend);
        }

        public async Task SendWarningsAsync(List<ulong> accountsToBeWarned)
        {
            foreach (var account in accountsToBeWarned)
            {
                if (_userDict.TryGetValue(account, out var user))
                {
                    if (user != null)
                    {
                        try
                        {
                            var dm = await user.GetOrCreateDMChannelAsync();
                            if (dm != null)
                            {
                                await dm.SendMessageAsync(_warningText);
                            }
                        }
                        catch (Exception ex)
                        {
                            var exceptionMessage = ex.Message.Substring(0, Math.Min(ex.Message.Length, 2000));
                            await _purgeChannel.SendMessageAsync($"Could not send Lurker warning to user {user}: {exceptionMessage}");
                        }
                    }
                }
            }
        }

        // User has been DMed in last 10 days
        private bool UserHasAlreadyBeenWarned(ulong userId)
        {
            if (_userDict.TryGetValue(userId, out var user))
            {
                if (user != null)
                {
                    var dm = user.GetOrCreateDMChannelAsync().Result;
                    if (dm != null)
                    {
                        var dmMessages = dm.GetMessagesAsync().FlattenAsync().Result;
                        if (dmMessages != null)
                        {
                            var now = DateTimeOffset.UtcNow;
                            return dmMessages.Any(d => (now - d.Timestamp).TotalDays < 10);
                        }
                    }
                }
            }

            return false;
        }

        public async Task PerformKicksAsync(List<ulong> accountsToKick)
        {
            foreach (var a in accountsToKick)
            {
                if (_userDict.TryGetValue(a, out var user))
                {
                    await user.KickAsync("Inactivity");
                }
            }
        }
    }
}
