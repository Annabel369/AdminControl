using AdminControlPlugin.commands;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Dapper;
using MySqlConnector;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using Timer = System.Timers.Timer;

namespace AdminControlPlugin;

[MinimumApiVersion(130)]
public class AdminControlPlugin : BasePlugin, IPluginConfig<AdminControlConfig>
{
    // ... [Propriedades e Configurações (Mantidas)] ...
    public override string ModuleName => "Admin Control with MySQL & CFG Sync";
    public override string ModuleVersion => "16.0.2";
    public override string ModuleAuthor => "Amauri Bueno dos Santos & Gemini";
    public override string ModuleDescription => "Plugin completo para banimentos, admins e RCON com MySQL e sincronização com arquivos de configuração nativos do servidor.";

    private string _connectionString = string.Empty;
    public AdminControlConfig Config { get; set; } = new AdminControlConfig();
    private Timer? _adminCheckTimer;

    public Ban BanCommands { get; private set; } = null!;
    public Admin AdminCommands { get; private set; } = null!;
    public Mute MuteCommands { get; private set; } = null!;
    public PlayerCommand PlayerCommands { get; private set; } = null!;

    public string T(string key, params object[] args)
    {
        return Localizer[key, args];
    }

    public void OnConfigParsed(AdminControlConfig config)
    {
        Config = config;
        _connectionString = $"server={Config.Host};uid={Config.User};pwd={Config.Password};database={Config.Database}";
    }

