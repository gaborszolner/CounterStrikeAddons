using AdminMenu.Entries;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace AdminMenu
{
    public partial class AdminMenu : BasePlugin
    {
        private void StartVote(CCSPlayerController player, string voteContent = "")
        {
            if (player is null || player.AuthorizedSteamID is null)
            {
                return;
            }

            if (GetAdminLevel(player) < 1) 
            {
                player.PrintToChat(Msg.Get("NotAdminError"));
                return;
            }

            var steamId = player.AuthorizedSteamID.SteamId2;

            if (string.IsNullOrWhiteSpace(voteContent))
            {
                lock (_voteLock)
                {
                    if (steamId != null && _voteCooldown.ContainsKey(steamId))
                    {
                        if (DateTime.UtcNow.Ticks < _voteCooldown[steamId])
                        {
                            player.PrintToChat($"{PluginPrefix} {Msg.Get("VoteWaitCooldown")}");
                            return;
                        }
                        else
                        {
                            _voteCooldown.Remove(steamId);
                        }
                    }

                    if (_activeVote != null)
                    {
                        player.PrintToChat($"{PluginPrefix} {Msg.Get("VoteAlreadyInProgress")}");
                        return;
                    }
                }

                player.PrintToChat($"{PluginPrefix} {Msg.Get("VoteFormat")}");
                return;
            }

            if (!ValidateVoteInput(voteContent, out var parts))
            {
                player.PrintToChat($"{PluginPrefix} {Msg.Get("VoteInvalidFormat")}");
                return;
            }

            lock (_voteLock)
            {
                if (steamId != null && _voteCooldown.ContainsKey(steamId))
                {
                    if (DateTime.UtcNow.Ticks < _voteCooldown[steamId])
                    {
                        player.PrintToChat($"{PluginPrefix} {Msg.Get("VoteWaitCooldown")}");
                        return;
                    }
                    else
                    {
                        _voteCooldown.Remove(steamId);
                    }
                }

                if (_activeVote != null)
                {
                    player.PrintToChat($"{PluginPrefix} {Msg.Get("VoteAlreadyInProgress")}");
                    return;
                }

                _activeVote = new VoteState
                {
                    Title = parts[0],
                    Options = parts.Skip(1).ToList(),
                    InitiatorSteamID2 = steamId ?? ""
                };
                _voteVoters.Clear();

                Server.PrintToChatAll($"{PluginPrefix} {ChatColors.Green}=== {Msg.Get("VoteNewVote")} ==={ChatColors.Default} {_activeVote.Title}");

                ShowVoteMenu(_activeVote);

                AddTimer(10.0f, () => EndVote());

                player.PrintToChat($"{PluginPrefix} {Msg.Get("VoteStarted")}");
            }
        }

        private bool ValidateVoteInput(string input, out List<string> parts)
        {
            parts = [];
            if (string.IsNullOrWhiteSpace(input))
                return false;

            parts = input.Split(',')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            return parts.Count >= 3 && parts.Count <= 5;
        }

        private void ShowVoteMenu(VoteState vote)
        {
            var players = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot).ToList();
            if (players.Count == 0)
                return;

            foreach (var player in players)
            {
                try
                {
                    var playerSteamId = player.AuthorizedSteamID?.SteamId2;
                    if (playerSteamId != null && _voteVoters.ContainsKey(playerSteamId))
                    {
                        continue;
                    }

                    var voteMenu = new CenterHtmlMenu(vote.Title, this)
                    {
                        PostSelectAction = PostSelectAction.Close
                    };

                    for (int i = 0; i < vote.Options.Count; i++)
                    {
                        int optionIndex = i;
                        voteMenu.AddMenuOption(vote.Options[i], (controller, option) =>
                        {
                            lock (_voteLock)
                            {
                                if (_activeVote != null && controller.AuthorizedSteamID != null)
                                {
                                    var sid = controller.AuthorizedSteamID.SteamId2;
                                    _voteVoters[sid] = optionIndex;
                                    controller.PrintToChat($"{PluginPrefix} {Msg.Get("VoteYouVotedFor", vote.Options[optionIndex])}");
                                }
                            }

                            MenuManager.GetActiveMenu(controller)?.Close();
                        });
                    }

                    MenuManager.OpenCenterHtmlMenu(this, player, voteMenu);
                }
                catch (Exception ex)
                {
                    Logger?.LogError($"{PluginPrefix} Vote menu error: {ex.Message}");
                }
            }
        }

        private void EndVote()
        {
            lock (_voteLock)
            {
                if (_activeVote == null)
                    return;

                var voteResults = new Dictionary<string, int>();
                foreach (var option in _activeVote.Options)
                {
                    voteResults[option] = 0;
                }

                foreach (var voter in _voteVoters)
                {
                    var optionIndex = voter.Value;
                    if (optionIndex >= 0 && optionIndex < _activeVote.Options.Count)
                    {
                        voteResults[_activeVote.Options[optionIndex]]++;
                    }
                }

                int maxVotes = voteResults.Values.DefaultIfEmpty(0).Max();
                var winners = voteResults.Where(x => x.Value == maxVotes).Select(x => x.Key).ToList();

                foreach (var human in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot))
                {
                    MenuManager.GetActiveMenu(human)?.Close();
                }

                Server.PrintToChatAll($"{PluginPrefix} {ChatColors.Green}=== {Msg.Get("VoteResults", _activeVote.Title)} ==={ChatColors.Default}");
                foreach (var option in _activeVote.Options)
                {
                    Server.PrintToChatAll(Msg.Get("VoteOptionResult", option, voteResults[option]));
                }

                if (winners.Count > 1)
                {
                    Server.PrintToChatAll($"{PluginPrefix} {ChatColors.Yellow}{Msg.Get("VoteTie", string.Join(", ", winners))}{ChatColors.Default}");
                }
                else if (winners.Count > 0)
                {
                    Server.PrintToChatAll($"{PluginPrefix} {ChatColors.Green}{Msg.Get("VoteWinner", winners[0])}{ChatColors.Default}");
                }

                if (!string.IsNullOrEmpty(_activeVote.InitiatorSteamID2))
                {
                    long cooldownEndTime = DateTime.UtcNow.AddSeconds(30).Ticks;
                    _voteCooldown[_activeVote.InitiatorSteamID2] = cooldownEndTime;
                }

                _activeVote = null;
                _voteVoters.Clear();
            }
        }
    }
}
