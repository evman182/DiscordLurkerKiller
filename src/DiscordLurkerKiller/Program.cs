using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
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
        private static readonly DateTime CurrentTime = DateTime.UtcNow;
        private const int MinimumAccountAge = 30;
        private const int DaysLurkingToWarn = 27;
        private const int DaysLurkingToKick = 30;


        static async Task Main(string[] args)
        {
            await DiscordClient.LoginAsync(TokenType.Bot, DiscordBotToken);
            var discordInactivityRetriever =
                new DiscordInactivityRetriever(DiscordClient, HttpClient, GuildId, PurgeLogChannelId, IdToBeg);
            var lastActivityInfo = await discordInactivityRetriever.GetActivityDatesAsync();

            var joinDateRetriever = new JoinDateRetriever(HttpClient, GuildId, DiscordBotToken);
            var joinDates = await joinDateRetriever.GetJoinDatesAsync();

            var unsafeUsersOlderThanMinimumAccountAge =
                lastActivityInfo.Where(x => UserIsUnsafeAndOlderThanMinimumAccountAge(joinDates, x)).ToList();

            var accountsToBeWarned = unsafeUsersOlderThanMinimumAccountAge
                .Where(x => TimeSinceLastSpoke(x.LastSpokeDate) > DaysLurkingToWarn)
                .Select(x => x.Id)
                .OrderBy(x => x)
                .ToList();

            var accountsGoingTomorrow = unsafeUsersOlderThanMinimumAccountAge
                .Where(x => TimeSinceLastSpoke(x.LastSpokeDate) > (DaysLurkingToKick - 1)
                            && TimeSinceLastSpoke(x.LastSpokeDate) <= DaysLurkingToKick)
                .Select(x => x.Id)
                .OrderBy(x => x)
                .ToList();

            var accountsToKick = unsafeUsersOlderThanMinimumAccountAge
                .Where(x => TimeSinceLastSpoke(x.LastSpokeDate) > DaysLurkingToKick)
                .Select(x => x.Id)
                .OrderBy(x => x)
                .ToList();

            var discordAnnouncer = new DiscordAnnouncer(DiscordClient, GuildId, PurgeLogChannelId);
            await discordAnnouncer.AnnounceAndSendWarningsAsync(accountsToBeWarned);
            await discordAnnouncer.AnnounceTomorrowsKicksAsync(accountsGoingTomorrow);
            await discordAnnouncer.AnnounceKicksAsync(accountsToKick);
            await discordAnnouncer.PerformKicksAsync(accountsToKick);

        }

        private static bool UserIsUnsafeAndOlderThanMinimumAccountAge(Dictionary<ulong, DiscordMemberInfo> joinDates, UserActivityInfo userActivityInfo)
        {
            if (!joinDates.ContainsKey(userActivityInfo.Id))
                return false;
            var userInfo = joinDates[userActivityInfo.Id];
            return userInfo.JoinDate < DateTime.UtcNow.AddDays(-1 * MinimumAccountAge) && RolesAreNotSafeRole(userInfo.Roles);
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

        private static double TimeSinceLastSpoke(DateTime lastSpoke)
        {
            var timeSinceLastSpoke = (CurrentTime - lastSpoke).TotalDays;
            return timeSinceLastSpoke;
        }
    }
}
