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
        private static readonly string WarningText = ConfigurationManager.AppSettings["WarningText"];
        private static readonly List<ulong> SafeRoleIds = GetSafeRoleIds();
        private static readonly DiscordRestClient DiscordClient = new DiscordRestClient();
        private static readonly DateTime CurrentTime = DateTime.UtcNow;
        private static readonly List<ulong> SafeUserIds = GetSafeUserIds();
        private const int MinimumAccountAge = 90;
        private const int WarningHeadsUpNumberDays = 3;
        private const int DaysLurkingToKick = 30;


        static async Task Main(string[] args)
        {
            await DiscordClient.LoginAsync(TokenType.Bot, DiscordBotToken);
            var discordInactivityRetriever =
                new DiscordInactivityRetriever(DiscordClient, HttpClient, GuildId, PurgeLogChannelId, IdToBeg);
            var lastActivityInfo = await discordInactivityRetriever.GetActivityDatesAsync();

            var joinDateRetriever = new JoinDateRetriever(HttpClient, GuildId, DiscordBotToken);
            var joinDates = await joinDateRetriever.GetJoinDatesAsync();

            lastActivityInfo = lastActivityInfo.Where(i => joinDates.ContainsKey(i.Id)).ToList();
            lastActivityInfo.ForEach(i =>
            {
                var userInfo = joinDates[i.Id];
                i.JoinDate = userInfo.JoinDate;
                i.Roles = userInfo.Roles;
            });

            var unsafeUsers = lastActivityInfo.Where(i => RolesAreNotSafeRole(i.Roles)
                                                          && !SafeUserIds.Contains(i.Id)).ToList();

            var accountsToBeWarned = unsafeUsers
                .Where(x => TimeSinceLastSpoke(x.LastSpokeDate) > DaysLurkingToKick - WarningHeadsUpNumberDays
                            && UserIsOlderThanNumberOfDays(x, MinimumAccountAge))
                .Select(x => x.Id)
                .OrderBy(x => x)
                .ToList();

            var accountsGoingTomorrow = unsafeUsers
                .Where(x => TimeSinceLastSpoke(x.LastSpokeDate) > (DaysLurkingToKick - 1)
                            && TimeSinceLastSpoke(x.LastSpokeDate) <= DaysLurkingToKick
                            && UserIsOlderThanNumberOfDays(x, WarningHeadsUpNumberDays + MinimumAccountAge - 1))
                .Select(x => x.Id)
                .OrderBy(x => x)
                .ToList();

            var accountsToKick = unsafeUsers
                .Where(x => TimeSinceLastSpoke(x.LastSpokeDate) > DaysLurkingToKick
                            && UserIsOlderThanNumberOfDays(x, WarningHeadsUpNumberDays + MinimumAccountAge))
                .Select(x => x.Id)
                .OrderBy(x => x)
                .ToList();

            var discordAnnouncer = new DiscordAnnouncer(DiscordClient, GuildId, PurgeLogChannelId, WarningText);
            await discordAnnouncer.AnnounceAndSendWarningsAsync(accountsToBeWarned);
            await discordAnnouncer.AnnounceTomorrowsKicksAsync(accountsGoingTomorrow);
            await discordAnnouncer.AnnounceKicksAsync(accountsToKick);
            await discordAnnouncer.PerformKicksAsync(accountsToKick);

        }

        private static bool UserIsOlderThanNumberOfDays(UserActivityInfo userInfo, int days)
        {
            return userInfo.JoinDate < DateTime.UtcNow.AddDays(-1 * days);
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

        private static List<ulong> GetSafeUserIds()
        {
            var safeRoleIdsString = ConfigurationManager.AppSettings["SafeUserIds"];
            if (string.IsNullOrWhiteSpace(safeRoleIdsString))
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
