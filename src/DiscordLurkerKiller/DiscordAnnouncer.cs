using System;
using System.Collections.Generic;
using System.Linq;
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
        //private readonly Dictionary<ulong, string>  _roleDictionary;
        //private readonly ulong _everyoneRole;

        public DiscordAnnouncer(DiscordRestClient discordClient, ulong guildId, ulong purgeLogChannelId)
        {
            _guild = discordClient.GetGuildAsync(guildId).Result;
            _purgeChannel = _guild.GetTextChannelAsync(purgeLogChannelId).Result;
            _userDict = _guild.GetUsersAsync().FlattenAsync().Result.ToDictionary(u => u.Id, u => u);
            //_roleDictionary = _guild.Roles.ToDictionary(r => r.Id, r => r.Name);
            //_everyoneRole = _guild.EveryoneRole.Id;
        }

        private string GetUserName(ulong userId)
        {
            var user = _userDict[userId];
            var nickName = user.Nickname;
            var nickNameString = !string.IsNullOrWhiteSpace(nickName) ? $" ({nickName})" : string.Empty;
            return $"{user.Username}#{user.Discriminator}{nickNameString}";
            //var userRoles = string.Join("|", user.RoleIds.Where(r => r != _everyoneRole).Select(r => _roleDictionary[r]));
            //return $"{user.Username}#{user.Discriminator}{nickNameString} ({userRoles})";
        }

        public async Task AnnounceAndSendWarningsAsync(List<ulong> accountsToBeWarned)
        {
            accountsToBeWarned = accountsToBeWarned.Where(a => !UserHasAlreadyBeenWarned(a)).ToList();
            var textToSend = "Warning Accounts:\n" + string.Join("\n", accountsToBeWarned.Select(GetUserName).ToList());
            await _purgeChannel.SendMessageAsync(textToSend);
            await SendWarningsAsync(accountsToBeWarned);
        }

        public async Task AnnounceTomorrowsKicksAsync(List<ulong> accountsGoingTomorrow)
        {
            var textToSend = "Kicking Tomorrow:\n" + string.Join("\n", accountsGoingTomorrow.Select(GetUserName).ToList());
            await _purgeChannel.SendMessageAsync(textToSend);
        }

        public async Task AnnounceKicksAsync(List<ulong> accountsToKick)
        {
            var textToSend = "Kicking now:\n" + string.Join("\n", accountsToKick.Select(GetUserName).ToList());
            await _purgeChannel.SendMessageAsync(textToSend);
        }

        public async Task SendWarningsAsync(List<ulong> accountsToBeWarned)
        {
            const string warningText =
                @"Hi, you have been inactive on the rNycMeetups server for a while. If you don't start participating in the next few days you'll be kicked from the server. Don't worry though, it's not a ban, and you can always come back using the invite link on the subreddit sidebar. This bot won't respond to replies, so if you have any questions you should ask the mods on the server.";
            foreach (var account in accountsToBeWarned)
            {
                var user = await _guild.GetUserAsync(account);
                if (user != null)
                {
                    var dm = await user.GetOrCreateDMChannelAsync();
                    if (dm != null)
                    {
                        await dm.SendMessageAsync(warningText);
                    }
                }
            }
        }

        // User has been DMed in last 10 days
        private bool UserHasAlreadyBeenWarned(ulong userId)
        {
            var user = _guild.GetUserAsync(userId).Result;
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

            return false;
        }

        public async Task PerformKicksAsync(List<ulong> accountsToKick)
        {
            foreach (var a in accountsToKick)
            {
                var user = await _guild.GetUserAsync(a);
                await user.KickAsync();
            }
        }
    }
}