using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using SharedLibrary;

namespace SiteRestrict
{
    public class SiteRestrict : BasePlugin
    {
        public override string ModuleName => "SiteRestrict";
        public override string ModuleVersion => "1.0";
        public override string ModuleAuthor => "Sinistral";
        public override string ModuleDescription => "Restricts bomb planting to a single random site when CTs are low";

        public readonly string PluginPrefix = "[SiteRestrict]";

        private static (float MinX, float MinY, float MinZ, float MaxX, float MaxY, float MaxZ)? _allowedZone = null;
        private static (float X, float Y, float Z)? _allowedCenter = null;
        private static (float X, float Y, float Z)? _otherCenter = null;
        private static Site _allowedSiteName = Site.NotDefined;
        private static bool _isRestricted = false;
        private static bool _isWarmup = false;
        private static Config _config = new();
        private static SiteSwitchConfig _switchConfig = new();
        private CounterStrikeSharp.API.Modules.Timers.Timer? _messageTimer = null;

        private string SwitchConfigPath => Path.Combine(ModuleDirectory, "site_switch.json");

        private static readonly Random random = new();
        Site previousSite = Site.NotDefined;
        double sameSiteProbability = 0.5;

        public override void Load(bool hotReload)
        {
            _config = Config.LoadConfig(Path.Combine(ModuleDirectory, "config.json"));
            _switchConfig = SiteSwitchConfig.Load(SwitchConfigPath);
            SharedLibrary.Localizer.Initialize(_config.Language);

            RegisterEventHandler<EventRoundAnnounceWarmup>(OnRoundAnnounceWarmup);
            RegisterEventHandler<EventWarmupEnd>(OnWarmupEnd);
            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
            RegisterEventHandler<EventBombBeginplant>(OnBombBeginPlant);
            RegisterEventHandler<EventPlayerChat>(OnPlayerChat);
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

        public HookResult OnPlayerChat(EventPlayerChat @event, GameEventInfo info)
        {
            var player = Utilities.GetPlayerFromUserid(@event.Userid);
            if (player is null || !player.IsValid)
                return HookResult.Continue;

            string command = @event?.Text.Trim().ToLower() ?? "";
            string adminsFilePath = Path.Combine(ModuleDirectory, "..", "..", "configs", "admins.json");

            if (command is "!reload")
            {
                if (PlayerHelper.GetAdminLevel(player, adminsFilePath) > 2)
                {
                    _config = Config.LoadConfig(Path.Combine(ModuleDirectory, "config.json"));
                    SharedLibrary.Localizer.Initialize(_config.Language);
                    player.PrintToChat($"{PluginPrefix} {Msg.Get("ConfigsReloaded")}");
                }
            }
            else if (command is "!siterestrict")
            {
                if (PlayerHelper.GetAdminLevel(player, adminsFilePath) >= 3)
                    OpenAdminMenu(player);
                else
                    player.PrintToChat($"{PluginPrefix} {Msg.Get("NoPermission")}");
            }

            return HookResult.Continue;
        }

        private void OpenAdminMenu(CCSPlayerController player)
        {
            var menu = new CenterHtmlMenu(Msg.Get("AdminMenuTitle"), this);

            menu.AddMenuOption(Msg.Get("MenuSwitchSites"), (controller, option) =>
            {
                if (controller is null || !controller.IsValid)
                    return;

                string mapName = Server.MapName;
                bool nowSwapped = _switchConfig.Toggle(mapName);
                _switchConfig.Save(SwitchConfigPath);

                if (_isRestricted && _allowedSiteName == Site.NotDefined)
                {
                    _allowedSiteName = _allowedSiteName == Site.A ? Site.B : Site.A;

                    foreach (var p in Utilities.GetPlayers().Where(p => p.IsValid))
                        p.PrintToCenter(Msg.Get("SiteAllowed", _allowedSiteName));
                }

                string message = nowSwapped
                    ? Msg.Get("SitesSwitchedOn", mapName)
                    : Msg.Get("SitesSwitchedOff", mapName);

                controller.PrintToChat($"{PluginPrefix} {message}");
                MenuManager.CloseActiveMenu(controller);
            });

            MenuManager.OpenCenterHtmlMenu(this, player, menu);
        }

        private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            _allowedZone = null;
            _allowedCenter = null;
            _otherCenter = null;
            _allowedSiteName = Site.NotDefined;
            _isRestricted = false;

            if (_isWarmup)
                return HookResult.Continue;

            AddTimer(0.3f, SetupRestriction);
            return HookResult.Continue;
        }

