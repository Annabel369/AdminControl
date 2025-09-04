using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Utils;
using CS2MenuManager.API.Enum;
using CS2MenuManager.API.Menu;
using System.Linq;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using System;
using System.Threading.Tasks;

namespace AdminControlPlugin.commands
{
    internal class PlayerCommand
    {
        private readonly AdminControlPlugin _plugin;

        public PlayerCommand(AdminControlPlugin plugin)
        {
            _plugin = plugin;
        }

        public void ShowAdminAndBanMenu(CCSPlayerController player)
        {
            var menu = new ChatMenu("👮 Menu Admin", _plugin)
            {
                ExitButton = true,
                MenuTime = 20
            };

            menu.AddItem("👥 Gerenciar Jogadores", (p, o) => ShowPlayerManagementMenu(p));
            menu.AddItem("🗳 Votação de Mapa", (p, o) => _plugin.StartMapVote(p));

            menu.Display(player, 20); // Corrigido o erro CS7036
        }

        private void ShowPlayerManagementMenu(CCSPlayerController caller)
        {
            var menu = new ChatMenu("👥 Gerenciar Jogadores", _plugin)
            {
                ExitButton = true,
                MenuTime = 20
            };

            var players = Utilities.GetPlayers().Where(p => p.IsValid).ToList();

            if (!players.Any())
            {
                menu.AddItem("Nenhum jogador disponível.", DisableOption.None);
            }
            else
            {
                foreach (var p in players)
                {
                    menu.AddItem($"👤 {p.PlayerName}", (pc, o) => ShowPlayerActionsMenu(pc, p));
                }
            }

            menu.Display(caller, 20); // Corrigido o erro CS7036
        }

        private void ShowPlayerActionsMenu(CCSPlayerController caller, CCSPlayerController target)
        {
            var menu = new ChatMenu($"Ações para {target.PlayerName}", _plugin)
            {
                ExitButton = true,
                MenuTime = 20
            };

            menu.AddItem($"🚫 Banir (SteamID)", (pc, o) => ShowBanConfirmation(pc, target));
            menu.AddItem($"🌐 Banir IP", (pc, o) => ShowIpBanReasonMenu(pc, target));

            if (AdminManager.GetPlayerAdminData(target) == null)
                menu.AddItem($"➕ Adicionar Admin", (pc, o) => HandleGrantAdminAction(pc, target));
            else
                menu.AddItem($"➖ Remover Admin", (pc, o) => HandleRemoveAdminAction(pc, target));

            menu.AddItem($"👢 Kickar", (pc, o) => ShowKickConfirmation(pc, target));
            menu.AddItem($"🔇 Mutar", (pc, o) => ShowMuteConfirmation(pc, target));
            menu.AddItem($"🔄 Trocar time", (pc, o) => ShowSwapTeamConfirmation(pc, target));

            menu.Display(caller, 20); // Corrigido o erro CS7036
        }

        // Métodos que estavam faltando (erros CS0103)
        private void ShowBanConfirmation(CCSPlayerController admin, CCSPlayerController target)
        {
            var confirmMenu = new ChatMenu($"⚠️ Confirmar banimento de {target.PlayerName}?", _plugin)
            {
                ExitButton = true,
                MenuTime = 20
            };

            confirmMenu.AddItem("✅ Cheating", (p, o) => HandleBanAction(p, target, "Cheating"));
            confirmMenu.AddItem("✅ Griefing", (p, o) => HandleBanAction(p, target, "Griefing"));
            confirmMenu.AddItem("✅ Abusing bug", (p, o) => HandleBanAction(p, target, "Abusing bug"));
            confirmMenu.AddItem("✅ Other", (p, o) => HandleBanAction(p, target, "Other"));
            confirmMenu.AddItem("❌ Cancelar", (p, o) => ShowPlayerManagementMenu(p));

            confirmMenu.Display(admin, 20);
        }

        private void HandleBanAction(CCSPlayerController admin, CCSPlayerController target, string reason)
        {
            try
            {
                Server.ExecuteCommand($"css_ban {target.AuthorizedSteamID!.SteamId64} \"{reason}\"");
                admin.PrintToChat($"🚫 Jogador {target.PlayerName} foi banido por: {reason}.");
            }
            catch (Exception ex)
            {
                admin.PrintToChat("❌ Erro ao banir o jogador.");
                Console.WriteLine($"[AdminControlPlugin] ERRO (ban): {ex.Message}");
            }
        }

        private void ShowIpBanReasonMenu(CCSPlayerController caller, CCSPlayerController target)
        {
            var menu = new ChatMenu($"🌐 Banir IP {target.PlayerName}", _plugin)
            {
                ExitButton = true,
                MenuTime = 20
            };

            menu.AddItem("Cheating", (p, o) => HandleIpBanAction(p, target, "Cheating"));
            menu.AddItem("Griefing", (p, o) => HandleIpBanAction(p, target, "Griefing"));
            menu.AddItem("Abusing bug", (p, o) => HandleIpBanAction(p, target, "Abusing bug"));
            menu.AddItem("Other", (p, o) => HandleIpBanAction(p, target, "Other"));

            menu.Display(caller, 20);
        }

