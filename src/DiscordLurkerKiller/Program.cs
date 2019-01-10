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
        private static readonly List<ulong> SafeRoleIds = GetSafeRoleIds();
        private static readonly DiscordRestClient DiscordClient = new DiscordRestClient();
        private static readonly DateTime CurrentTime = DateTime.UtcNow;
        private static readonly List<ulong> SafeUserIds = GetSafeUserIds();
        private const int MinimumAccountAge = 60;
        private static readonly ulong MemberRoleId = ulong.Parse(ConfigurationManager.AppSettings["MemberRoleId"]);


        static async Task Main(string[] args)
        {
            await DiscordClient.LoginAsync(TokenType.Bot, DiscordBotToken);
            var guild = await DiscordClient.GetGuildAsync(GuildId);
            var users = await guild.GetUsersAsync().FlattenAsync();
            var usersWithoutMemberRole = users.Where(u => !u.IsBot && !u.RoleIds.Contains(MemberRoleId)).ToList();
            
            var joinDateRetriever = new JoinDateRetriever(HttpClient, GuildId, DiscordBotToken);
            var joinDates = await joinDateRetriever.GetJoinDatesAsync();

            var accountAgeBoundary = CurrentTime.AddDays(-1 * MinimumAccountAge);
            var usersToKick = usersWithoutMemberRole.Where(u => joinDates[u.Id] <= accountAgeBoundary).ToList();

            var userStrings = usersToKick.Select(r =>
                r.Username + "#" + r.Discriminator +
                (string.IsNullOrWhiteSpace(r.Nickname)
                    ? string.Empty
                    : $" ({r.Nickname})"));
            var usersToKickString = string.Join(Environment.NewLine, userStrings.OrderBy(u => u));
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
    }
}