    public override void Load(bool hotReload)
    {
        // ... [Inicialização e Listeners (Mantidos)] ...
        try
        {
            BanCommands = new Ban(this);
            AdminCommands = new Admin(this);
            MuteCommands = new Mute(this);
            PlayerCommands = new PlayerCommand(this);

            Task.Run(async () =>
            {
                await EnsureDatabaseAndTablesExistAsync();
                await AdminCommands.GenerateAdminsJsonAsync();
                await BanCommands.LoadBansFromDatabaseAsync();
                await MuteCommands.LoadMutesFromDatabaseAsync();
                EnsureSharedConfigFilesExist();
            });

            RegisterListener<Listeners.OnMapStart>(OnMapStart);
            RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFullCheckBan);

            RegisterCommands();

            StartAdminCheckTimer();

            Console.WriteLine($"[AdminControlPlugin] {T("plugin_loaded")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminControlPlugin] {T("plugin_load_error", ex.Message)}");
        }
    }

    private void RegisterCommands()
    {
        AddCommand("css_ban", "Ban a player by SteamID64", BanCommands.BanPlayerCommand);
        AddCommand("css_unban", "Unban a player by SteamID64", BanCommands.UnbanPlayerCommand);
        AddCommand("css_listbans", "List all banned players", BanCommands.ListBans);
        AddCommand("css_rcon", "Execute RCON command", ExecuteRcon);
        AddCommand("css_addadmin", "Grant custom admin with permission and duration", AdminCommands.GrantCustomAdmin);
        AddCommand("css_removeadmin", "Remove a custom admin by SteamID64", RemoveAdminCommand);

        AddCommand("css_admincontrol", "Abre o menu de administração", Command_AdminMenu);

        // Comandos de Mute
        AddCommand("css_mute", "Mute um jogador pelo SteamID64", MuteCommands.MutePlayerCommand);
        AddCommand("css_unmute", "Desmute um jogador pelo SteamID64", MuteCommands.UnmutePlayerCommand);

        // Comandos de Banimento por IP
        AddCommand("css_ipban", "Banir um IP", IpBanPlayerCommand);
        AddCommand("css_unbanip", "Unban a player by IP Address", BanCommands.UnbanIpCommand);

        AddCommand("!kick", "Kicka um jogador", AdminCommands.KickCommand);
        AddCommand("!mute", "Muta um jogador", MuteCommands.MutePlayerCommand);
        AddCommand("!swapteam", "Move jogador para o outro time", AdminCommands.SwapTeamCommand);
    }

    public override void Unload(bool hotReload)
    {
        _adminCheckTimer?.Stop();
        _adminCheckTimer?.Dispose();
        Console.WriteLine($"[AdminControlPlugin] {T("plugin_unloaded")}");
    }

    // ... [OnPlayerConnectFullCheckBan (Mantido)] ...
    public HookResult OnPlayerConnectFullCheckBan(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;

        if (player == null || !player.IsValid || player.IsBot)
        {
            return HookResult.Continue;
        }

        ulong steamId = player.AuthorizedSteamID?.SteamId64 ?? 0;

        string? rawIp = string.IsNullOrWhiteSpace(player.IpAddress) ? null : player.IpAddress;
        string? ip = rawIp?.Split(':')[0];

        // SteamID Ban Check
        if (BanCommands.IsBanned(steamId))
        {
            string reason = BanCommands.GetBanReason(steamId) ?? T("no_reason");
            Server.ExecuteCommand($"kickid {player.UserId} \"{T("kick_ban_message", reason)}\"");
            Server.PrintToConsole($"[AdminControlPlugin] {T("log_player_banned", player.UserId ?? 0, steamId, reason)}");
            return HookResult.Stop;
        }

        // IP Ban Check
        if (ip != null && BanCommands.IsIpBanned(ip))
        {
            string reason = BanCommands.GetIpBanReason(ip) ?? T("no_reason");
            // Nota: O CS2 não tem kickip nativo, mas o banimento por IP já deve impedir a conexão.
            Server.ExecuteCommand($"kick {ip} \"{T("kick_ip_ban_message", reason)}\"");
            Server.PrintToConsole($"[AdminControlPlugin] {T("log_ip_banned", ip, reason)}");
            return HookResult.Stop;
        }

        // Mute Check
        if (MuteCommands.IsMuted(steamId))
        {
            player.VoiceFlags = (VoiceFlags)0;
            player.PrintToChat(T("you_are_muted"));
            Server.PrintToConsole($"[AdminControlPlugin] {T("log_player_muted", player.UserId ?? 0)}");
        }

        Server.PrintToConsole($"[AdminControlPlugin] {T("log_player_connected", player.UserId ?? 0, steamId, ip ?? "n/a")}");
        return HookResult.Continue;
    }
    // ... [RemoveAdminCommand e HandleRemoveAdmin (Mantidos)] ...
    [RequiresPermissions("@css/root")]
    public void RemoveAdminCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2 || !ulong.TryParse(info.GetArg(1), out var steamId))
        {
            caller?.PrintToChat("Uso: css_removeadmin <steamid64>");
            return;
        }
        HandleRemoveAdmin(caller, steamId);
    }

    [RequiresPermissions("@css/root")]
    public void HandleRemoveAdmin(CCSPlayerController? caller, ulong steamId)
    {
        Task.Run(async () => await ExecuteDbActionAsync(caller, async () =>
        {
            await using var connection = await GetOpenConnectionAsync();
            await connection.ExecuteAsync("DELETE FROM admins WHERE steamid = @SteamId;", new { SteamId = steamId });
            await AdminCommands.GenerateAdminsJsonAsync();
        }, T("admin_removed_success"), T("admin_removed_error")));
    }


    // ----------------------------------------------------------------------
    // --- IMPLEMENTAÇÃO DE BAN/UNBAN (CORREÇÃO CRÍTICA DE THREADING) ---
    // ----------------------------------------------------------------------

    // Função de comando para IP BAN que chama o manipulador protegido
    [RequiresPermissions("@css/ban")]
    public void IpBanPlayerCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2)
        {
            caller?.PrintToChat(T("ip_ban_usage"));
            return;
        }

        var rawIp = info.GetArg(1);
        var ipAddress = rawIp.Split(':')[0];

        if (!IPAddress.TryParse(ipAddress, out _))
        {
            caller?.PrintToChat(T("ip_ban_usage"));
            return;
        }

        var reason = info.ArgCount > 2
            ? string.Join(" ", Enumerable.Range(2, info.ArgCount - 2).Select(i => info.GetArg(i)))
            : T("no_reason");

        HandleIpBan(caller, ipAddress, reason);
    }

    // Manipulador de Banimento por SteamID (Protegido)
    public async void HandleBan(CCSPlayerController? caller, ulong steamId, string reason)
    {
        await Task.Run(async () =>
        {
            try
            {
                // 1. Lógica do DB
                using var connection = await GetOpenConnectionAsync();

                var isAlreadyBanned = await connection.ExecuteScalarAsync<bool>(
                    "SELECT EXISTS(SELECT 1 FROM bans WHERE steamid = @SteamId AND unbanned = FALSE)", new { SteamId = steamId });

                if (isAlreadyBanned)
                {
                    Server.NextFrame(() => caller?.PrintToChat(T("player_already_banned")));
                    return;
                }

                await connection.ExecuteAsync(@"
                    INSERT INTO bans (steamid, reason, unbanned) VALUES (@SteamId, @Reason, FALSE)
                    ON DUPLICATE KEY UPDATE reason = @Reason, unbanned = FALSE, timestamp = CURRENT_TIMESTAMP;",
                    new { SteamId = steamId, Reason = reason });

                // Recarrega dados
                await BanCommands.LoadBansFromDatabaseAsync();

                // 2. Lógica do Jogo (Thread Principal)
                Server.NextFrame(() =>
                {
                    Server.PrintToConsole($"[AdminControlPlugin] {T("log_player_banned_reason", caller?.PlayerName ?? "Console", steamId, reason)}");

                    var playerToKick = Utilities.GetPlayers().FirstOrDefault(p => p.AuthorizedSteamID?.SteamId64 == steamId);
                    if (playerToKick?.IsValid ?? false)
                    {
                        Server.ExecuteCommand($"kickid {playerToKick.UserId} \"{T("kick_ban_message", reason)}\"");
                    }

                    Server.ExecuteCommand($"banid 0 {steamId}");
                    Server.ExecuteCommand($"writeid");

                    caller?.PrintToChat(T("player_banned", steamId, reason));
                });
            }
            catch (Exception ex)
            {
                Server.NextFrame(() => caller?.PrintToChat(T("error_banning_player")));
                Console.WriteLine($"[AdminControlPlugin] ERRO BAN: {ex.Message}");
            }
        });
    }

    // Manipulador de Desbanimento por SteamID (Protegido)
    public async void HandleUnban(CCSPlayerController? caller, ulong steamId)
    {
        await Task.Run(async () =>
        {
            try
            {
                // 1. Lógica do DB
                using var connection = await GetOpenConnectionAsync();
                var updatedRows = await connection.ExecuteAsync("UPDATE bans SET unbanned = TRUE WHERE steamid = @SteamId AND unbanned = FALSE;",
                    new { SteamId = steamId });

                if (updatedRows > 0)
                {
                    // Recarrega dados
                    await BanCommands.LoadBansFromDatabaseAsync();

                    // 2. Lógica do Jogo (Thread Principal)
                    Server.NextFrame(() =>
                    {
                        Server.PrintToConsole($"[AdminControlPlugin] {T("log_player_unbanned", caller?.PlayerName ?? "Console", steamId)}");
                        Server.ExecuteCommand($"removeid {steamId}");
                        Server.ExecuteCommand($"writeid");
                        caller?.PrintToChat(T("player_unbanned", steamId));
                        Console.WriteLine($"[AdminControlPlugin] {T("log_player_unbanned", caller?.PlayerName ?? "Console", steamId)}");
                    });
                }
                else
                {
                    Server.NextFrame(() => caller?.PrintToChat(T("player_not_banned", steamId)));
                }
            }
            catch (Exception ex)
            {
                Server.NextFrame(() => caller?.PrintToChat(T("error_unbanning_player")));
                Console.WriteLine($"[AdminControlPlugin] ERRO UNBAN: {ex.Message}");
            }
        });
    }

    // Manipulador de Banimento por IP (Protegido)
    public async void HandleIpBan(CCSPlayerController? caller, string ipAddress, string reason)
    {
        await Task.Run(async () =>
        {
            try
            {
                ipAddress = ipAddress.Split(':')[0];

                // 1. Lógica do DB
                using var connection = await GetOpenConnectionAsync();

                var existingBan = await connection.QueryFirstOrDefaultAsync<Ban.IpBanEntry>(
                    "SELECT * FROM ip_bans WHERE ip_address = @IpAddress LIMIT 1",
                    new { IpAddress = ipAddress });

                if (existingBan != null && !existingBan.unbanned)
                {
                    Server.NextFrame(() => caller?.PrintToChat(T("ip_already_banned_chat", ipAddress)));
                    return;
                }

                await connection.ExecuteAsync(@"
                    INSERT INTO ip_bans (ip_address, reason, timestamp, unbanned)
                    VALUES (@IpAddress, @Reason, NOW(), FALSE)
                    ON DUPLICATE KEY UPDATE reason = @Reason, unbanned = FALSE, timestamp = NOW();",
                    new { IpAddress = ipAddress, Reason = reason });

                // Recarrega dados
                await BanCommands.LoadBansFromDatabaseAsync();

                // 2. Lógica do Jogo (Thread Principal)
                Server.NextFrame(() =>
                {
                    Server.PrintToConsole($"[AdminControlPlugin] {T("log_ip_banned", caller?.PlayerName ?? "Console", ipAddress)}");
                    Server.ExecuteCommand($"banip 0 {ipAddress} \"{reason}\"");
                    Server.ExecuteCommand($"writeip");
                    caller?.PrintToChat(T("ip_banned", ipAddress, reason));
                });
            }
            catch (Exception ex)
            {
                Server.NextFrame(() => caller?.PrintToChat(T("error_banning_ip")));
                Console.WriteLine($"[AdminControlPlugin] ERRO IP BAN: {ex.Message}");
            }
        });
    }

    // --- ADICIONE/SUBSTITUA NO AdminControlPlugin.cs ---

    // Manipulador de Mute (Protegido)
    public async void HandleMute(CCSPlayerController? caller, ulong steamId, string reason, string? targetPlayerName)
    {
        await Task.Run(async () =>
        {
            try
            {
                // 1. Lógica do DB
                using var connection = await GetOpenConnectionAsync();

                // Verifica se já está mutado para evitar logs duplicados
                var isAlreadyMuted = MuteCommands.IsMuted(steamId);

                await connection.ExecuteAsync(@"
                INSERT INTO mutes (steamid, reason, unmuted) VALUES (@SteamId, @Reason, FALSE)
                ON DUPLICATE KEY UPDATE reason = @Reason, unmuted = FALSE, timestamp = CURRENT_TIMESTAMP;",
                    new { SteamId = steamId, Reason = reason });

                // Recarrega dados em memória (Seguro em Task.Run)
                await MuteCommands.LoadMutesFromDatabaseAsync();

                // 2. Lógica do Jogo (Thread Principal)
                Server.NextFrame(() =>
                {
                    var playerToMute = Utilities.GetPlayers().FirstOrDefault(p => p.AuthorizedSteamID?.SteamId64 == steamId);

                    // Aplica o Mute se o jogador estiver online
                    if (playerToMute?.IsValid ?? false)
                    {
                        playerToMute.VoiceFlags = (VoiceFlags)0; // Desativa flags de voz/chat
                        playerToMute.PrintToChat(T("you_have_been_muted", reason));
                    }

                    if (!isAlreadyMuted)
                    {
                        // Usa o nome se estiver online, senão usa o SteamID.
                        string nameOrId = targetPlayerName ?? steamId.ToString();
                        caller?.PrintToChat(T("player_muted_success", nameOrId, reason));
                        Server.PrintToConsole($"[AdminControlPlugin] {T("log_player_muted_reason", caller?.PlayerName ?? "Console", nameOrId, steamId, reason)}");
                    }
                    else
                    {
                        caller?.PrintToChat(T("player_already_muted"));
                    }
                });
            }
            catch (Exception ex)
            {
                Server.NextFrame(() => caller?.PrintToChat(T("mute_error")));
                Console.WriteLine($"[AdminControlPlugin] {T("mute_error_log", ex.Message)}");
            }
        });
    }

    // Manipulador de Unmute (Protegido)
    public async void HandleUnmute(CCSPlayerController? caller, ulong steamId)
    {
        await Task.Run(async () =>
        {
            try
            {
                // 1. Lógica do DB
                using var connection = await GetOpenConnectionAsync();
                var rowsAffected = await connection.ExecuteAsync(@"
                UPDATE mutes
                SET unmuted = TRUE
                WHERE steamid = @SteamId AND unmuted = FALSE;",
                    new { SteamId = steamId });

                if (rowsAffected > 0)
                {
                    // Recarrega dados
                    await MuteCommands.LoadMutesFromDatabaseAsync();

                    // 2. Lógica do Jogo (Thread Principal)
                    Server.NextFrame(() =>
                    {
                        var targetPlayer = Utilities.GetPlayers().FirstOrDefault(p => p.AuthorizedSteamID?.SteamId64 == steamId);

                        // Remove o Mute se o jogador estiver online
                        if (targetPlayer?.IsValid ?? false)
                        {
                            // 2 é a flag padrão para permitir voz/chat.
                            targetPlayer.VoiceFlags = (VoiceFlags)2;
                            caller?.PrintToChat(T("player_unmuted_name", targetPlayer.PlayerName));
                            targetPlayer.PrintToChat(T("you_have_been_unmuted"));
                            Server.PrintToConsole($"[AdminControlPlugin] you_have_been_unmuted: {targetPlayer.PlayerName}");
                        }
                        else
                        {
                            caller?.PrintToChat(T("player_unmuted_steamid", steamId));
                        }
                        Server.PrintToConsole($"[AdminControlPlugin] {T("log_player_unmuted_steamid_log", caller?.PlayerName ?? "Console", steamId)}");
                    });
                }
                else
                {
                    Server.NextFrame(() => caller?.PrintToChat(T("player_not_muted")));
                }
            }
            catch (Exception ex)
            {
                Server.NextFrame(() => caller?.PrintToChat(T("unmute_error")));
                Console.WriteLine($"[AdminControlPlugin] {T("unmute_error_log", ex.Message)}");
            }
        });
    }

    // Manipulador de Desbanimento por IP (Protegido)
    public async void HandleUnbanIp(CCSPlayerController? caller, string ipAddress)
    {
        await Task.Run(async () =>
        {
            try
            {
                // 1. Lógica do DB
                using var connection = await GetOpenConnectionAsync();
                var updatedRows = await connection.ExecuteAsync(@"
                    UPDATE ip_bans SET unbanned = TRUE WHERE ip_address = @IpAddress AND unbanned = FALSE;",
                    new { IpAddress = ipAddress });

                if (updatedRows > 0)
                {
                    // Recarrega dados
                    await BanCommands.LoadBansFromDatabaseAsync();

                    // 2. Lógica do Jogo (Thread Principal)
                    Server.NextFrame(() =>
                    {
                        Server.PrintToConsole($"[AdminControlPlugin] {T("log_unbanned_ip", caller?.PlayerName ?? "Console", ipAddress)}");
                        Server.ExecuteCommand($"removeip {ipAddress}");
                        Server.ExecuteCommand($"writeip");
                        Console.WriteLine($"[AdminControlPlugin] {T("log_unbanned_ip", caller?.PlayerName ?? "Console", ipAddress)}");
                        caller?.PrintToChat(T("ip_unbanned", ipAddress));
                    });
                }
                else
                {
                    Server.NextFrame(() => caller?.PrintToChat(T("ip_not_banned", ipAddress)));
                }
            }
            catch (Exception ex)
            {
                Server.NextFrame(() => caller?.PrintToChat(T("error_unbanning_ip")));
                Console.WriteLine($"[AdminControlPlugin] ERRO UNBAN IP: {ex.Message}");
            }
        });
    }


    // --- MÉTODOS AUXILIARES ---

    private void Command_AdminMenu(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null) return;
        PlayerCommands.ShowAdminAndBanMenu(player);
    }

    public async Task ExecuteDbActionAsync(CCSPlayerController? caller, Func<Task> action, string successMessage, string errorMessage)
    {
        try
        {
            await action();
            // Garante que a mensagem de sucesso seja enviada na thread principal
            Server.NextFrame(() => caller?.PrintToChat(successMessage));
        }
        catch (Exception ex)
        {
            // Garante que a mensagem de erro seja enviada na thread principal
            Server.NextFrame(() => caller?.PrintToChat(errorMessage));
            Console.WriteLine($"[AdminControlPlugin] ERRO DB ACTION: {ex.Message}");
        }
    }

    // ... [StartAdminCheckTimer, GetOpenConnectionAsync, EnsureDatabaseAndTablesExistAsync, OnMapStart, ExecuteRcon, EnsureSharedConfigFilesExist (Mantidos)] ...
    private void StartAdminCheckTimer()
    {
        _adminCheckTimer = new Timer(TimeSpan.FromMinutes(5).TotalMilliseconds);
        _adminCheckTimer.Elapsed += async (sender, e) => await AdminCommands.RemoveExpiredAdminsAsync();
        _adminCheckTimer.AutoReset = true;
        _adminCheckTimer.Start();
    }

    public async Task<MySqlConnection> GetOpenConnectionAsync()
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private async Task EnsureDatabaseAndTablesExistAsync()
    {
        var baseConnectionString = $"server={Config.Host};uid={Config.User};pwd={Config.Password};";
        using var connection = new MySqlConnection(baseConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync($"CREATE DATABASE IF NOT EXISTS `{Config.Database}`;");
        await connection.ChangeDatabaseAsync(Config.Database);

        // Criação de tabelas (mantido igual, está correta)
        await connection.ExecuteAsync(@"
        CREATE TABLE IF NOT EXISTS bans (
            steamid BIGINT UNSIGNED NOT NULL, reason VARCHAR(255), unbanned BOOLEAN NOT NULL DEFAULT FALSE, 
            timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP, PRIMARY KEY (steamid)
        );");
        await connection.ExecuteAsync(@"
        CREATE TABLE IF NOT EXISTS ip_bans (
            ip_address VARCHAR(45) NOT NULL, reason VARCHAR(255), unbanned BOOLEAN NOT NULL DEFAULT FALSE, 
            timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP, PRIMARY KEY (ip_address)
        );");
        await connection.ExecuteAsync(@"
        CREATE TABLE IF NOT EXISTS admins (
            steamid BIGINT UNSIGNED NOT NULL, name VARCHAR(64), permission VARCHAR(64), level INT NOT NULL, 
            expires_at DATETIME, granted_by BIGINT UNSIGNED, timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP, PRIMARY KEY (steamid)
        );");
        await connection.ExecuteAsync(@"
        CREATE TABLE IF NOT EXISTS mutes (
            steamid BIGINT UNSIGNED NOT NULL, reason VARCHAR(255), timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP, 
            unmuted BOOLEAN NOT NULL DEFAULT FALSE, PRIMARY KEY (steamid)
        );");
        await connection.ExecuteAsync(@"
        CREATE TABLE IF NOT EXISTS users (
            id INT AUTO_INCREMENT PRIMARY KEY, username VARCHAR(50) NOT NULL UNIQUE, password VARCHAR(255) NOT NULL, 
            created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
        );");
    }

    public void OnMapStart(string mapName)
    {
        Task.Run(async () =>
        {
            await AdminCommands.GenerateAdminsJsonAsync();
            await BanCommands.LoadBansFromDatabaseAsync();
            await MuteCommands.LoadMutesFromDatabaseAsync();
        });
        Server.ExecuteCommand("exec banned_user.cfg");
        Server.ExecuteCommand("exec banned_ip.cfg");
    }

    [RequiresPermissions("@css/rcon")]
    public void ExecuteRcon(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2)
        {
            caller?.PrintToChat(T("rcon_usage"));
            Server.PrintToConsole(T("rcon_usage"));
            return;
        }
        var command = string.Join(" ", Enumerable.Range(1, info.ArgCount - 1).Select(i => info.GetArg(i)));
        Server.ExecuteCommand(command);
        caller?.PrintToChat(T("rcon_executed", command));
        Server.PrintToConsole($"[AdminControlPlugin] {T("log_rcon_executed", caller?.PlayerName ?? "Console", command)}");
    }

    private void EnsureSharedConfigFilesExist()
    {
        var configsDir = Path.Combine(ModuleDirectory, "../../configs/");
        Directory.CreateDirectory(configsDir);

        // Criação de arquivos de config (mantido igual, está correta)
        var groupsPath = Path.Combine(configsDir, "groups.json");
        if (!File.Exists(groupsPath)) File.WriteAllText(groupsPath, @"
{
    ""#css/admin"": {
        ""flags"": [
            ""@css/reservation"", ""@css/generic"", ""@css/kick"", ""@css/ban"",
            ""@css/unban"", ""@css/vip"", ""@css/slay"", ""@css/changemap"",
            ""@css/cvar"", ""@css/config"", ""@css/chat"", ""@css/vote"",
            ""@css/password"", ""@css/rcon"", ""@css/cheats"", ""@css/root""
        ],
        ""immunity"": 99
    },
    ""#css/custom-permission"": {
        ""flags"": [
            ""@css/reservation"", ""@css/vip"", ""@css/generic"", ""@css/chat"",
            ""@css/vote"", ""@css/custom-permission""
        ],
        ""immunity"": 40
    }
}");
        var overridesPath = Path.Combine(configsDir, "admin_overrides.json");
        if (!File.Exists(overridesPath)) File.WriteAllText(overridesPath, @"
{
    ""vip_store_given_by"": {
        ""flags"": [ ""@css/custom-permission"" ],
        ""check_type"": ""all"",
        ""enabled"": true
    }
}");
        var corePath = Path.Combine(configsDir, "core.json");
        if (!File.Exists(corePath)) File.WriteAllText(corePath, @"
{
    ""PublicChatTrigger"": [ ""!"" ],
    ""SilentChatTrigger"": [ ""/"" ],
    ""FollowCS2ServerGuidelines"": false,
    ""PluginHotReloadEnabled"": true,
    ""PluginAutoLoadEnabled"": true,
    ""ServerLanguage"": ""en"",
    ""UnlockConCommands"": true,
    ""UnlockConVars"": true
}");
        var bannedUserPath = Path.Combine(Server.GameDirectory, "csgo/cfg/banned_user.cfg");
        if (!File.Exists(bannedUserPath)) File.WriteAllText(bannedUserPath, "");
        var bannedIpPath = Path.Combine(Server.GameDirectory, "csgo/cfg/banned_ip.cfg");
        if (!File.Exists(bannedIpPath)) File.WriteAllText(bannedIpPath, "");
    }

    internal void StartMapVote(CCSPlayerController p)
    {
        throw new NotImplementedException();
    }
}