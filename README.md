# CounterStrikeAddons

A collection of plugins for **Counter-Strike 2** servers, built with [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp).

**References:**
- [CounterStrikeSharp docs](https://docs.cssharp.dev/)
- [CounterStrikeSharp GitHub](https://github.com/roflmuffin/CounterStrikeSharp)
- [Metamod:Source](https://www.metamodsource.net/downloads.php/?branch=master)

## Recent updates

The project has been updated to the current CounterStrikeSharp toolchain and .NET runtime:
- All plugins target **.NET 10** (`net10.0`)
- The solution uses **CounterStrikeSharp.API 1.0.371**
- New gameplay additions include **DamageReport**, **ReviveTeammate**, **SiteRestrict**, and **StartMap**
- Shared logic is centralized in the **SharedLibrary** project used by all plugins

## Plugins

| Plugin | Version | Description |
|---|---|---|
| [AdminMenu](#adminmenu) | 2.1 | In-game admin management menu |
| [DamageReport](#damagereport) | 1.0 | Shows damage dealt and received to a player upon death |
| [GameStatistic](#gamestatistic) | 2.0 | Player and map statistics tracking |
| [MenuHotKey](#menuhotkey) | 1.0 | Keyboard hotkeys for all in-game menus |
| [QuickDefuse](#quickdefuse) | 1.0 | Defuse the bomb by cutting the correct wire |
| [ReviveTeammate](#reviveteammate) | 1.0 | Revive dead teammates by holding USE near them |
| [SiteRestrict](#siterestrict) | 1.0 | Restricts bomb planting to a single random site when CTs are low |
| [StartMap](#startmap) | 1.0 | Automatically switch to a configured map on server start |
| [SpamFilter](#spamFilter) | 1.0 | Auto kick spammers without steamId, disable chat messages to them |
## Requirements

- **Counter-Strike 2** dedicated game server
- **CounterStrikeSharp.API v1.0.371** or later
- **.NET 10** runtime

This project is built against the current .NET 10 toolchain and the matching CounterStrikeSharp API package referenced by the plugins.

## Installation

Each plugin is installed independently. Extract the plugin folder into the corresponding subdirectory:

```
...\csgo\addons\counterstrikesharp\plugins\<PluginName>\
```

See each plugin's section below for specific installation notes.

---

# AdminMenu

An in-game admin menu accessible via the `!admin` chat command.

**Features — admins can:**
- Ban a player (timed or permanent)
- Kick a player
- Kill a player
- Slap a player
- Respawn a player
- Mute / unmute a player (manually, or automatically after death — configurable)
- Rename any player (type the new name in chat after selecting this menu item)
- Set a player's team (with optional respawn)
- Auto-rename players if their name is already taken (configurable)
- Drop a player's weapon
- Change map (requires the [RockTheVote](https://github.com/abnerfs/cs2-rockthevote) addon or its `maplist.txt`)
- Team shuffle (configurable; requires the GameStatistic stat file to balance teams by skill)
- Add / kick bots
- Set admin level for a player
- Weapon (un)restrict — for the current map or all maps
- Welcome message or ban message shown on connect (configurable)
- Start a player vote/poll with a custom question and up to 4 options (`!vote` command)
- Block spammers

**Admin levels:** There are 3 levels. Admins cannot perform actions on players with a higher admin level than their own.

**Commands:** `!admin`, `!vote`

### Voting

Any player can start a poll among everyone on the server using the `!vote` chat command.

**Format:**
```
!vote Question, Option1, Option2[, Option3, Option4]
```
- The text after `!vote` is split by commas. The **first part is always the question** (used as the menu title); the remaining parts are the answer options.
- Between **2 and 4 options** are supported (3 to 5 comma-separated parts in total).

**How it works:**
1. A pop-up menu appears for every player, titled with the question and listing the options.
2. The vote is **active for 10 seconds**. Each player can vote once; selecting an option closes the menu for that player. After the time runs out, any still-open vote menus close automatically.
3. When the vote ends, the per-option results are printed to chat along with the winner (or a tie if applicable).
4. After a vote completes, the player who started it must wait 30 seconds before starting another.

It is **strongly recommended** to use the [MenuHotKey](#menuhotkey) plugin alongside this feature so options can be selected instantly from the pop-up menu.

## Installation

1. Extract the `AdminMenu` folder to `...\csgo\addons\counterstrikesharp\plugins\AdminMenu\`.
2. The plugin uses the following config files (created automatically on first run if missing):
   - `...\csgo\addons\counterstrikesharp\configs\admins.json` — admin list
   - `...\csgo\addons\counterstrikesharp\configs\banned.json` — banned players list
   - `...\csgo\addons\counterstrikesharp\configs\weaponRestrict.json` — weapon restrictions
3. For the **change map** feature, place the `maplist.txt` from [RockTheVote](https://github.com/abnerfs/cs2-rockthevote) in `...\csgo\addons\counterstrikesharp\plugins\RockTheVote\maplist.txt`.
4. See the `Example/` folder in the plugin directory for sample config files.

---

# GameStatistic

Tracks player and map statistics. Statistics are only recorded when at least 4 (configurable) non-spectator players are present.

**Player statistics:**
- Tracks kills, deaths, team kills, self-kills, and assists
- Events during warmup or after round end are not counted
- Builds a ranking used by AdminMenu to perform skill-balanced team shuffles

**Map statistics:**
- Tracks how many times a map has been started and completed (used to calculate RTV rate)
- Tracks CT and T side win counts per map
- Displayed automatically at halftime and warmup end

**Commands:**

| Command | Description |
|---|---|
| `!top` | Show the top players by ranking |
| `!bottom` | Show the bottom players by ranking |
| `!mystat` | Show your own statistics |
| `!mapstat` | Show statistics for the current map |
| `!teamstat` | Show team statistics |
| `!chance` | Show win chance estimates per team |
| `!help` | List available commands |

## Installation

Extract the `GameStatistic` folder to `...\csgo\addons\counterstrikesharp\plugins\GameStatistic\`.

---

# MenuHotKey

Allows players to select in-game menu items using bound keyboard keys instead of typing `!1`, `!2`, etc. in chat. Works with every CounterStrikeSharp menu, including AdminMenu and QuickDefuse.

**Setup:** Bind number keys in the CS2 console. For example, to bind Numpad 3 to menu option 3:
```
bind kp_3 "3"
```

For the full key name reference, see the [Steam key mapping guide](https://steamcommunity.com/sharedfiles/filedetails/?id=2498088800).

## Installation

Extract the `MenuHotKey` folder to `...\csgo\addons\counterstrikesharp\plugins\MenuHotKey\`.

---

# QuickDefuse

Adds a wire-cutting minigame for defusing the bomb. When a CT starts defusing, a menu appears with 5 wire colour options. Choosing the correct wire defuses the bomb instantly; choosing the wrong one detonates it immediately.

**Wires:**
| Option | Colour |
|---|---|
| `!1` | Green |
| `!2` | Yellow |
| `!3` | Red |
| `!4` | Blue |
| `!5` | Random |

**Notes:**
- The correct wire is randomised each time the bomb is planted.
- The wire colour menu appears as soon as a CT begins defusing.
- It is **strongly recommended** to use the [MenuHotKey](#menuhotkey) plugin alongside this one so the wire can be selected instantly from the pop-up menu.

## Installation

Extract the `QuickDefuse` folder to `...\csgo\addons\counterstrikesharp\plugins\QuickDefuse\`.

---

# ReviveTeammate

Allows alive teammates to revive recently killed players without waiting for the next round.

**How it works:**
1. After a player dies, there is a configurable time window during which they can be revived.
2. An alive teammate stands close to the death position, aims at the dead player's body, and **holds the USE key**.
3. A progress bar is shown on screen. After holding for the configured duration, the dead player is respawned with a small amount of HP (configurable).
4. If the revivers stops aiming or moves out of range, the progress resets.

**Configurable options (in `config.json`):**

| Option | Default | Description |
|---|---|---|
| `ReviveHoldDurationSeconds` | `10.0` | How long to hold USE to complete the revive |
| `ReviveDeathWindowSeconds` | `30.0` | How long after death a player can be revived |
| `ReviveHP` | `10` | HP the revived player is spawned with |
| `CanReviveTeammate` | `true` | Enable or disable the feature |

## Installation

Extract the `ReviveTeammate` folder to `...\csgo\addons\counterstrikesharp\plugins\ReviveTeammate\`.

---

# StartMap

Automatically switches the server to a configured map shortly after startup. Supports official maps, workshop maps by ID, and workshop maps by name.

**How it works:**
1. On server load, the plugin reads `startMap.txt` from its plugin directory.
2. After a short delay, it switches to the specified map using the appropriate server command (`map`, `host_workshop_map`, or `ds_workshop_changelevel`).

**`startMap.txt` format:**
```
mapname:workshopid
```
Example:
```
de_dolls:3501880673
```
- If the map name is a valid built-in map, it uses `map <name>`.
- If a workshop ID is provided, it uses `host_workshop_map <id>`.
- Otherwise, it falls back to `ds_workshop_changelevel <name>`.

## Installation

1. Extract the `StartMap` folder to `...\csgo\addons\counterstrikesharp\plugins\StartMap\`.
2. Edit `startMap.txt` with the desired map name and workshop ID.

---

# DamageReport

Shows each player a damage summary in chat upon death. Displays how much damage (and how many hits) they dealt to each enemy this round, and how much damage they received from each attacker — with the killer highlighted.

**Notes:**
- The report is only shown to the player who died, not to all players.
- Bots receive no report (they cannot read chat).
- Damage during warmup is not tracked.

## Installation

Extract the `DamageReport` folder to `...\csgo\addons\counterstrikesharp\plugins\DamageReport\`.

---

# SiteRestrict

When the CT team has fewer players than a configurable threshold at the start of a round, the plugin randomly selects one bomb site (A or B) as the only active planting location for that round. Planting at the other site is blocked.

**How it works:**
1. At the start of each round the CT count is checked.
2. If CTs are below `MinCTsForSiteRestrict`, one site is chosen at random.
3. All players receive a center-screen announcement 2 seconds into the round indicating which site is active.
4. If a T player attempts to plant at the restricted site, their bomb is dropped and they receive an on-screen warning.

**Configurable options (in `config.json`):**

| Option | Default | Description |
|---|---|---|
| `MinCTsForSiteRestrict` | `4` | Minimum CT count required; restriction activates below this value |

**Commands:**
- `!reload` (admin only) — reload the config without restarting the plugin.
- `!siterestrict` (admin level 3+) — open the SiteRestrict admin menu. From the menu you can toggle/force which site (A or B) is allowed for the current map (useful for troubleshooting maps or forcing a site when automatic detection fails). The toggle is saved per-map in `site_switch.json` located in the plugin folder.

## Installation

Extract the `SiteRestrict` folder to `...\csgo\addons\counterstrikesharp\plugins\SiteRestrict\`.

---

# SpamFilter
It tries to detect spammers and the frequent name changes used by scammers.

## Installation

Extract the `SpamFilter` folder to `...\csgo\addons\counterstrikesharp\plugins\SpamFilter\`.

---

# SharedLibrary

An internal shared library used by all plugins in this solution. It provides common utilities including configuration loading, localisation (multi-language support), player helpers, statistic helpers, and weapon helpers. It is not a standalone plugin and does not need to be installed separately.

------------------------
------------------------
------------------------
# Donate

If you enjoy my work and would like to support what I do, I'd truly appreciate it. 
You can do so here: https://revolut.me/gaborszolner