        private void SetupRestriction()
        {
            if (_isWarmup)
                return;

            int ctCount = Utilities.GetPlayers()
                .Count(p => p.IsValid && p.Team == CsTeam.CounterTerrorist);

            if (ctCount >= _config.MinCTsForSiteRestrict)
                return;

            var sites = Utilities.FindAllEntitiesByDesignerName<CBombTarget>("func_bomb_target")
                .Where(e => e.IsValid && e.AbsOrigin != null)
                .ToList();

            if (sites.Count < 2)
                return;

            var identified = IdentifySites(sites);
            if (identified == null)
                return;

            var (nameA, zoneA, centerA, nameB, zoneB, centerB) = identified.Value;

            if (_switchConfig.IsSwapped(Server.MapName))
                (nameA, nameB) = (nameB, nameA);

            if (GetRandomSite(previousSite, ref sameSiteProbability) == Site.A)
            {
                _allowedSiteName = Site.A;
                _allowedZone = zoneA;
                _allowedCenter = centerA;
                _otherCenter = centerB;
            }
            else
            {
                _allowedSiteName = Site.B;
                _allowedZone = zoneB;
                _allowedCenter = centerB;
                _otherCenter = centerA;
            }

            _isRestricted = true;

            Server.PrintToChatAll(Msg.Get("SiteAllowed", _allowedSiteName));
            _messageTimer = AddTimer(1.0f, () =>
            {
                if (!_isRestricted)
                    return;

                foreach (var p in Utilities.GetPlayers().Where(p => p.IsValid))
                    p.PrintToCenter(Msg.Get("SiteAllowed", _allowedSiteName));
            }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
        }

        private static Site GetRandomSite(Site previousSite, ref double sameSiteProbability)
        {
            Site nextSite = Site.NotDefined;
            if (previousSite == Site.NotDefined)
            {
                return random.NextDouble() < 0.5 ? Site.A : Site.B;
            }
            else
            {
                if (random.NextDouble() < sameSiteProbability)
                {
                    nextSite = previousSite;
                }
                else
                {
                    nextSite = previousSite == Site.A ? Site.B : Site.A;
                }

                if (nextSite != previousSite)
                {
                    sameSiteProbability = 0.5;
                }
                else
                {
                    sameSiteProbability -= 0.1;

                    if (sameSiteProbability < 0)
                    {
                        sameSiteProbability = 0;
                    }
                }

                previousSite = nextSite;
                return nextSite;
            }
        }

        private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            StopMessageTimer();
            _isRestricted = false;
            return HookResult.Continue;
        }

        private void StopMessageTimer()
        {
            _messageTimer?.Kill();
            _messageTimer = null;
            ClearCenterMessages();
        }

        private static void ClearCenterMessages()
        {
            foreach (var p in Utilities.GetPlayers().Where(p => p.IsValid))
                p.PrintToCenter(" ");
        }

