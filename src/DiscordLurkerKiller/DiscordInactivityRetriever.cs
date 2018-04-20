using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using Discord;
using Discord.Rest;

namespace DiscordLurkerKiller
{
    public class DiscordInactivityRetriever
    {
        private readonly HttpClient _httpClient;
        private readonly ulong _idToBeg;
        private readonly RestTextChannel _purgeChannel;
        private const ulong SpoopyId = 109379894718234624;

        public DiscordInactivityRetriever(DiscordRestClient discordClient, HttpClient httpClient, ulong guildId, ulong channelId, ulong idToBeg)
        {
            _httpClient = httpClient;
            _idToBeg = idToBeg;
            _purgeChannel = discordClient.GetGuildAsync(guildId).Result.GetTextChannelAsync(channelId).Result;
        }

        public List<UserActivityInfo> GetActivityDates()
        {
            var fileUrl = GetActivityDataFileUrl();
            var inactivityData = DownloadInactivityData(fileUrl);
            var userActivityDates = ParseInactivityData(inactivityData);
            return userActivityDates;
        }

        private void BegForUserToRunFindOldCommand()
        {
            
            _purgeChannel.SendMessageAsync($"<@{_idToBeg}> Please enter **'findold 0** into this channel in the next hour").Wait();
        }

        private List<UserActivityInfo> ParseInactivityData(string inactivityData)
        {
            var activityInfos = new List<UserActivityInfo>();
            var userLines = inactivityData.Split('\n');
            for (int x = 2; x < userLines.Length; x++)
            {
                var userLine = userLines[x];
                var parsedLine = Regex.Match(userLine,
                    @"(.*#[0-9]{4})\s+([0-9]+)\s+([0-9]{4}-[0-9]{2}-[0-9]{2}).*([0-9]{4}-[0-9]{2}-[0-9]{2}).*");

                var id = ulong.Parse(parsedLine.Groups[2].Value);
                var lastSpokeDate = DateTime.Parse(parsedLine.Groups[3].Value);

                activityInfos.Add(new UserActivityInfo
                {
                    Id = id,
                    LastSpokeDate = lastSpokeDate
                });
            }

            return activityInfos;
        }

        private string DownloadInactivityData(string fileUrl)
        {
            return _httpClient.GetStringAsync(fileUrl).Result;
        }

        private string GetActivityDataFileUrl()
        {
            var stopTime = DateTime.Now.AddHours(1);
            var beggedYet = false;
            while (DateTime.Now <= stopTime)
            {
                var last100Messages = _purgeChannel.GetMessagesAsync().FlattenAsync().Result;

                var mostRecentFindOldResponse = last100Messages
                    .Where(m => m.Timestamp > DateTimeOffset.UtcNow.AddHours(-2) &&
                                m.Author.Id == SpoopyId &&
                                m.Attachments.Any())
                    .OrderByDescending(m => m.Timestamp)
                    .FirstOrDefault();

                if (mostRecentFindOldResponse != null)
                {
                    var file = mostRecentFindOldResponse.Attachments.First();
                    var fileUrl = file.Url;
                    return fileUrl;
                }

                if (!beggedYet)
                {
                    beggedYet = true;
                    BegForUserToRunFindOldCommand();
                }

                Thread.Sleep(3 * 60 * 1000);
            }
            
                throw new Exception("Failed to retrieve activity dates");
        }
    }
}