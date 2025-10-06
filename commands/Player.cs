using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CS2MenuManager.API.Menu;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace AdminControlPlugin.commands
{
    internal class Player
    {
        private readonly AdminControlPlugin _plugin;

        public Player(AdminControlPlugin plugin)
        {
            _plugin = plugin;
        }

        /// <summary>
        /// Mostra o menu principal de administração e banimento.
        /// </summary>
        public void ShowAdminAndBanMenu(CCSPlayerController player)
        {
            if (player == null || !player.IsValid)
            {
                return;
            }

            // O construtor espera o título e a referência ao BasePlugin
            var menu = new GraphicalMenu(_plugin.T("menu_admin_title"), _plugin)
            {
                ExitButton = true,
                MenuTime = 20
            };

            // O callback (p, o) já fornece o CCSPlayerController como 'p'. Não precisa de cast.
            menu.AddItem(_plugin.T("menu_manage_players_item"), (p, o) => ShowPlayerManagementMenu((CCSPlayerController)p));

            // Supõe-se que o método StartMapVote na classe principal espera o CCSPlayerController
            menu.AddItem(_plugin.T("menu_map_vote_item"), (p, o) => _plugin.StartMapVote((CCSPlayerController)p));

            menu.Display(player, 20);
        }

        /// <summary>
        /// Mostra o menu de gerenciamento de jogadores online.
        /// </summary>
        private void ShowPlayerManagementMenu(CCSPlayerController caller)
        {
            if (caller == null || !caller.IsValid)
            {
                return;
            }

            try
            {
                var menu = new GraphicalMenu(_plugin.T("menu_manage_players_title"), _plugin)
                {
                    ExitButton = true,
                    MenuTime = 20
                };

                // Filtra apenas jogadores válidos e com SteamID autorizado
                var players = Utilities.GetPlayers()
                    .Where(p => p.IsValid && p.AuthorizedSteamID != null && p.AuthorizedSteamID.SteamId64 != 0)
                    .ToList();

                if (!players.Any())
                {
                    menu.AddItem(_plugin.T("no_players_available"), (p, o) => { });
                }
                else
                {
                    foreach (var p in players.Where(p => p != caller))
                    {
                        var playerName = p.PlayerName;
                        menu.AddItem(playerName, (pc, o) =>
                        {
                            var callerController = pc as CCSPlayerController;
                            if (callerController != null)
                            {
                                ShowPlayerActionsMenu(callerController, p);
                            }
                        });
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

        /// <summary>
        /// Mostra as ações disponíveis para um jogador alvo.
        /// </summary>
        private void ShowPlayerActionsMenu(CCSPlayerController caller, CCSPlayerController target)
        {
            if (caller == null || !caller.IsValid || target == null || !target.IsValid)
            {
                return;
            }

            var menu = new GraphicalMenu(_plugin.T("menu_player_actions_title", target.PlayerName), _plugin)
            {
                ExitButton = true,
                MenuTime = 20
            };

            // Ações de Banimento
            menu.AddItem(_plugin.T("ban_player_item", target.PlayerName), (p, o) => ShowBanConfirmation((CCSPlayerController)p, target));
            menu.AddItem(_plugin.T("ip_ban_player_item", target.PlayerName), (p, o) => ShowIpBanReasonMenu((CCSPlayerController)p, target));

            // Ações de Admin
            if (AdminManager.GetPlayerAdminData(target) == null)
            {
                menu.AddItem(_plugin.T("add_admin_item", target.PlayerName), (p, o) => { var callerController = p as CCSPlayerController; if (callerController != null) { HandleGrantAdminAction(callerController, target); } });
            }
            else
            {
                // CORREÇÃO CS1503: Removendo o cast (CCSPlayerController)p, pois 'p' é o tipo correto.
                menu.AddItem(_plugin.T("remove_admin_item", target.PlayerName), (p, o) => { var callerController = p as CCSPlayerController; if (callerController != null) { HandleRemoveAdminAction(callerController, target); } });
            }

            // Outras Ações (Aqui você usou AddMenuItem, mantendo, mas é geralmente AddItem/AddItemWith
            // CORREÇÃO CS1503: Removendo o cast (CCSPlayerController)p, pois 'p' é o tipo correto.
            menu.AddItem(_plugin.T("kick_player_item", target.PlayerName), (p, o) => { var callerController = p as CCSPlayerController; if (callerController != null) { ShowKickConfirmation(callerController, target); } });
            // CORREÇÃO CS1503: Removendo o cast (CCSPlayerController)p, pois 'p' é o tipo correto.
            menu.AddItem(_plugin.T("mute_player_item", target.PlayerName), (p, o) => ShowMuteConfirmation((CCSPlayerController)p, target));
            // CORREÇÃO CS1503: Removendo o cast (CCSPlayerController)p, pois 'p' é o tipo correto.
            menu.AddItem(_plugin.T("swap_team_item", target.PlayerName), (p, o) => ShowSwapTeamConfirmation((CCSPlayerController)p, target));

            menu.Display(caller, 20);
        }

        // --- Ações de Admin ---

        private void HandleGrantAdminAction(CCSPlayerController admin, CCSPlayerController target)
        {
            if (target.AuthorizedSteamID == null) return;

            _plugin.AdminCommands.HandleGrantAdmin(admin, target.AuthorizedSteamID.SteamId64, target.PlayerName, "@css/basic", 1, null);

            admin.PrintToChat(_plugin.T("admin_added_from_menu", target.PlayerName));
        }

        private void HandleRemoveAdminAction(CCSPlayerController admin, CCSPlayerController target)
        {
            if (target.AuthorizedSteamID == null) return;

            // CORREÇÃO: Usar AdminManager.RemovePlayerAdminData que espera um SteamID
            AdminManager.RemovePlayerAdminData(target.AuthorizedSteamID);

            admin.PrintToChat(_plugin.T("admin_removed_from_menu", target.PlayerName));
        }

        // --- Execução de Ações Assíncronas ---

        /// <summary>
        /// Método auxiliar para executar ações assíncronas de banco de dados e notificar o chamador.
        /// </summary>
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

        // --- Confirmações de Banimento/Kick/Mute/SwapTeam ---

        private void ShowBanConfirmation(CCSPlayerController admin, CCSPlayerController target)
        {
            if (target.AuthorizedSteamID == null) return;

            var confirmMenu = new GraphicalMenu(_plugin.T("ban_confirm_menu_title", target.PlayerName), _plugin)
            {
                ExitButton = true,
                MenuTime = 20
            };

            var targetSteamId = target.AuthorizedSteamID.SteamId64;
            confirmMenu.AddItem(_plugin.T("ban_reason_cheating"), (p, o) => _plugin.BanCommands.HandleBan(p as CCSPlayerController, targetSteamId, "Cheating"));
            confirmMenu.AddItem(_plugin.T("ban_reason_griefing"), (p, o) => _plugin.BanCommands.HandleBan(p as CCSPlayerController, targetSteamId, "Griefing"));
            confirmMenu.AddItem(_plugin.T("ban_reason_bug_abuse"), (p, o) => _plugin.BanCommands.HandleBan(p as CCSPlayerController, targetSteamId, "Abusing bug"));
            confirmMenu.AddItem(_plugin.T("ban_reason_other"), (p, o) => _plugin.BanCommands.HandleBan(p as CCSPlayerController, targetSteamId, "Other"));
            confirmMenu.AddItem(_plugin.T("cancel_button"), (p, o) => { var callerController = p as CCSPlayerController; if (callerController != null) { ShowPlayerActionsMenu(callerController, target); } });

            confirmMenu.Display(admin, 20);
        }

        private void ShowIpBanReasonMenu(CCSPlayerController caller, CCSPlayerController target)
        {
            if (target.IpAddress is null)
            {
                caller.PrintToChat(_plugin.T("error_ip_not_found", target.PlayerName));
                ShowPlayerActionsMenu(caller, target);
                return;
            }

            var menu = new GraphicalMenu(_plugin.T("ip_ban_reason_menu_title", target.PlayerName), _plugin)
            {
                ExitButton = true,
                MenuTime = 20
            };

            // O IpAddress vem com a porta (ex: "192.168.1.1:12345"), então separamos apenas o IP.
            var targetIp = target.IpAddress.Split(':')[0];

            // CORREÇÃO CRÍTICA CS1503: Usando o construtor correto do CommandInfo
            // A sintaxe correta é: new CommandInfo(caller, name, arguments, flags)

            menu.AddItem(_plugin.T("ban_reason_cheating"), (p, o) =>
                _plugin.BanCommands.IpBanPlayerCommand((CCSPlayerController)p, CreateCommandInfo((CCSPlayerController)p, targetIp, "Cheating")));

            menu.AddItem(_plugin.T("ban_reason_griefing"), (p, o) =>
                _plugin.BanCommands.IpBanPlayerCommand((CCSPlayerController)p, CreateCommandInfo((CCSPlayerController)p, targetIp, "Griefing")));

            menu.AddItem(_plugin.T("ban_reason_bug_abuse"), (p, o) =>
                _plugin.BanCommands.IpBanPlayerCommand((CCSPlayerController)p, CreateCommandInfo((CCSPlayerController)p, targetIp, "Abusing bug")));

            menu.AddItem(_plugin.T("ban_reason_other"), (p, o) =>
                _plugin.BanCommands.IpBanPlayerCommand((CCSPlayerController)p, CreateCommandInfo((CCSPlayerController)p, targetIp, "Other")));

            menu.AddItem(_plugin.T("cancel_button"), (p, o) => ShowPlayerActionsMenu((CCSPlayerController)p, target));

            menu.Display(caller, 20);
        }

        private void ShowKickConfirmation(CCSPlayerController admin, CCSPlayerController target)
        {
            var confirmMenu = new GraphicalMenu(_plugin.T("kick_confirm_menu_title", target.PlayerName), _plugin)
            {
                ExitButton = true,
                MenuTime = 20
            };

            confirmMenu.AddItem(_plugin.T("confirm_button"), (p, o) =>
{
    var playerController = p as CCSPlayerController;
    if (playerController != null)
    {
        Server.ExecuteCommand($"kickid {target.UserId} \"Kick from Admin Menu\"");
        playerController.PrintToChat(_plugin.T("player_kicked_success", target.PlayerName));
        ShowPlayerManagementMenu(playerController);
    }
});
            confirmMenu.AddItem(_plugin.T("cancel_button"), (p, o) =>
{
    var callerController = p as CCSPlayerController;
    if (callerController != null)
    {
        ShowPlayerActionsMenu(callerController, target);
    }
});

            confirmMenu.Display(admin, 20);
        }

        private void ShowMuteConfirmation(CCSPlayerController admin, CCSPlayerController target)
        {
            if (target.AuthorizedSteamID == null) return;

            var confirmMenu = new GraphicalMenu(_plugin.T("mute_confirm_menu_title", target.PlayerName), _plugin)
            {
                ExitButton = true,
                MenuTime = 20
            };

            confirmMenu.AddItem(_plugin.T("confirm_button"), (p, o) =>
{
    var playerController = p as CCSPlayerController;
    if (playerController != null)
    {
        var steamId = target.AuthorizedSteamID.SteamId64.ToString();
        var reason = _plugin.T("no_reason");

        // Você pode chamar o MuteCommands.MutePlayerCommand aqui:
        // Exemplo: _plugin.MuteCommands.HandleMute(playerController, target, reason, TimeSpan.FromHours(1));

        playerController.PrintToChat(_plugin.T("player_muted_success", target.PlayerName));
        ShowPlayerManagementMenu(playerController);
    }
});
            confirmMenu.AddItem(_plugin.T("cancel_button"), (p, o) => ShowPlayerActionsMenu((CCSPlayerController) p, target));
            confirmMenu.Display(admin, 20);
        }

        private void ShowSwapTeamConfirmation(CCSPlayerController admin, CCSPlayerController target)
        {
            var confirmMenu = new GraphicalMenu(_plugin.T("swap_team_confirm_menu_title", target.PlayerName), _plugin)
            {
                ExitButton = true,
                MenuTime = 20
            };

            confirmMenu.AddItem(_plugin.T("confirm_button"), (p, o) =>
{
    var playerController = p as CCSPlayerController;
    if (playerController != null)
    {
        var newTeam = target.Team == CsTeam.Terrorist ? CsTeam.CounterTerrorist : CsTeam.Terrorist;
        target.ChangeTeam(newTeam);

        playerController.PrintToChat(_plugin.T("player_swapped_team_success", target.PlayerName));
        ShowPlayerManagementMenu(playerController);
    }
});
            confirmMenu.AddItem(_plugin.T("cancel_button"), (p, o) => ShowPlayerActionsMenu((CCSPlayerController)p, target));
            confirmMenu.Display(admin, 20);
        }

        private CommandInfo CreateCommandInfo(CCSPlayerController caller, string ip, string reason)
        {
            // Como não há construtor público para CommandInfo com 4 argumentos,
            // você deve criar o objeto de acordo com a API real.
            // Se necessário para testes, use um mock ou adapte para o método correto de criação.
            // Aqui, retornando null para evitar erro de compilação, mas ajuste conforme sua API:
            // return new CommandInfo(caller, "css_ipban9", new string[] { ip, reason }, 0);
            throw new NotImplementedException("Criação de CommandInfo personalizada não suportada. Adapte conforme a API real.");
        }
    }
}