        private HookResult OnBombBeginPlant(EventBombBeginplant @event, GameEventInfo info)
        {
            if (!_isRestricted)
                return HookResult.Continue;

            var player = @event.Userid;
            if (player == null || !player.IsValid)
                return HookResult.Continue;

            var pawn = player.PlayerPawn.Value;
            if (pawn?.AbsOrigin == null)
                return HookResult.Continue;

            bool isAtAllowedSite;
            if (_allowedZone != null)
            {
                isAtAllowedSite = IsInsideZone(pawn.AbsOrigin, _allowedZone.Value);
            }
            else if (_allowedCenter != null && _otherCenter != null)
            {
                isAtAllowedSite = Distance(pawn.AbsOrigin, _allowedCenter.Value) <= Distance(pawn.AbsOrigin, _otherCenter.Value);
            }
            else
            {
                return HookResult.Continue;
            }

            if (isAtAllowedSite)
                return HookResult.Continue;

            // Wrong site — drop the bomb
            player.PrintToCenter(Msg.Get("WrongSite", _allowedSiteName));
                if (player.IsValid && player.PawnIsAlive)
                    player.DropActiveWeapon();

            return HookResult.Continue;
        }

        private static (
            string nameA,
            (float MinX, float MinY, float MinZ, float MaxX, float MaxY, float MaxZ)? zoneA,
            (float X, float Y, float Z) centerA,
            string nameB,
            (float MinX, float MinY, float MinZ, float MaxX, float MaxY, float MaxZ)? zoneB,
            (float X, float Y, float Z) centerB
        )? IdentifySites(List<CBombTarget> sites)
        {
            var validSites = sites.Where(s => s.AbsOrigin != null).ToList();

            var aSites = validSites.Where(s => !s.IsBombSiteB).ToList();
            var bSites = validSites.Where(s => s.IsBombSiteB).ToList();

            CBombTarget siteA;
            CBombTarget siteB;

            if (aSites.Count == 1 && bSites.Count == 1)
            {
                siteA = aSites.First();
                siteB = bSites.First();
            }
            else
            {
                var sorted = validSites.OrderBy(s => s.AbsOrigin!.X).ToList();
                if (sorted.Count < 2)
                    return null;
                siteA = sorted.First();
                siteB = sorted[1];
            }

            return (
                Site.A.ToString(), GetZoneAabb(siteA), GetSiteCenter(siteA),
                Site.B.ToString(), GetZoneAabb(siteB), GetSiteCenter(siteB)
            );
        }

        private static (float MinX, float MinY, float MinZ, float MaxX, float MaxY, float MaxZ)? GetZoneAabb(CBaseEntity site)
        {
            var origin = site.AbsOrigin;
            if (origin == null) return null;
            var mins = site.Collision?.Mins;
            var maxs = site.Collision?.Maxs;
            if (mins == null || maxs == null) return null;

            if (MathF.Abs(maxs.X - mins.X) < 1f && MathF.Abs(maxs.Y - mins.Y) < 1f) return null;

            const float buffer = 20.0f; // adjust if needed (units depend on map scale)

            return (
                origin.X + mins.X - buffer, origin.Y + mins.Y - buffer, origin.Z + mins.Z - buffer,
                origin.X + maxs.X + buffer, origin.Y + maxs.Y + buffer, origin.Z + maxs.Z + buffer
            );
        }

        private static (float X, float Y, float Z) GetSiteCenter(CBaseEntity site)
        {
            var origin = site.AbsOrigin!;
            var mins = site.Collision?.Mins;
            var maxs = site.Collision?.Maxs;
            if (mins != null && maxs != null)
                return (
                    origin.X + (mins.X + maxs.X) * 0.5f,
                    origin.Y + (mins.Y + maxs.Y) * 0.5f,
                    origin.Z + (mins.Z + maxs.Z) * 0.5f
                );
            return (origin.X, origin.Y, origin.Z);
        }

        private static bool IsInsideZone(Vector pos, (float MinX, float MinY, float MinZ, float MaxX, float MaxY, float MaxZ) zone)
        {
            return pos.X >= zone.MinX && pos.X <= zone.MaxX &&
                   pos.Y >= zone.MinY && pos.Y <= zone.MaxY &&
                   pos.Z >= zone.MinZ && pos.Z <= zone.MaxZ;
        }

        private static float Distance(Vector a, (float X, float Y, float Z) b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }

    enum Site
    {
        NotDefined,
        A,
        B,
        Both
    }
}