        private void HandleIpBanAction(CCSPlayerController admin, CCSPlayerController target, string reason)
        {
            try
            {
                if (target.IpAddress is null)
                {
                    admin.PrintToChat("❌ Não foi possível obter o IP do jogador.");
                    return;
                }
                Server.ExecuteCommand($"css_ipban {target.IpAddress} \"{reason}\"");
                admin.PrintToChat($"🌐 IP {target.IpAddress} de {target.PlayerName} foi banido.");
            }
            catch (Exception ex)
            {
                admin.PrintToChat("❌ Erro ao banir o IP do jogador.");
                Console.WriteLine($"[AdminControlPlugin] ERRO (ip ban): {ex.Message}");
            }
        }

        private void HandleGrantAdminAction(CCSPlayerController admin, CCSPlayerController target)
        {
            try
            {
                Server.ExecuteCommand($"css_addadmin {target.AuthorizedSteamID!.SteamId64} {target.PlayerName} \"@css/basic\" 1 0");
                admin.PrintToChat($"✅ Jogador {target.PlayerName} agora é um admin.");
            }
            catch (Exception ex)
            {
                admin.PrintToChat("❌ Erro ao adicionar admin.");
                Console.WriteLine($"[AdminControlPlugin] ERRO (grant admin): {ex.Message}");
            }
        }

        private void HandleRemoveAdminAction(CCSPlayerController admin, CCSPlayerController target)
        {
            try
            {
                Server.ExecuteCommand($"css_removeadmin {target.AuthorizedSteamID!.SteamId64}");
                admin.PrintToChat($"✅ Admin removido com sucesso.");
            }
            catch (Exception ex)
            {
                admin.PrintToChat("❌ Erro ao remover admin.");
                Console.WriteLine($"[AdminControlPlugin] ERRO (remove admin): {ex.Message}");
            }
        }

        private void ShowKickConfirmation(CCSPlayerController admin, CCSPlayerController target)
        {
            var confirmMenu = new ChatMenu($"⚠️ Kickar {target.PlayerName}?", _plugin)
            {
                ExitButton = true,
                MenuTime = 20
            };

            confirmMenu.AddItem("✅ Confirmar", (p, o) =>
            {
                try
                {
                    target.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED);
                    admin.PrintToChat($"👢 Jogador {target.PlayerName} foi kickado.");
                }
                catch (Exception ex)
                {
                    admin.PrintToChat("❌ Erro ao kickar o jogador.");
                    Console.WriteLine($"[AdminControlPlugin] ERRO (kick): {ex.Message}");
                }
            });
            confirmMenu.AddItem("❌ Cancelar", (p, o) => ShowPlayerManagementMenu(p));

            confirmMenu.Display(admin, 20);
        }

        private void ShowMuteConfirmation(CCSPlayerController admin, CCSPlayerController target)
        {
            var confirmMenu = new ChatMenu($"🔇 Mutar {target.PlayerName}?", _plugin)
            {
                ExitButton = true,
                MenuTime = 20
            };

            confirmMenu.AddItem("✅ Confirmar", (p, o) =>
            {
                try
                {
                    target.VoiceFlags = 0;
                    p.PrintToChat($"🔇 {target.PlayerName} foi mutado.");
                }
                catch (Exception ex)
                {
                    p.PrintToChat("❌ Erro ao mutar o jogador.");
                    Console.WriteLine($"[AdminControlPlugin] ERRO (mute): {ex.Message}");
                }
            });

            confirmMenu.AddItem("❌ Cancelar", (p, o) => ShowPlayerManagementMenu(p));
            confirmMenu.Display(admin, 20);
        }

        private void ShowSwapTeamConfirmation(CCSPlayerController admin, CCSPlayerController target)
        {
            var confirmMenu = new ChatMenu($"🔄 Trocar time de {target.PlayerName}?", _plugin)
            {
                ExitButton = true,
                MenuTime = 20
            };

            confirmMenu.AddItem("✅ Confirmar", (p, o) =>
            {
                try
                {
                    var newTeam = target.Team == CsTeam.Terrorist ? CsTeam.CounterTerrorist : CsTeam.Terrorist;
                    target.SwitchTeam(newTeam);
                    p.PrintToChat($"🔄 {target.PlayerName} foi movido para o time {newTeam}.");
                }
                catch (Exception ex)
                {
                    p.PrintToChat("❌ Erro ao trocar o time do jogador.");
                    Console.WriteLine($"[AdminControlPlugin] ERRO (swap team): {ex.Message}");
                }
            });

            confirmMenu.AddItem("❌ Cancelar", (p, o) => ShowPlayerManagementMenu(p));
            confirmMenu.Display(admin, 20);
        }
    }
}