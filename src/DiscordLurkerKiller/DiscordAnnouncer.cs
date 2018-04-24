using System;
using System.Collections.Generic;
using System.Linq;
using Discord;
using Discord.Rest;

namespace DiscordLurkerKiller
{
    public class DiscordAnnouncer
    {
        private readonly DiscordRestClient _discordClient;
        private readonly ulong _guildId;
        private readonly RestTextChannel _purgeChannel;

        public DiscordAnnouncer(DiscordRestClient discordClient, ulong guildId, ulong purgeLogChannelId)
        {
            _discordClient = discordClient;
            _guildId = guildId;
            _purgeChannel = _discordClient.GetGuildAsync(guildId).Result.GetTextChannelAsync(purgeLogChannelId).Result;
        }

        private string GetUserName(ulong userId)
        {
            var user = _discordClient.GetGuildAsync(_guildId).Result.GetUserAsync(userId).Result;
            var nickName = user.Nickname;
            var nickNameString = !string.IsNullOrWhiteSpace(nickName) ? $" ({nickName})" : string.Empty;
            return $"{user.Username}#{user.Discriminator}{nickNameString}";
        }

        public void AnnounceWarnings(List<ulong> accountsToBeWarned)
        {
            var textToSend = "Warning Accounts:\n" + string.Join("\n", accountsToBeWarned.Where(a => !UserHasAlreadyBeenWarned(a)).Select(GetUserName).ToList());
            _purgeChannel.SendMessageAsync(textToSend).Wait();
        }

        public void AnnounceTomorrowsKicks(List<ulong> accountsGoingTomorrow)
        {
            var textToSend = "Kicking Tomorrow:\n" + string.Join("\n", accountsGoingTomorrow.Select(GetUserName).ToList());
            _purgeChannel.SendMessageAsync(textToSend).Wait();
        }

        public void AnnounceKicks(List<ulong> accountsToKick)
        {
            var textToSend = "Kicking now:\n" + string.Join("\n", accountsToKick.Select(GetUserName).ToList());
            _purgeChannel.SendMessageAsync(textToSend).Wait();
        }

        private bool UserHasAlreadyBeenWarned(ulong userId)
        {
            var dmMessages = _discordClient.GetDMChannelAsync(userId).Result?.GetMessagesAsync().FlattenAsync().Result;
            if (dmMessages == null)
                return false;
            return dmMessages.Any(d => d.Timestamp > DateTimeOffset.UtcNow.AddDays(-25));
        }
    }
}