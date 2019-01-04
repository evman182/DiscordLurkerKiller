using System;
using System.Collections.Generic;

namespace DiscordLurkerKiller
{
    public class UserActivityInfo
    {
        public ulong Id { get; set; }
        public DateTime JoinDate { get; set; }
        public DateTime LastSpokeDate { get; set; }
        public HashSet<ulong> Roles { get; set; }
    }
}