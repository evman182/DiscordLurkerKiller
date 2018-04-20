using System;
using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace DiscordLurkerKiller
{
    public class JoinDateRetriever
    {
        private readonly ulong _guildId;
        private readonly string _botToken;
        private readonly HttpClient _httpClient;
        private const string GuildMembersApiUrl = @"https://discordapp.com/api/guilds/{0}/members?limit=1000";

        public JoinDateRetriever(HttpClient httpClient, ulong guildId, string botToken)
        {
            _guildId = guildId;
            _botToken = botToken;
            _httpClient = httpClient;
        }

        public Dictionary<ulong, DateTime> GetJoinDates()
        {
            var joinDatesJson = GetJoinDatesJson();
            var joinDateDictionary = GetDictionaryFromJoinDateJson(joinDatesJson);
            return joinDateDictionary;
        }

        private Dictionary<ulong, DateTime> GetDictionaryFromJoinDateJson(string json)
        {
            var array = JArray.Parse(json);
            var dict = new Dictionary<ulong, DateTime>();
            foreach (var member in array)
            {
                var user = member["user"];
                var id = ulong.Parse(user.Value<string>("id"));
                var joinDate = DateTime.Parse(member.Value<string>("joined_at"));

                dict.Add(id, joinDate);
            }

            return dict;
        }

        private string GetJoinDatesJson()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, string.Format(GuildMembersApiUrl, _guildId));
            request.Headers.Clear();
            request.Headers.Add("Authorization", $"Bot {_botToken}");
            request.Headers.Add("User-Agent", "DiscordBot (DiscordLurkerKiller, 1.0)");
            var response = _httpClient.SendAsync(request).Result;
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Error retrieving join dates: {response.ReasonPhrase}");
            var responseBody = response.Content.ReadAsStringAsync().Result;
            return responseBody;
        }
    }
}