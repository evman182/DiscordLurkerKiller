using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using Discord;
using Discord.Rest;

namespace DiscordLurkerKiller
{
    public class DiscordInactivityRetriever
    {
        private readonly HttpClient _httpClient;
        private readonly RestTextChannel _purgeChannel;
        private const ulong SpoopyId = 109379894718234624;

        public DiscordInactivityRetriever(DiscordRestClient discordClient, HttpClient httpClient, ulong guildId, ulong channelId)
        {
            _httpClient = httpClient;
            _purgeChannel = discordClient.GetGuildAsync(guildId).Result.GetTextChannelAsync(channelId).Result;
        }

        public Dictionary<ulong, DateTime> GetActivityDates()
        {
            var fileUrl = GetActivityDataFileUrl();
            var inactivityData = DownloadInactivityData(fileUrl);
            var userActivityDates = ParseInactivityData(inactivityData);
            return userActivityDates;
        }

        private Dictionary<ulong, DateTime> ParseInactivityData(string inactivityData)
        {
            var dict = new Dictionary<ulong, DateTime>();
            var userLines = inactivityData.Split('\n');
            for (int x = 2; x < userLines.Length; x++)
            {
                var userLine = userLines[x];
                var parsedLine = Regex.Match(userLine,
                    @"(.*#[0-9]{4})\s+([0-9]+)\s+([0-9]{4}-[0-9]{2}-[0-9]{2}).*([0-9]{4}-[0-9]{2}-[0-9]{2}).*");

                var id = ulong.Parse(parsedLine.Groups[2].Value);
                var lastSpokeDate = DateTime.Parse(parsedLine.Groups[3].Value);

                dict.Add(id, lastSpokeDate);
            }

            return dict;
        }

        private string DownloadInactivityData(string fileUrl)
        {
            return _httpClient.GetStringAsync(fileUrl).Result;
        }

        private string GetActivityDataFileUrl()
        {
            var last100Messages = _purgeChannel.GetMessagesAsync().FlattenAsync().Result;

            var mostRecentFindOldResponse = last100Messages
                .Where(m => m.Timestamp > DateTimeOffset.UtcNow.AddDays(-1) &&
                            m.Author.Id == SpoopyId &&
                            m.Attachments.Any())
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefault();

            if (mostRecentFindOldResponse == null)
            {
                throw new Exception("Failed to retrieve activity dates");
            }

            var file = mostRecentFindOldResponse.Attachments.First();
            var fileUrl = file.Url;
            return fileUrl;
        }
    }
}