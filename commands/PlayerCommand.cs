using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using CS2MenuManager.API.Enum;
using CS2MenuManager.API.Menu;
using Dapper;
using System.Collections.Generic;
using System.Linq;
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
            //menu.AddItem("🔷 Lista de Admins", async (p, o) => await ShowAdminList(p));
            //menu.AddItem("🚫 Banidos por SteamID", async (p, o) => await ShowSteamBanList(p));
            //menu.AddItem("🌐 Banidos por IP", async (p, o) => await ShowIpBanList(p));
            menu.AddItem("🗳 Votação de Mapa", (p, o) => _plugin.StartMapVote(p));

            menu.Display(player, 20);
        }

        private async Task ShowIpBanList(CCSPlayerController player)
        {
            try
            {
                var menu = new ChatMenu("🌐 Banidos por IP", _plugin)
                {
                    ExitButton = true,
                    MenuTime = 20
                };

                using var connection = await _plugin.GetOpenConnectionAsync();
                var bans = await connection.QueryAsync<AdminControlPlugin.IpBanEntry>("SELECT ip_address, reason, timestamp FROM ip_bans WHERE unbanned = FALSE;");

                if (!bans.Any())
                {
                    menu.AddItem("Nenhum IP banido.", DisableOption.None);
                }
                else
                {
                    foreach (var ban in bans)
                    {
                        menu.AddItem($"{ban.ip_address!} - {ban.reason} ({ban.timestamp:dd/MM/yyyy})", (p, o) => HandleUnbanIpAction(p, ban.ip_address!));
                    }
                }

                menu.Display(player, 20);
            }
            catch (Exception ex)
            {
                player.PrintToChat("❌ Erro ao carregar a lista de IPs banidos.");
                Console.WriteLine($"[AdminControlPlugin] ERRO (menu): {ex.Message}");
            }
        }

        private void HandleUnbanIpAction(CCSPlayerController player, string ipAddress)
        {
            try
            {
                Server.ExecuteCommand($"css_unbanip {ipAddress}");
                player.PrintToChat($"✅ IP {ipAddress} desbanido.");
            }
            catch (Exception ex)
            {
                player.PrintToChat("❌ Erro ao desbanir o IP.");
                Console.WriteLine($"[AdminControlPlugin] ERRO (unban ip): {ex.Message}");
            }
        }

        private void ShowPlayerManagementMenu(CCSPlayerController caller)
        {
            try
            {
                var menu = new ChatMenu("👥 Gerenciar Jogadores", _plugin)
                {
                    ExitButton = true,
                    MenuTime = 20
                };

                var players = Utilities.GetPlayers()
                    .Where(p => p.IsValid && p.AuthorizedSteamID != null)
                    .ToList();

                if (!players.Any())
                {
                    menu.AddItem("Nenhum jogador disponível.", DisableOption.None);
                }
                else
                {
                    foreach (var p in players)
                    {
                        menu.AddItem($"🚫 Banir {p.PlayerName}", (pc, o) => ShowBanConfirmation(pc, p));
                        menu.AddItem($"🌐 Banir IP {p.PlayerName}", (pc, o) => ShowIpBanReasonMenu(pc, p));

                        if (AdminManager.GetPlayerAdminData(p) == null)
                            menu.AddItem($"➕ Adicionar Admin {p.PlayerName}", (pc, o) => HandleGrantAdminAction(pc, p));
                        else
                            menu.AddItem($"➖ Remover Admin {p.PlayerName}", (pc, o) => HandleRemoveAdminAction(pc, p));

                        menu.AddItem($"👢 Kickar {p.PlayerName}", (pc, o) => ShowKickConfirmation(pc, p));
                        menu.AddItem($"🔇 Mutar {p.PlayerName}", (pc, o) => ShowMuteConfirmation(pc, p));
                        menu.AddItem($"🔄 Trocar time {p.PlayerName}", (pc, o) => ShowSwapTeamConfirmation(pc, p));

                        menu.AddItem("----------------------", DisableOption.None);
                    }
                }

                menu.Display(caller, 20);
            }
            catch (Exception ex)
            {
                caller.PrintToChat("❌ Erro ao carregar o menu de gerenciamento de jogadores.");
                Console.WriteLine($"[AdminControlPlugin] ERRO (player menu): {ex.Message}");
            }
        }

        private void HandleGrantAdminAction(CCSPlayerController admin, CCSPlayerController target)
        {
            try
            {
                // Comando de console para adicionar admin
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
                // Comando de console para remover admin
                Server.ExecuteCommand($"css_removeadmin {target.AuthorizedSteamID!.SteamId64}");
                admin.PrintToChat($"✅ Admin removido com sucesso.");
            }
            catch (Exception ex)
            {
                admin.PrintToChat("❌ Erro ao remover admin.");
                Console.WriteLine($"[AdminControlPlugin] ERRO (remove admin): {ex.Message}");
            }
        }

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

        private async Task ShowAdminList(CCSPlayerController player)
        {
            try
            {
                var menu = new ChatMenu("🔷 Admins Ativos", _plugin)
                {
                    ExitButton = true,
                    MenuTime = 20
                };

                using var connection = await _plugin.GetOpenConnectionAsync();
                var admins = await connection.QueryAsync<AdminControlPlugin.DbAdmin>("SELECT steamid, name, permission, level FROM admins;");

                if (!admins.Any())
                    menu.AddItem("Nenhum admin ativo.", DisableOption.None);
                else
                    foreach (var admin in admins)
                        menu.AddItem($"{admin.name} ({admin.steamid}) - {admin.permission} [Lv.{admin.level}]", DisableOption.None);

                menu.Display(player, 20);
            }
            catch (Exception ex)
            {
                player.PrintToChat("❌ Erro ao carregar a lista de admins.");
                Console.WriteLine($"[AdminControlPlugin] ERRO (menu): {ex.Message}");
            }
        }

        private async Task ShowSteamBanList(CCSPlayerController player)
        {
            try
            {
                var menu = new ChatMenu("🚫 Banidos por SteamID", _plugin)
                {
                    ExitButton = true,
                    MenuTime = 20
                };

                using var connection = await _plugin.GetOpenConnectionAsync();
                var bans = await connection.QueryAsync<AdminControlPlugin.BanEntry>("SELECT steamid, reason, timestamp FROM bans WHERE unbanned = FALSE;");

                if (!bans.Any())
                {
                    menu.AddItem("Nenhum jogador banido.", DisableOption.None);
                }
                else
                {
                    foreach (var ban in bans)
                    {
                        menu.AddItem($"{ban.steamid} - {ban.reason} ({ban.timestamp:dd/MM/yyyy})", (p, o) => HandleUnbanAction(p, ban.steamid));
                    }
                }

                menu.Display(player, 20);
            }
            catch (Exception ex)
            {
                player.PrintToChat("❌ Erro ao carregar a lista de banidos.");
                Console.WriteLine($"[AdminControlPlugin] ERRO (menu): {ex.Message}");
            }
        }

        private void HandleUnbanAction(CCSPlayerController player, ulong steamId)
        {
            try
            {
                Server.ExecuteCommand($"css_unban {steamId}");
                player.PrintToChat($"✅ Jogador {steamId} desbanido.");
            }
            catch (Exception ex)
            {
                player.PrintToChat("❌ Erro ao desbanir o jogador.");
                Console.WriteLine($"[AdminControlPlugin] ERRO (unban): {ex.Message}");
            }
        }
    }
}