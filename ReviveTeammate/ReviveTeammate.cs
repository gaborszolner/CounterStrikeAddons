global using Msg = SharedLibrary.Msg<global::ReviveTeammate.ReviveTeammate>;

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using SharedLibrary;
using System.Drawing;

namespace ReviveTeammate
{
    public class ReviveTeammate : BasePlugin
    {
        public override string ModuleName => "ReviveTeammate";
        public override string ModuleVersion => "1.0";
        public override string ModuleAuthor => "Sinistral";
        public override string ModuleDescription => "Allows alive teammates to revive recently dead players";

        public readonly string PluginPrefix = "[ReviveTeammate]";

        private static readonly Dictionary<ulong, (ulong TargetSteamId, DateTime StartTime)> _reviveProgress = new();
        private static readonly Dictionary<ulong, DateTime> _deathTimes = new();
        private static readonly Dictionary<ulong, (float X, float Y, float Z)> _deathPositions = new();
        private static readonly Dictionary<ulong, DateTime> _reviveWindowEndTime = new();
        private static readonly Dictionary<ulong, float> _reviveWindowFrozen = new();
        private static readonly Dictionary<ulong, HashSet<ulong>> _activeReviversForTarget = new();
        private static readonly HashSet<ulong> _revivedThisRound = new();
        private static readonly Dictionary<ulong, CPointWorldText> _deathMarkers = new();
        private static readonly Dictionary<ulong, CsTeam> _deathTeams = new();
        private static readonly Dictionary<ulong, int> _lastMarkerSeconds = new();

        private const float ReviveMaxRange = 150.0f;
        private const float ReviveAimCosThreshold = 0.85f; // ~32° cone

        private static bool _isWarmup = false;
        private static ReviveTeammateConfig _config = new();

        public override void Load(bool hotReload)
        {
            _config = ConfigLoader.Load<ReviveTeammateConfig>(ModuleDirectory, "ReviveTeammateConfig.json");
            SharedLibrary.Localizer.Initialize(_config.Language);

            RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
            RegisterEventHandler<EventPlayerChat>(OnPlayerChat);
            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            RegisterEventHandler<EventRoundAnnounceWarmup>(OnRoundAnnounceWarmup);
            RegisterEventHandler<EventWarmupEnd>(OnWarmupEnd);
            RegisterListener<Listeners.OnTick>(OnTick);
            RegisterListener<Listeners.CheckTransmit>(OnCheckTransmit);
        }

        public HookResult OnPlayerChat(EventPlayerChat @event, GameEventInfo info)
        {
            var player = Utilities.GetPlayerFromUserid(@event.Userid);

            if (player is null || !player.IsValid)
            {
                return HookResult.Continue;
            }

            if (@event?.Text.Trim().ToLower() is "!reload")
            {
                string adminsFilePath = Path.Combine(ModuleDirectory, "..", "..", "configs", "admins.json");
                if (PlayerHelper.GetAdminLevel(player, adminsFilePath) > 2)
                {
                    _config = ConfigLoader.Load<ReviveTeammateConfig>(ModuleDirectory, "ReviveTeammateConfig.json");
                    SharedLibrary.Localizer.Initialize(_config.Language);
                    player.PrintToChat($"{PluginPrefix} {Msg.Get("ConfigsReloaded")}");
                }
            }

            return HookResult.Continue;
        }

        private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            _reviveProgress.Clear();
            _deathTimes.Clear();
            _deathPositions.Clear();
            _reviveWindowEndTime.Clear();
            _reviveWindowFrozen.Clear();
            _activeReviversForTarget.Clear();
            _revivedThisRound.Clear();
            foreach (var marker in _deathMarkers.Values)
                if (marker.IsValid) marker.Remove();
            _deathMarkers.Clear();
            _deathTeams.Clear();
            _lastMarkerSeconds.Clear();
            return HookResult.Continue;
        }

        private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
        {
            var targetPlayer = @event.Userid;

            if (targetPlayer is not null && targetPlayer.IsValid)
            {
                _deathTimes[targetPlayer.SteamID] = Utils.GetServerTime();

                var pawn = targetPlayer.PlayerPawn.Value;
                if (pawn?.AbsOrigin != null)
                    _deathPositions[targetPlayer.SteamID] = (pawn.AbsOrigin.X, pawn.AbsOrigin.Y, pawn.AbsOrigin.Z);

                ulong steamId = targetPlayer.SteamID;
                var endTime = Utils.GetServerTime() + TimeSpan.FromSeconds(_config.ReviveDeathWindowSeconds);
                _reviveWindowEndTime[steamId] = endTime;
                AddTimer(_config.ReviveDeathWindowSeconds, () => NotifyReviveExpiredIfNeeded(steamId, endTime));
                _deathTeams[steamId] = targetPlayer.Team;
                if (!_isWarmup && _config.CanReviveTeammate && !_revivedThisRound.Contains(steamId))
                    CreateDeathMarker(steamId);
            }

            return HookResult.Continue;
        }

