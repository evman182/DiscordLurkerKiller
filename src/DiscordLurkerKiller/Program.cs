using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;

namespace DiscordLurkerKiller
{
    class Program
    {
        private static readonly ulong GuildId = ulong.Parse(ConfigurationManager.AppSettings["DiscordGuildId"]);
        private static readonly string DiscordBotToken = ConfigurationManager.AppSettings["DiscordBotToken"];
        private static readonly List<ulong> SafeRoleIds = GetSafeRoleIds();
        private static readonly DiscordRestClient DiscordClient = new DiscordRestClient();
        private static readonly DateTime CurrentTime = DateTime.UtcNow;
        private const int InactivityTimeoutDays = 90;

        static async Task Main(string[] args)
        {
            await DiscordClient.LoginAsync(TokenType.Bot, DiscordBotToken);
            var guild = await DiscordClient.GetGuildAsync(GuildId);
            var users = (await guild.GetUsersAsync().FlattenAsync()).ToList();

            var inactivityThreshold = CurrentTime.AddDays(-1 * InactivityTimeoutDays);
            var eligibleMembersToKick = users
                .Where(u => u.Id != guild.OwnerId
                        && !u.IsBot
                        && !u.RoleIds.Any(r => SafeRoleIds.Contains(r))
                        && u.JoinedAt.Value <= inactivityThreshold).ToDictionary(u => u.Id);

            var currentUser = await guild.GetCurrentUserAsync();
            var channels = await guild.GetTextChannelsAsync();

            foreach (var channel in channels)
            {
                var myPermissions = currentUser.GetPermissions(channel);

                // You may want to remove this if-block. This is to allow a test run if your bot initially does not have access to every channel
                if (!myPermissions.ViewChannel || !myPermissions.ReadMessageHistory)
                {
                    continue;
                }

                Console.WriteLine($"{DateTime.Now} Starting channel {channel.Name}");
                var firstMessage = (await channel.GetMessagesAsync(1).FlattenAsync()).SingleOrDefault();

                if (firstMessage == null)
                {
                    Console.WriteLine($"{DateTime.Now} Finished channel {channel.Name}, no messages in channel");
                    continue;
                }

                if (firstMessage.Timestamp < inactivityThreshold)
                {
                    Console.WriteLine($"{DateTime.Now} Finished channel {channel.Name}, no messages in last 90 days");
                    continue;
                }

                if (firstMessage is RestUserMessage firstUserMessage)
                {
                    await ProcessMessage(eligibleMembersToKick, firstUserMessage);
                }

                var getMessagesBeforeId = firstMessage.Id;
                var oldestMessageFound = false;
                var processedMessages = 1;
                while (true)
                {
                    var messages = (await channel.GetMessagesAsync(getMessagesBeforeId, Direction.Before).FlattenAsync());

                    // No more messages to process for channel
                    if (!messages.Any())
                    {
                        break;
                    }

                    foreach (var message in messages.Where(m => m is RestUserMessage).Cast<RestUserMessage>())
                    {
                        if (message.Timestamp < inactivityThreshold)
                        {
                            oldestMessageFound = true;
                            break;
                        }

                        await ProcessMessage(eligibleMembersToKick, message);

                        processedMessages++;
                    }

                    if (oldestMessageFound)
                    {
                        break;
                    }

                    getMessagesBeforeId = messages.Last().Id;
                }

                Console.WriteLine($"{DateTime.Now} Finished channel {channel.Name}, processed {processedMessages} messages");

            }

            var userStrings = eligibleMembersToKick.Values.Select(r =>
                r.Username + "#" + r.Discriminator +
                (string.IsNullOrWhiteSpace(r.Nickname)
                    ? string.Empty
                    : $" ({r.Nickname})"));
            var usersToKickString = string.Join(Environment.NewLine, userStrings.OrderBy(u => u));
            File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + $"KickList-{DateTime.Now.Ticks}.txt", usersToKickString, encoding: Encoding.UTF8);

            foreach (var mememberToKick in eligibleMembersToKick.Values)
            {
                await mememberToKick.KickAsync("Inactivity");
            }
        }

        private static async Task ProcessMessage(Dictionary<ulong, RestGuildUser> eligibleMembersToKick, RestUserMessage message)
        {
            eligibleMembersToKick.Remove(message.Author.Id);

            foreach (var reaction in message.Reactions.Keys)
            {
                var userReactions = await message.GetReactionUsersAsync(reaction, 100).FlattenAsync();
                foreach (var userReaction in userReactions)
                {
                    eligibleMembersToKick.Remove(userReaction.Id);
                }
            }
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
