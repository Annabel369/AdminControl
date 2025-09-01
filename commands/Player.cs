using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using CS2MenuManager.API.Enum;
using CS2MenuManager.API.Menu;
using MySqlConnector;
using System.Linq;

namespace AdminControlPlugin.commands
{
    internal class Player
    {
        private readonly AdminControlPlugin _plugin;

        public Player(AdminControlPlugin plugin)
        {
            _plugin = plugin;
        }

        public void ShowAdminAndBanMenu(CCSPlayerController player)
        {
            var menu = new ChatMenu(_plugin.T("menu_admin_title"), _plugin)
            {
                ExitButton = true,
                MenuTime = 20
            };

            menu.AddItem(_plugin.T("menu_manage_players_item"), (p, o) => ShowPlayerManagementMenu(p));
            menu.AddItem(_plugin.T("menu_map_vote_item"), (p, o) => _plugin.StartMapVote(p));

            menu.Display(player, 20);
        }

        private void ShowPlayerManagementMenu(CCSPlayerController caller)
        {
            try
            {
                var menu = new ChatMenu(_plugin.T("menu_manage_players_title"), _plugin)
                {
                    ExitButton = true,
                    MenuTime = 20
                };

                var players = Utilities.GetPlayers()
                    .Where(p => p.IsValid && p.AuthorizedSteamID != null)
                    .ToList();

                if (!players.Any())
                {
                    menu.AddItem(_plugin.T("no_players_available"), DisableOption.None);
                }
                else
                {
                    foreach (var p in players)
                    {
                        var playerName = p.PlayerName;
                        menu.AddItem(playerName, (pc, o) => ShowPlayerActionsMenu(pc, p));
                    }
                }

                menu.Display(caller, 20);
            }
            catch (Exception ex)
            {
                caller.PrintToChat(_plugin.T("error_loading_player_menu"));
                Console.WriteLine($"[AdminControlPlugin] ERRO (player menu): {ex.Message}");
            }
        }

        private void ShowPlayerActionsMenu(CCSPlayerController caller, CCSPlayerController target)
        {
            var menu = new ChatMenu(_plugin.T("menu_player_actions_title", target.PlayerName), _plugin)
            {
                ExitButton = true,
                MenuTime = 20
            };

            menu.AddItem(_plugin.T("ban_player_item", target.PlayerName), (p, o) => ShowBanConfirmation(p, target));
            menu.AddItem(_plugin.T("ip_ban_player_item", target.PlayerName), (p, o) => ShowIpBanReasonMenu(p, target));

            if (AdminManager.GetPlayerAdminData(target) == null)
            {
                menu.AddItem(_plugin.T("add_admin_item", target.PlayerName), (p, o) => HandleGrantAdminAction(p, target));
            }
            else
            {
                menu.AddItem(_plugin.T("remove_admin_item", target.PlayerName), (p, o) => HandleRemoveAdminAction(p, target));
            }

            menu.AddItem(_plugin.T("kick_player_item", target.PlayerName), (p, o) => ShowKickConfirmation(p, target));
            menu.AddItem(_plugin.T("mute_player_item", target.PlayerName), (p, o) => ShowMuteConfirmation(p, target));
            menu.AddItem(_plugin.T("swap_team_item", target.PlayerName), (p, o) => ShowSwapTeamConfirmation(p, target));

            menu.Display(caller, 20);
        }

        private void HandleGrantAdminAction(CCSPlayerController admin, CCSPlayerController target)
        {
            _plugin.AdminCommands.HandleGrantAdmin(admin, target.AuthorizedSteamID!.SteamId64, target.PlayerName, "@css/basic", 1, null);
            admin.PrintToChat(_plugin.T("admin_added_from_menu", target.PlayerName));
        }

        private void HandleRemoveAdminAction(CCSPlayerController admin, CCSPlayerController target)
        {
            //HandleRemoveAdmin(admin, target.AuthorizedSteamID!.SteamId64);
            admin.PrintToChat(_plugin.T("admin_removed_from_menu"));
        }

        

        public async Task ExecuteDbActionAsync(CCSPlayerController? caller, Func<Task> action, string successMessage, string errorMessage)
        {
            try
            {
                await action();
                caller?.PrintToChat(successMessage);
            }
            catch (Exception ex)
            {
                caller?.PrintToChat(errorMessage);
                Console.WriteLine($"[AdminControlPlugin] ERRO: {ex.Message}");
            }
        }