        private void OnTick()
        {
            if (!_config.CanReviveTeammate)
                return;

            UpdateDeathMarkers();

            foreach (var player in Utilities.GetPlayers()
                .Where(p => p.IsValid
                         && p.PawnIsAlive
                         && p.Team != CsTeam.Spectator
                         && p.Team != CsTeam.None)
                .ToList())
            {
                bool isHoldingUse = (player.Buttons & PlayerButtons.Use) != 0;

                if (!isHoldingUse)
                {
                    if (_reviveProgress.TryGetValue(player.SteamID, out var oldProg))
                        RemoveReviver(player.SteamID, oldProg.TargetSteamId);
                    _reviveProgress.Remove(player.SteamID);
                    continue;
                }

                var target = FindAimedDeadTeammate(player);
                if (target == null)
                {
                    if (_reviveProgress.TryGetValue(player.SteamID, out var oldProg))
                        RemoveReviver(player.SteamID, oldProg.TargetSteamId);
                    _reviveProgress.Remove(player.SteamID);
                    continue;
                }

                if (_reviveProgress.TryGetValue(player.SteamID, out var progress))
                {
                    // Switched aim to a different dead teammate — restart timer
                    if (progress.TargetSteamId != target.SteamID)
                    {
                        RemoveReviver(player.SteamID, progress.TargetSteamId);
                        AddReviver(player.SteamID, target.SteamID);
                        _reviveProgress[player.SteamID] = (target.SteamID, Utils.GetServerTime());
                        player.PrintToCenter(Msg.Get("ReviveProgressStart", target.PlayerName, _config.ReviveHoldDurationSeconds));
                    }
                    else
                    {
                        float elapsed = (float)(Utils.GetServerTime() - progress.StartTime).TotalSeconds;
                        float remaining = _config.ReviveHoldDurationSeconds - elapsed;

                        if (elapsed >= _config.ReviveHoldDurationSeconds)
                        {
                            _reviveProgress.Remove(player.SteamID);
                            ReviveDeadTeammate(player, target);
                        }
                        else
                        {
                            int filled = (int)(elapsed / _config.ReviveHoldDurationSeconds * 10);
                            string bar = new string('|', filled) + new string('.', 10 - filled);
                            player.PrintToCenter(Msg.Get("ReviveProgressBar", target.PlayerName, bar, $"{remaining:F1}"));
                        }
                    }
                }
                else
                {
                    AddReviver(player.SteamID, target.SteamID);
                    _reviveProgress[player.SteamID] = (target.SteamID, Utils.GetServerTime());
                    player.PrintToCenter(Msg.Get("ReviveProgressStart", target.PlayerName, _config.ReviveHoldDurationSeconds));
                }
            }
        }

