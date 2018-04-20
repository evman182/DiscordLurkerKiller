using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using Discord;
using Discord.Rest;

namespace DiscordLurkerKiller
{
    class Program
    {
        private static readonly HttpClient HttpClient = new HttpClient();
        private static readonly ulong GuildId = ulong.Parse(ConfigurationManager.AppSettings["DiscordGuildId"]);
        private static readonly string DiscordBotToken = ConfigurationManager.AppSettings["DiscordBotToken"];
        private static readonly ulong PurgeLogChannelId = ulong.Parse(ConfigurationManager.AppSettings["PurgeChannelId"]);
        private static readonly ulong IdToBeg = ulong.Parse(ConfigurationManager.AppSettings["IdToBeg"]);
        private static readonly List<ulong> SafeRoleIds = GetSafeRoleIds();
        private static readonly DiscordRestClient DiscordClient = new DiscordRestClient();

        static void Main(string[] args)
        {

            DiscordClient.LoginAsync(TokenType.Bot, DiscordBotToken).Wait();
            var discordInactivityRetriever =
                new DiscordInactivityRetriever(DiscordClient, HttpClient, GuildId, PurgeLogChannelId, IdToBeg);
            var lastActivityInfo = discordInactivityRetriever.GetActivityDates();

            var joinDateRetriever = new JoinDateRetriever(HttpClient, GuildId, DiscordBotToken);
            var joinDates = joinDateRetriever.GetJoinDates();

            var unsafeLurkersOlderThan30Days = lastActivityInfo.Where(x => UserIsUnsafeLurker(joinDates, x)).ToList();

            var accountsToBeWarned = unsafeLurkersOlderThan30Days
                .Where(x => x.LastSpokeDate > DateTime.UtcNow.AddDays(-44))
                .Select(x => x.Id)
                .ToList();

            var accountsGoingTomorrow = unsafeLurkersOlderThan30Days
                .Where(x => x.LastSpokeDate <= DateTime.UtcNow.AddDays(-44) && x.LastSpokeDate > DateTime.UtcNow.AddDays(-45))
                .Select(x => x.Id)
                .ToList();

            var accountsToKick = unsafeLurkersOlderThan30Days
                .Where(x => x.LastSpokeDate <= DateTime.UtcNow.AddDays(-45))
                .Select(x => x.Id)
                .ToList();

            var discordAnnouncer = new DiscordAnnouncer(DiscordClient, GuildId, PurgeLogChannelId);
            discordAnnouncer.AnnounceWarnings(accountsToBeWarned);
            discordAnnouncer.AnnounceTomorrowsKicks(accountsGoingTomorrow);
            discordAnnouncer.AnnounceKicks(accountsToKick);
        }

        private static bool UserIsUnsafeLurker(Dictionary<ulong, DiscordMemberInfo> joinDates, UserActivityInfo userActivityInfo)
        {
            var userInfo = joinDates[userActivityInfo.Id];
            return userInfo.JoinDate < DateTime.UtcNow.AddDays(-30) &&
                   userActivityInfo.LastSpokeDate < DateTime.UtcNow.AddDays(-30) &&
                   RolesAreNotSafeRole(userInfo.Roles);
        }

        private static bool RolesAreNotSafeRole(HashSet<ulong> userRoles)
        {
            if (!SafeRoleIds.Any())
                return true;

            foreach (var safeRole in SafeRoleIds)
            {
                if (userRoles.Contains(safeRole))
                    return false;
            }

            return true;
        }

        private static List<ulong> GetSafeRoleIds()
        {
            var safeRoleIdsString = ConfigurationManager.AppSettings["SafeRoleIds"];
            if(string.IsNullOrWhiteSpace(safeRoleIdsString))
                return new List<ulong>();

            return safeRoleIdsString.Split(',').Select(ulong.Parse).ToList();
        }
    }
}
