using System;
using System.Collections.Generic;

namespace DiscordLurkerKiller
{
    public class DiscordMemberInfo
    {
        public DateTime JoinDate { get; set; }
        public HashSet<ulong> Roles { get; set; }
    }
}