        private static CCSPlayerController? FindAimedDeadTeammate(CCSPlayerController player)
        {
            var pawn = player.PlayerPawn.Value;
            if (pawn?.AbsOrigin == null || pawn.EyeAngles == null)
                return null;

            double pitchRad = pawn.EyeAngles.X * Math.PI / 180.0;
            double yawRad   = pawn.EyeAngles.Y * Math.PI / 180.0;

            float fwdX = (float)(Math.Cos(pitchRad) * Math.Cos(yawRad));
            float fwdY = (float)(Math.Cos(pitchRad) * Math.Sin(yawRad));
            float fwdZ = (float)(-Math.Sin(pitchRad));

            float eyeX = pawn.AbsOrigin.X;
            float eyeY = pawn.AbsOrigin.Y;
            float eyeZ = pawn.AbsOrigin.Z + 64.0f;

            CCSPlayerController? bestTarget = null;
            float bestDot = ReviveAimCosThreshold;

            foreach (var target in Utilities.GetPlayers()
                .Where(p => p.IsValid
                         && !p.PawnIsAlive
                         && p.Team == player.Team
                         && p.SteamID != player.SteamID
                         && p.Team != CsTeam.Spectator
                         && p.Team != CsTeam.None))
            {
                if (_revivedThisRound.Contains(target.SteamID))
                    continue;

                if (!_reviveWindowFrozen.ContainsKey(target.SteamID))
                {
                    if (!_reviveWindowEndTime.TryGetValue(target.SteamID, out var endTime)
                        || Utils.GetServerTime() > endTime)
                        continue;
                }

                var targetPawn = target.PlayerPawn.Value;
                if (targetPawn?.AbsOrigin == null)
                    continue;

                float dx = targetPawn.AbsOrigin.X - eyeX;
                float dy = targetPawn.AbsOrigin.Y - eyeY;
                float dz = targetPawn.AbsOrigin.Z - eyeZ;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);

                if (dist > ReviveMaxRange || dist < 0.01f)
                    continue;

                float dot = (dx * fwdX + dy * fwdY + dz * fwdZ) / dist;
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestTarget = target;
                }
            }

            return bestTarget;
        }

        private void ReviveDeadTeammate(CCSPlayerController reviverPlayer, CCSPlayerController targetPlayer)
        {
            if (!targetPlayer.IsValid || targetPlayer.PawnIsAlive)
                return;

            _reviveWindowEndTime.Remove(targetPlayer.SteamID);
            _reviveWindowFrozen.Remove(targetPlayer.SteamID);
            _activeReviversForTarget.Remove(targetPlayer.SteamID);
            _revivedThisRound.Add(targetPlayer.SteamID);
            RemoveDeathMarker(targetPlayer.SteamID);
            targetPlayer.Respawn();

            // Set HP after a short delay so Respawn() has time to fully execute
            AddTimer(0.1f, () =>
            {
                try
                {
                    if (!targetPlayer.IsValid || !targetPlayer.PawnIsAlive)
                        return;

                    var targetPawn = targetPlayer.PlayerPawn.Value;
                    if (targetPawn != null)
                    {
                        targetPawn.Health = _config.ReviveHP;
                        Utilities.SetStateChanged(targetPawn, "CBaseEntity", "m_iHealth");

                        if (_deathPositions.TryGetValue(targetPlayer.SteamID, out var deathPos))
                        {
                            targetPawn.Teleport(new Vector(deathPos.X, deathPos.Y, deathPos.Z));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogError($"{PluginPrefix} Error setting HP after revive: {ex.Message}");
                }
            });

            reviverPlayer.PrintToCenter(Msg.Get("ReviveSuccessful"));
            Server.PrintToChatAll(Msg.Get("PlayerRevived", reviverPlayer.PlayerName, targetPlayer.PlayerName, _config.ReviveHP));
        }

        private void AddReviver(ulong reviverSteamId, ulong targetSteamId)
        {
            if (!_activeReviversForTarget.TryGetValue(targetSteamId, out var set))
            {
                set = new HashSet<ulong>();
                _activeReviversForTarget[targetSteamId] = set;
            }

            if (set.Count == 0 && _reviveWindowEndTime.TryGetValue(targetSteamId, out var endTime))
            {
                float remaining = (float)(endTime - Utils.GetServerTime()).TotalSeconds;
                _reviveWindowFrozen[targetSteamId] = Math.Max(0f, remaining);
            }

            set.Add(reviverSteamId);
        }

        private void RemoveReviver(ulong reviverSteamId, ulong targetSteamId)
        {
            if (!_activeReviversForTarget.TryGetValue(targetSteamId, out var set))
                return;

            set.Remove(reviverSteamId);

            if (set.Count > 0)
                return;

            _activeReviversForTarget.Remove(targetSteamId);

            if (!_reviveWindowFrozen.TryGetValue(targetSteamId, out float frozenRemaining))
                return;

            _reviveWindowFrozen.Remove(targetSteamId);

            if (frozenRemaining <= 0)
                return;

            var newEndTime = Utils.GetServerTime() + TimeSpan.FromSeconds(frozenRemaining);
            _reviveWindowEndTime[targetSteamId] = newEndTime;

            AddTimer(frozenRemaining, () => NotifyReviveExpiredIfNeeded(targetSteamId, newEndTime));
        }

        private void NotifyReviveExpiredIfNeeded(ulong steamId, DateTime expectedEndTime)
        {
            if (!_reviveWindowEndTime.TryGetValue(steamId, out var currentEndTime) || currentEndTime != expectedEndTime)
                return;

            if (_reviveWindowFrozen.ContainsKey(steamId))
                return;

            var target = Utilities.GetPlayers().FirstOrDefault(p => p.IsValid && p.SteamID == steamId && !p.PawnIsAlive);
            if (target == null)
                return;

            target.PrintToCenter(Msg.Get("ReviveWindowExpired"));

            RemoveDeathMarker(steamId);
            _reviveWindowEndTime.Remove(steamId);
            _deathTimes.Remove(steamId);
            _deathPositions.Remove(steamId);
        }

        private void CreateDeathMarker(ulong steamId)
        {
            if (!_deathPositions.TryGetValue(steamId, out var pos))
                return;
            if (!_reviveWindowEndTime.TryGetValue(steamId, out var endTime))
                return;

            var deadPlayer = Utilities.GetPlayers().FirstOrDefault(p => p.IsValid && p.SteamID == steamId);
            int seconds = (int)Math.Ceiling((endTime - Utils.GetServerTime()).TotalSeconds);

            var marker = Utilities.CreateEntityByName<CPointWorldText>("point_worldtext");
            if (marker == null || !marker.IsValid)
                return;

            marker.MessageText = $"{seconds}s";
            marker.Enabled = true;
            marker.Fullbright = true;
            marker.WorldUnitsPerPx = 0.25f;
            marker.FontSize = 40;
            marker.Color = Color.Yellow;
            marker.JustifyHorizontal = PointWorldTextJustifyHorizontal_t.POINT_WORLD_TEXT_JUSTIFY_HORIZONTAL_CENTER;
            marker.JustifyVertical = PointWorldTextJustifyVertical_t.POINT_WORLD_TEXT_JUSTIFY_VERTICAL_CENTER;
            marker.ReorientMode = PointWorldTextReorientMode_t.POINT_WORLD_TEXT_REORIENT_NONE;
            marker.DispatchSpawn();
            marker.Teleport(new Vector(pos.X, pos.Y, pos.Z + 70), new QAngle(0, 90, 90));

            _deathMarkers[steamId] = marker;
            _lastMarkerSeconds[steamId] = seconds;
        }

        private void RemoveDeathMarker(ulong steamId)
        {
            if (_deathMarkers.TryGetValue(steamId, out var marker))
            {
                if (marker.IsValid)
                    marker.Remove();
                _deathMarkers.Remove(steamId);
                _lastMarkerSeconds.Remove(steamId);
                _deathTeams.Remove(steamId);
            }
        }

        private void UpdateDeathMarkers()
        {
            foreach (var steamId in _deathMarkers.Keys.ToList())
            {
                if (!_deathMarkers.TryGetValue(steamId, out var marker) || !marker.IsValid)
                {
                    _deathMarkers.Remove(steamId);
                    _lastMarkerSeconds.Remove(steamId);
                    continue;
                }

                float remaining;
                if (_reviveWindowFrozen.TryGetValue(steamId, out float frozenRemaining))
                    remaining = frozenRemaining;
                else if (_reviveWindowEndTime.TryGetValue(steamId, out var endTime))
                    remaining = (float)(endTime - Utils.GetServerTime()).TotalSeconds;
                else
                {
                    RemoveDeathMarker(steamId);
                    continue;
                }

                int seconds = Math.Max(0, (int)Math.Ceiling(remaining));

                if (!_lastMarkerSeconds.TryGetValue(steamId, out int lastSec) || lastSec != seconds)
                {
                    var deadPlayer = Utilities.GetPlayers().FirstOrDefault(p => p.IsValid && p.SteamID == steamId);
                    marker.MessageText = $"{seconds}s";
                    Utilities.SetStateChanged(marker, "CPointWorldText", "m_messageText");
                    _lastMarkerSeconds[steamId] = seconds;
                }
            }
        }

        private HookResult OnRoundAnnounceWarmup(EventRoundAnnounceWarmup @event, GameEventInfo info)
        {
            _isWarmup = true;
            return HookResult.Continue;
        }

        private HookResult OnWarmupEnd(EventWarmupEnd @event, GameEventInfo info)
        {
            _isWarmup = false;
            return HookResult.Continue;
        }

        private void OnCheckTransmit(CCheckTransmitInfoList infoList)
        {
            if (_deathMarkers.Count == 0)
                return;

            foreach ((CCheckTransmitInfo info, CCSPlayerController? player) in infoList)
            {
                if (player == null || !player.IsValid) continue;

                foreach (var (steamId, marker) in _deathMarkers)
                {
                    if (!marker.IsValid) continue;

                    if (!_deathTeams.TryGetValue(steamId, out var deadTeam) || player.Team != deadTeam)
                    {
                        info.TransmitEntities.Remove(marker);
                    }
                }
            }
        }
    }
}