        private void ShowBanConfirmation(CCSPlayerController admin, CCSPlayerController target)
        {
            var confirmMenu = new ChatMenu(_plugin.T("ban_confirm_menu_title", target.PlayerName), _plugin)
            {
                ExitButton = true,
                MenuTime = 20
            };

            confirmMenu.AddItem(_plugin.T("ban_reason_cheating"), (p, o) => _plugin.BanCommands.HandleBan(p, target.AuthorizedSteamID!.SteamId64, "Cheating"));
            confirmMenu.AddItem(_plugin.T("ban_reason_griefing"), (p, o) => _plugin.BanCommands.HandleBan(p, target.AuthorizedSteamID!.SteamId64, "Griefing"));
            confirmMenu.AddItem(_plugin.T("ban_reason_bug_abuse"), (p, o) => _plugin.BanCommands.HandleBan(p, target.AuthorizedSteamID!.SteamId64, "Abusing bug"));
            confirmMenu.AddItem(_plugin.T("ban_reason_other"), (p, o) => _plugin.BanCommands.HandleBan(p, target.AuthorizedSteamID!.SteamId64, "Other"));
            confirmMenu.AddItem(_plugin.T("cancel_button"), (p, o) => ShowPlayerManagementMenu(p));

            confirmMenu.Display(admin, 20);
        }

        private void ShowIpBanReasonMenu(CCSPlayerController caller, CCSPlayerController target)
        {
            var menu = new ChatMenu(_plugin.T("ip_ban_reason_menu_title", target.PlayerName), _plugin)
            {
                ExitButton = true,
                MenuTime = 20
            };

            if (target.IpAddress is null)
            {
                caller.PrintToChat(_plugin.T("error_ip_not_found"));
                return;
            }

            menu.AddItem(_plugin.T("ban_reason_cheating"), (p, o) => _plugin.BanCommands.HandleIpBan(p, target.IpAddress!, "Cheating"));
            menu.AddItem(_plugin.T("ban_reason_griefing"), (p, o) => _plugin.BanCommands.HandleIpBan(p, target.IpAddress!, "Griefing"));
            menu.AddItem(_plugin.T("ban_reason_bug_abuse"), (p, o) => _plugin.BanCommands.HandleIpBan(p, target.IpAddress!, "Abusing bug"));
            menu.AddItem(_plugin.T("ban_reason_other"), (p, o) => _plugin.BanCommands.HandleIpBan(p, target.IpAddress!, "Other"));

            menu.Display(caller, 20);
        }

        private void ShowKickConfirmation(CCSPlayerController admin, CCSPlayerController target)
        {
            var confirmMenu = new ChatMenu(_plugin.T("kick_confirm_menu_title", target.PlayerName), _plugin)
            {
                ExitButton = true,
                MenuTime = 20
            };

            confirmMenu.AddItem(_plugin.T("confirm_button"), (p, o) =>
            {
                //_plugin.AdminCommands.KickCommand(p, new CommandInfo($"!kick {target.PlayerName}"));
            });

            confirmMenu.AddItem(_plugin.T("cancel_button"), (p, o) => ShowPlayerManagementMenu(p));

            confirmMenu.Display(admin, 20);
        }

        private void ShowMuteConfirmation(CCSPlayerController admin, CCSPlayerController target)
        {
            var confirmMenu = new ChatMenu(_plugin.T("mute_confirm_menu_title", target.PlayerName), _plugin)
            {
                ExitButton = true,
                MenuTime = 20
            };

            confirmMenu.AddItem(_plugin.T("confirm_button"), (p, o) =>
            {
                var steamId = target.AuthorizedSteamID!.SteamId64.ToString();
                var reason = _plugin.T("no_reason");
                //_plugin.MuteCommands.MutePlayerCommand(p, new CommandInfo($"!mute {steamId} {reason}"));
            });

            confirmMenu.AddItem(_plugin.T("cancel_button"), (p, o) => ShowPlayerManagementMenu(p));
            confirmMenu.Display(admin, 20);
        }

        private void ShowSwapTeamConfirmation(CCSPlayerController admin, CCSPlayerController target)
        {
            var confirmMenu = new ChatMenu(_plugin.T("swap_team_confirm_menu_title", target.PlayerName), _plugin)
            {
                ExitButton = true,
                MenuTime = 20
            };

            confirmMenu.AddItem(_plugin.T("confirm_button"), (p, o) =>
            {
                //_plugin.AdminCommands.SwapTeamCommand(p, new CommandInfo($"!swapteam {target.PlayerName}"));
            });

            confirmMenu.AddItem(_plugin.T("cancel_button"), (p, o) => ShowPlayerManagementMenu(p));
            confirmMenu.Display(admin, 20);
        }
    }
}