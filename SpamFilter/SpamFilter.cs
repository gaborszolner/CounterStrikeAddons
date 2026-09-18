using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Linq;
using SharedLibrary;
using CounterStrikeSharp.API.Core.Attributes;

namespace SpamFilter
{
    public class SpamFilter : BasePlugin
    {
        public override string ModuleName => "SpamFilter";
        public override string ModuleVersion => "1.0";
        public override string ModuleAuthor => "Sinistral";
        public override string ModuleDescription => "Filters spammers and some cheaters";

        List<KeyValuePair<string, DateTime>> playerNameChanges = new();
        int spammerNameChangeLimit = 3; // Number of name changes allowed within the time window
        int spammerNameChangeTimeWindow = 30; // Time window in seconds

        public override void Load(bool hotReload)
        {
            RegisterEventHandler<EventPlayerConnect>(OnPlayerConnect);
            RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect, HookMode.Pre);
            RegisterEventHandler<EventPlayerChat>(OnPlayerChat);
            RegisterEventHandler<EventPlayerChangename>(OnPlayerChangename);
            RegisterEventHandler<EventRoundAnnounceWarmup>(OnRoundAnnounceWarmup);
        }

        private HookResult OnRoundAnnounceWarmup(EventRoundAnnounceWarmup @event, GameEventInfo info)
        {
            playerNameChanges.Clear();      
            return HookResult.Continue;
        }

        private HookResult OnPlayerChangename(EventPlayerChangename @event, GameEventInfo info)
        {
            var player = @event.Userid;
            var playerId = player?.AuthorizedSteamID?.SteamId2 ?? player?.GetHashCode().ToString() ?? "";
            var now = Utils.GetServerTime();

            playerNameChanges.Add(new KeyValuePair<string, DateTime>(playerId, now));

            playerNameChanges.RemoveAll(kv => (now - kv.Value).TotalSeconds > spammerNameChangeTimeWindow);

            var recentChangesForPlayer = playerNameChanges.Count(kv => kv.Key == playerId);
            Server.PrintToChatAll($"Player {player?.PlayerName} ({playerId}) has changed their name {recentChangesForPlayer} times within {spammerNameChangeTimeWindow}s.");

            if (recentChangesForPlayer > spammerNameChangeLimit)
            {
                Logger?.LogInformation($"Player {player?.PlayerName} ({playerId}) has changed their name {recentChangesForPlayer} times within {spammerNameChangeTimeWindow}s.");
                player?.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKBANADDED);
            }

            return HookResult.Continue;
        }

        private HookResult OnPlayerConnect(EventPlayerConnect @event, GameEventInfo info)
        {
            var player = @event.Userid;

            if (IsSpammer(player))
            {
                player?.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKBANADDED);
            }
            return HookResult.Continue;
        }

        [GameEventHandler(HookMode.Pre)]
        private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
        {
            var playerName = @event.Userid?.PlayerName.Trim().Replace("\r", "").Replace("\n", "");
            Logger?.LogInformation($"Player disconnect: {playerName} - SteamID2: {@event.Userid?.AuthorizedSteamID?.SteamId2}");

            if (IsSpammer(@event.Userid))
            {
                Logger?.LogInformation($"Player is a spammer: {playerName}");
                info.DontBroadcast = true;
            }
            return HookResult.Continue;
        }

        public HookResult OnPlayerChat(EventPlayerChat @event, GameEventInfo info)
        {
            var player = Utilities.GetPlayerFromUserid(@event.Userid);

            if (IsSpammer(player))
            {
                player?.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED);
                return HookResult.Handled;
            }
            return HookResult.Continue;
        }

        private static bool IsSpammer(CCSPlayerController? player)
        {
            if (player?.PlayerName is not null && Regex.Matches(player.PlayerName, "\r?\n").Count >= 2)
            {
                return true;
            }
            return false;
        }
    }
}
