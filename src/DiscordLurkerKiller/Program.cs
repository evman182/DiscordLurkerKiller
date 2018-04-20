using System;
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

        static void Main(string[] args)
        {
            var joinDateRetriever = new JoinDateRetriever(HttpClient, GuildId, DiscordBotToken);
            var joinDates = joinDateRetriever.GetJoinDates();
            
            var discordClient = new DiscordRestClient();
            
            discordClient.LoginAsync(TokenType.Bot, DiscordBotToken).Wait();
            var discordInactivityRetriever = new DiscordInactivityRetriever(discordClient, HttpClient, GuildId, PurgeLogChannelId);
            var lastActivityDates = discordInactivityRetriever.GetActivityDates();

            var accountsOlderThan30Days = lastActivityDates.Where(x => joinDates[x.Key] < DateTime.UtcNow.AddDays(-30)).ToList();

            var accountsToBeWarned = accountsOlderThan30Days
                .Where(x => x.Value < DateTime.UtcNow.AddDays(-30) && x.Value > DateTime.UtcNow.AddDays(-44))
                .Select(x => x.Key)
                .ToList();

            var accountsGoingTomorrow = accountsOlderThan30Days
                .Where(x => x.Value <= DateTime.UtcNow.AddDays(-44) && x.Value > DateTime.UtcNow.AddDays(-45))
                .Select(x => x.Key)
                .ToList();

            var accountsToKick = accountsOlderThan30Days
                .Where(x => x.Value <= DateTime.UtcNow.AddDays(-45))
                .Select(x => x.Key)
                .ToList();

            var discordAnnouncer = new DiscordAnnouncer(discordClient, GuildId, PurgeLogChannelId);
            discordAnnouncer.AnnounceWarnings(accountsToBeWarned);
            discordAnnouncer.AnnounceTomorrowsKicks(accountsGoingTomorrow);
            discordAnnouncer.AnnounceKicks(accountsToKick);
        }

        
    }
}
