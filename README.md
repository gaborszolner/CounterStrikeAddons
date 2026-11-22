# CounterStrikeAddons

A collection of plugins for **Counter-Strike 2** servers, built with [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp).

**References:**
- [CounterStrikeSharp docs](https://docs.cssharp.dev/)
- [CounterStrikeSharp GitHub](https://github.com/roflmuffin/CounterStrikeSharp)
- [Metamod:Source](https://www.metamodsource.net/downloads.php/?branch=master)

## Plugins

| Plugin | Version | Description |
|---|---|---|
| [AdminMenu](#adminmenu) | 2.1 | In-game admin management menu |
| [DamageReport](#damagereport) | 1.0 | Shows damage dealt and received to a player upon death |
| [GameStatistic](#gamestatistic) | 2.0 | Player and map statistics tracking |
| [MenuHotKey](#menuhotkey) | 1.0 | Keyboard hotkeys for all in-game menus |
| [QuickDefuse](#quickdefuse) | 1.0 | Defuse the bomb by cutting the correct wire |
| [ReviveTeammate](#reviveteammate) | 1.0 | Revive dead teammates by holding USE near them |
| [RockTheVote](#rockthevote) | 1.0 | Rock the vote, map nomination and end of map vote |
| [SiteRestrict](#siterestrict) | 1.0 | Restricts bomb planting to a single random site when CTs are low |
| [StartMap](#startmap) | 1.0 | Automatically switch to a configured map on server start |
| [SpamFilter](#spamFilter) | 1.0 | Auto kick spammers without steamId, disable chat messages to them |
## Requirements

- **Counter-Strike 2** dedicated game server
- **CounterStrikeSharp.API v1.0.380** or later
- **.NET 10** runtime

This project is built against the current .NET 10 toolchain and the matching CounterStrikeSharp API package referenced by the plugins.

## Installation

Each plugin is installed independently. Extract the plugin folder into the corresponding subdirectory:

```
...\csgo\addons\counterstrikesharp\plugins\<PluginName>\
```

See each plugin's section below for specific installation notes.

## Configuration

Each plugin creates its own configuration in its plugin directory on first run. Default JSON files are provided in the source tree as references and are not copied into the deployed plugin directory:

| Plugin | Configuration file |
|---|---|
| AdminMenu | `AdminMenuConfig.json` |
| DamageReport | `DamageReportConfig.json` |
| GameStatistic | `GameStatisticConfig.json` |
| MenuHotKey | `MenuHotKeyConfig.json` |
| QuickDefuse | `QuickDefuseConfig.json` |
| ReviveTeammate | `ReviveTeammateConfig.json` |
| RockTheVote | `rtvConfig.json` |
| SiteRestrict | `SiteRestrictConfig.json` |
| SpamFilter | `SpamFilterConfig.json` |

If a plugin has an old `config.json` but no new plugin-specific file, its relevant settings are copied to the new file on first load. The old file is left unchanged. If the new file exists, it takes precedence. For these migrated plugins, invalid configuration files cause a load error instead of silently resetting settings.

On case-sensitive systems, the previous lowercase-`config` filenames, including `rtvconfig.json`, are also migrated when the new filename is missing.

Settings used by multiple plugins, including `Language`, must be configured separately in each plugin's file. StartMap uses `startMap.txt` instead.

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
- Change map (requires the [RockTheVote](#rockthevote) plugin or its `maplist.txt`)
- Manage the map list and workshop map files with the [Map handler](#map-handler)
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

### Map handler

The **Map handler** menu item in `!admin` (admin level 3) manages the RockTheVote `maplist.txt` and the downloaded workshop map files. It is only shown when `maplist.txt` exists.

**Options:**
- **Add map** — type the map name in chat, then its workshop ID (or `-` for a built-in map without an ID). The entry is appended to `maplist.txt` as `name:workshopId`. Map names cannot contain spaces or `:`, and duplicates are rejected. You have 60 seconds for each step; type `!cancel` to abort.
- **Delete map** — pick a map from the list and confirm. The map is removed from `maplist.txt`, and its workshop folder (`...\csgo\workshop\content\730\<id>`) and its entry in `appworkshop_730.acf` are deleted. The currently running map cannot be deleted.
- **Clean map files** — after confirmation, deletes every workshop map folder whose ID is not in `maplist.txt` (and its entry in `appworkshop_730.acf`). The folder of the currently running map is always kept. The deleted maps are listed in chat.

## Installation

1. Extract the `AdminMenu` folder to `...\csgo\addons\counterstrikesharp\plugins\AdminMenu\`.
2. The plugin uses the following config files (created automatically on first run if missing):
   - `...\csgo\addons\counterstrikesharp\configs\admins.json` — admin list
   - `...\csgo\addons\counterstrikesharp\configs\banned.json` — banned players list
   - `...\csgo\addons\counterstrikesharp\configs\weaponRestrict.json` — weapon restrictions
3. For the **change map** feature, place the `maplist.txt` from [RockTheVote](#rockthevote) in `...\csgo\addons\counterstrikesharp\plugins\RockTheVote\maplist.txt`.
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

**Configurable options (in `ReviveTeammateConfig.json`):**

| Option | Default | Description |
|---|---|---|
| `ReviveHoldDurationSeconds` | `10.0` | How long to hold USE to complete the revive |
| `ReviveDeathWindowSeconds` | `30.0` | How long after death a player can be revived |
| `ReviveHP` | `10` | HP the revived player is spawned with |
| `CanReviveTeammate` | `false` | Enable or disable the feature |

## Installation

Extract the `ReviveTeammate` folder to `...\csgo\addons\counterstrikesharp\plugins\ReviveTeammate\`.

---

# RockTheVote

Lets players vote to change the map (rock the vote), nominate maps for the next vote, and automatically runs a map vote before the match ends. Maps are read from `maplist.txt` in the plugin directory (the same file used by the AdminMenu [Map handler](#map-handler)).

**How it works:**
1. Players type `!rtv`. When the configured percentage of human players has voted, a map vote starts. If the next map is already decided (e.g. with `!setnextmap`), `!rtv` still counts, and reaching the threshold changes to that map after 5 seconds without a vote.
2. Players can `!nominate` maps beforehand; nominated maps (most nominated first) are always included in the vote, the rest of the options are filled with random maps. At most `MapsToShow` different maps can be nominated; when a player leaves, their nomination is removed and frees up a slot.
3. The vote menu is shown to every player for `VotingSecondLimit` seconds; the vote ends early as soon as every human player has voted (e.g. a lone player whose nominated map is in the vote). An RTV vote also ends early once the winner is certain, i.e. no other map can overtake it even if all remaining players vote for it. Players whose nominated map is in the vote automatically vote for it and don't get the menu (they can use `!revote` to change). The map with the most votes wins; on a tie the map that reached that vote count first wins (only remaining ties and no-vote cases are decided randomly). When the vote ends, everyone sees the vote type (RTV or end of map), the per-map results and the chosen map.
4. After a successful RTV the map changes 5 seconds after the vote (if `ChangeMapImmediatelyAfterRtv` is enabled), otherwise the winner becomes the next map.
5. If `EnableEndOfMapVote` is enabled and no next map is set, a vote starts automatically at the start of a round when the match can end within `EndOfMapVoteRoundsBeforeEnd` rounds (remaining rounds, or rounds a team needs to clinch). When the match ends, the server switches to the chosen map just before `mp_match_restart_delay` expires.
6. The current map and the last `MapsInCoolDown` played maps cannot be nominated or voted.

**`maplist.txt` format** — one map per line, lines starting with `//` are comments:
```
de_dust2
de_dolls:3501880673
```
Built-in maps use `changelevel`, maps with a workshop ID use `host_workshop_map <id>`, otherwise `ds_workshop_changelevel <name>`.

**Commands:**

| Command | Description |
|---|---|
| `!rtv` | Vote to change the map |
| `!nominate [map]` | Nominate a map (opens a menu, or matches the given name/part of name) |
| `!revote` | During a map vote: removes your previous vote (if any) and reopens the vote menu |
| `!nextmap` | Show the next map |
| `!setnextmap [map]` | Admin level 3: set the next map |
| `!reload` | Admin level 3: reload `rtvConfig.json` |

**Configurable options (in `rtvConfig.json`):**

| Option | Default | Description |
|---|---|---|
| `EnableRtv` | `true` | Enable the `!rtv` command |
| `EnableRtvInWarmup` | `false` | Allow `!rtv` during warmup |
| `MinimumRtvPercentage` | `60` | Percentage of human players needed to start the vote |
| `ChangeMapImmediatelyAfterRtv` | `true` | Change map right after an RTV vote instead of at match end |
| `EnableNominate` | `true` | Enable the `!nominate` command |
| `EnableNominateInWarmup` | `true` | Allow nominations during warmup |
| `EnableEndOfMapVote` | `true` | Automatically vote for the next map before the match ends |
| `EndOfMapVoteRoundsBeforeEnd` | `1` | How many rounds before the match can end the end-of-map vote starts (`1` = in the round that can end the match, minimum 1) |
| `VotingSecondLimit` | `30` | Duration of the map vote in seconds (minimum 5) |
| `CloseVotingMenuAfterVote` | `true` | Close the vote menu after voting (players can use `!revote` to change their vote) |
| `MapsToShow` | `8` | Number of maps in the vote (also the maximum number of different nominated maps) |
| `MapsInCoolDown` | `3` | Number of previously played maps excluded from nominations and votes |
| `MapSortOrder` | `None` | `None` keeps the file unchanged; `Name` sorts alphabetically; `Usage` puts least recently played maps first using GameStatistic's `mapStatistic.json` |
| `Language` | `en` | Message language |

Sorting updates `maplist.txt` when the plugin loads, after `!reload`, and at each round start. Comment lines are kept at the top. `Usage` requires the GameStatistic map statistics file; missing or invalid files and invalid sort modes are logged.

It is **strongly recommended** to use the [MenuHotKey](#menuhotkey) plugin alongside this one so maps can be selected instantly from the pop-up menu.

## Installation

1. Extract the `RockTheVote` folder to `...\csgo\addons\counterstrikesharp\plugins\RockTheVote\`.
2. Edit `maplist.txt` with your maps.

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

**Configurable options (in `SiteRestrictConfig.json`):**

| Option | Default | Description |
|---|---|---|
| `MinCTsForSiteRestrict` | `4` | Minimum CT count required; restriction activates below this value |

**Commands:**
- `!reload` (admin only) — reload the config without restarting the plugin.

## Installation

Extract the `SiteRestrict` folder to `...\csgo\addons\counterstrikesharp\plugins\SiteRestrict\`.

---

# SpamFilter
It tries to detect spammers and the frequent name changes used by scammers.

**Configurable options (in `SpamFilterConfig.json`, created automatically on first run):**

| Option | Default | Description |
|---|---|---|
| `Language` | `en` | Message language |

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

