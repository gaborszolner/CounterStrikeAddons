global using Msg = SharedLibrary.Msg<global::DamageReport.DamageReport>;

using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Utils;
using SharedLibrary;

namespace DamageReport
{
    public class DamageReport : BasePlugin
    {
        public override string ModuleName => "DamageReport";
        public override string ModuleVersion => "1.0";
        public override string ModuleAuthor => "Sinistral";
        public override string ModuleDescription => "Shows damage dealt and received to a player upon death";

        public readonly string PluginPrefix = $"[DamageReport]";

        // attackerSlot -> victimSlot -> (Damage, Hits)
        private static readonly Dictionary<int, Dictionary<int, (int Damage, int Hits)>> _damageDealt = [];

        // slot -> player name (stored at time of damage event, handles bots too)
        private static readonly Dictionary<int, string> _playerNames = [];

        private static bool _isWarmup = false;
        private static Config _config = new();

        public override void Load(bool hotReload)
        {
            _config = Config.LoadConfig(Path.Combine(ModuleDirectory, "config.json"));
            SharedLibrary.Localizer.Initialize(_config.Language);

            RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
            RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            RegisterEventHandler<EventRoundAnnounceWarmup>(OnRoundAnnounceWarmup);
            RegisterEventHandler<EventWarmupEnd>(OnWarmupEnd);
        }

        private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
        {
            if (_isWarmup) return HookResult.Continue;

            var attacker = @event.Attacker;
            var victim = @event.Userid;

            if (attacker is null || !attacker.IsValid || victim is null || !victim.IsValid)
                return HookResult.Continue;

            if (attacker.Slot == victim.Slot)
                return HookResult.Continue;

            var damage = @event.DmgHealth;
            if (damage <= 0) return HookResult.Continue;

            var attackerSlot = attacker.Slot;
            var victimSlot = victim.Slot;

            _playerNames[attackerSlot] = attacker.IsBot ? $"BOT {attacker.PlayerName}" : attacker.PlayerName;
            _playerNames[victimSlot] = victim.IsBot ? $"BOT {victim.PlayerName}" : victim.PlayerName;

            if (!_damageDealt.ContainsKey(attackerSlot))
                _damageDealt[attackerSlot] = [];

            if (!_damageDealt[attackerSlot].ContainsKey(victimSlot))
                _damageDealt[attackerSlot][victimSlot] = (0, 0);

            var (d, h) = _damageDealt[attackerSlot][victimSlot];
            _damageDealt[attackerSlot][victimSlot] = (d + damage, h + 1);

            return HookResult.Continue;
        }

        private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
        {
            if (_isWarmup) return HookResult.Continue;

            var victim = @event.Userid;

            if (victim is null || !victim.IsValid || victim.IsBot)
                return HookResult.Continue;

            var victimSlot = victim.Slot;
            var killerSlot = @event.Attacker?.Slot;

            // Damage dealt by the dying player to others this round
            if (_damageDealt.TryGetValue(victimSlot, out var dealtMap) && dealtMap.Count > 0)
            {
                victim.PrintToChat($" {ChatColors.Default}{PluginPrefix} {ChatColors.Green}{Msg.Get("DealtHeader")}");
                foreach (var (targetSlot, data) in dealtMap.OrderByDescending(x => x.Value.Damage))
                {
                    var targetName = _playerNames.GetValueOrDefault(targetSlot, "?");
                    victim.PrintToChat($"  {ChatColors.White}» {ChatColors.Green}{targetName}{ChatColors.Default} — {ChatColors.Yellow}{data.Damage} {Msg.Get("Dmg")}{ChatColors.Default} ({data.Hits} {Msg.Get("Hits")})");
                }
            }

            // Damage received by the dying player from others this round
            var receivedFrom = _damageDealt
                .Where(kvp => kvp.Value.ContainsKey(victimSlot))
                .Select(kvp => (AttackerSlot: kvp.Key, Data: kvp.Value[victimSlot]))
                .OrderByDescending(x => x.Data.Damage)
                .ToList();

            if (receivedFrom.Count > 0)
            {
                victim.PrintToChat($" {ChatColors.Default}{PluginPrefix} {ChatColors.Red}{Msg.Get("ReceivedHeader")}");
                foreach (var (attackerSlot, data) in receivedFrom)
                {
                    var attackerName = _playerNames.GetValueOrDefault(attackerSlot, "?");
                    var killedYouSuffix = killerSlot.HasValue && attackerSlot == killerSlot.Value
                        ? $" {ChatColors.Red}{Msg.Get("KilledYou")}"
                        : string.Empty;
                    victim.PrintToChat($"  {ChatColors.White}» {ChatColors.Red}{attackerName}{ChatColors.Default} — {ChatColors.Yellow}{data.Damage} {Msg.Get("Dmg")}{ChatColors.Default} ({data.Hits} {Msg.Get("Hits")}){killedYouSuffix}");
                }
            }

            return HookResult.Continue;
        }

        private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            _damageDealt.Clear();
            _playerNames.Clear();
            return HookResult.Continue;
        }

        private HookResult OnRoundAnnounceWarmup(EventRoundAnnounceWarmup @event, GameEventInfo info)
        {
            _isWarmup = true;
            _damageDealt.Clear();
            _playerNames.Clear();
            return HookResult.Continue;
        }

        private HookResult OnWarmupEnd(EventWarmupEnd @event, GameEventInfo info)
        {
            _isWarmup = false;
            _damageDealt.Clear();
            _playerNames.Clear();
            return HookResult.Continue;
        }
    }
}
