using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Dapper;
using MySqlConnector;
using System.Text.Json;
using System.Text.Json.Serialization;
using Timer = System.Timers.Timer;

namespace AdminControlPlugin;

[MinimumApiVersion(130)]
public class AdminControlPlugin : BasePlugin, IPluginConfig<AdminControlPlugin.AdminControlConfig>
{
    public override string ModuleName => "Admin Control with MySQL & CFG Sync";
    public override string ModuleVersion => "11.0.0";
    public override string ModuleAuthor => "Amauri Bueno dos Santos & Gemini";
    public override string ModuleDescription => "Plugin completo para banimentos, admins e RCON com MySQL e sincronização com arquivos de configuração nativos do servidor.";

    private string _connectionString = string.Empty;
    public AdminControlConfig Config { get; set; } = new AdminControlConfig();
    private Timer? _adminCheckTimer;

    public void OnConfigParsed(AdminControlConfig config)
    {
        Config = config;
        _connectionString = $"server={Config.Host};uid={Config.User};pwd={Config.Password};database={Config.Database}";
    }

    public class AdminControlConfig : BasePluginConfig
    {
        [JsonPropertyName("MySQLHost")]
        public string Host { get; set; } = "localhost";

        [JsonPropertyName("MySQLUser")]
        public string User { get; set; } = "root";

        [JsonPropertyName("MySQLPassword")]
        public string Password { get; set; } = "0073007";

        [JsonPropertyName("MySQLDatabase")]
        public string Database { get; set; } = "mariusbd";

        [JsonPropertyName("RequiredFlags")]
        public List<string> RequiredFlags { get; set; } = new List<string> { "@css/root", "@css/ban" };
    }

    public class AdminEntry
    {
        [JsonPropertyName("identity")]
        public string? Identity { get; set; }
        [JsonPropertyName("immunity")]
        public int Immunity { get; set; }
        [JsonPropertyName("flags")]
        public List<string> Flags { get; set; } = new List<string>();
        [JsonPropertyName("groups")]
        public List<string> Groups { get; set; } = new List<string>();
    }

    public class AdminFile
    {
        public Dictionary<string, AdminEntry> Admins { get; set; } = new Dictionary<string, AdminEntry>();
    }

    public class DbAdmin
    {
        public ulong steamid { get; set; }
        public string? name { get; set; }
        public string? permission { get; set; }
        public int level { get; set; }
    }

    public class BanEntry
    {
        public ulong steamid { get; set; }
        public string? reason { get; set; }
        public DateTime timestamp { get; set; }
    }

    public class IpBanEntry
    {
        public string? ip_address { get; set; }
        public string? reason { get; set; }
        public DateTime timestamp { get; set; }
    }

    public override void Load(bool hotReload)
    {
        try
        {
            EnsureSharedConfigFilesExist();
            Task.Run(async () =>
            {
                await EnsureDatabaseAndTablesExistAsync();
                await GenerateAdminsJsonAsync();
            }).GetAwaiter().GetResult();

            // Comandos de SteamID
            AddCommand("css_ban", "Ban a player by SteamID64", BanPlayer);
            AddCommand("css_unban", "Unban a player by SteamID64", UnbanPlayer);

            // Comandos de IP
            AddCommand("css_ipban", "Ban a player by IP Address", IpBanPlayer);
            AddCommand("css_unbanip", "Unban a player by IP Address", UnbanIpPlayer);

            // Outros Comandos
            AddCommand("css_listbans", "List all banned players", ListBans);
            AddCommand("css_rcon", "Execute RCON command", ExecuteRcon);
            AddCommand("css_admin", "Grant basic admin", GrantBasicAdmin);
            AddCommand("css_removeadmin", "Remove admin", RemoveAdmin);
            AddCommand("css_addadmin", "Grant custom admin with permission and duration", GrantCustomAdmin);
            AddCommand("css_reloadadmins", "Reloads admins from the database", ReloadAdminsCommand);

            // Comandos de Chat
            AddCommand("!ban", "Ban a player by name", ChatBanPlayer);
            AddCommand("!unban", "Unban a player by name", ChatUnbanPlayer);
            AddCommand("!ipban", "Ban a player by name (IP Ban)", ChatIpBanPlayer);
            AddCommand("!unbanip", "Unban a player by name (IP Unban)", ChatUnbanIpPlayer);
            AddCommand("!admin", "Grant basic admin to a player by name", ChatGrantBasicAdmin);
            AddCommand("!removeadm", "Remove admin from a player by name", ChatRemoveAdmin);
            AddCommand("!status", "Check ban status", ChatCheckStatus);

            // Listeners
            RegisterListener<Listeners.OnClientConnect>((playerSlot, name, ipAddress) =>
            {
                // Obtenha o CCSPlayerController usando playerSlot
                var player = Utilities.GetPlayerFromSlot(playerSlot);
                OnClientConnect(playerSlot);
            });
            RegisterListener<Listeners.OnMapStart>(OnMapStart);

            StartAdminCheckTimer();

            Console.WriteLine("[AdminControlPlugin] Plugin carregado com sucesso!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminControlPlugin] ERRO ao carregar o plugin: {ex.Message}");
        }
    }

    public override void Unload(bool hotReload)
    {
        _adminCheckTimer?.Stop();
        _adminCheckTimer?.Dispose();
        Console.WriteLine("[AdminControlPlugin] Plugin descarregado.");
    }

    private void StartAdminCheckTimer()
    {
        _adminCheckTimer = new Timer(TimeSpan.FromMinutes(5).TotalMilliseconds);
        _adminCheckTimer.Elapsed += async (sender, e) => await RemoveExpiredAdminsAsync();
        _adminCheckTimer.AutoReset = true;
        _adminCheckTimer.Start();
    }

    private async Task<MySqlConnection> GetOpenConnectionAsync()
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
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS bans (
                steamid BIGINT UNSIGNED NOT NULL,
                reason VARCHAR(255),
                timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                unbanned BOOLEAN NOT NULL DEFAULT FALSE,
                PRIMARY KEY (steamid)
            );
            CREATE TABLE IF NOT EXISTS ip_bans (
                ip_address VARCHAR(16) NOT NULL,
                reason VARCHAR(255),
                timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                unbanned BOOLEAN NOT NULL DEFAULT FALSE,
                PRIMARY KEY (ip_address)
            );
            CREATE TABLE IF NOT EXISTS admins (
                steamid BIGINT UNSIGNED NOT NULL,
                name VARCHAR(64),
                permission VARCHAR(64),
                level INT NOT NULL,
                expires_at DATETIME,
                granted_by BIGINT UNSIGNED,
                timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (steamid)
            );
        ");
    }

    private async Task GenerateAdminsJsonAsync()
    {
        try
        {
            await RemoveExpiredAdminsAsync();
            using var connection = await GetOpenConnectionAsync();
            var admins = await connection.QueryAsync<DbAdmin>("SELECT steamid, name, permission, level FROM admins;");
            var adminFile = new AdminFile();

            foreach (var admin in admins)
            {
                adminFile.Admins[admin.name!] = new AdminEntry
                {
                    Identity = admin.steamid.ToString(),
                    Immunity = admin.level,
                    Flags = new List<string> { admin.permission! }
                };
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var jsonString = JsonSerializer.Serialize(adminFile.Admins, options);

            var path = Path.Combine(ModuleDirectory, "../../configs/admins.json");
            await File.WriteAllTextAsync(path, jsonString);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminControlPlugin] ERRO ao gerar admins.json: {ex.Message}");
        }
    }

    private async Task RemoveExpiredAdminsAsync()
    {
        try
        {
            using var connection = await GetOpenConnectionAsync();
            var expiredAdmins = await connection.ExecuteAsync("DELETE FROM admins WHERE expires_at IS NOT NULL AND expires_at < NOW();");
            if (expiredAdmins > 0)
            {
                Console.WriteLine($"[AdminControlPlugin] {expiredAdmins} admins expirados foram removidos.");
                await GenerateAdminsJsonAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminControlPlugin] ERRO ao remover admins expirados: {ex.Message}");
        }
    }

    public HookResult OnClientConnect(int playerSlot)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);

        if (player?.AuthorizedSteamID == null || !player.AuthorizedSteamID.IsValid())
        {
            return HookResult.Continue;
        }

        var steamId = player.AuthorizedSteamID.SteamId64;
        var ipAddress = player.IpAddress;

        // Sincroniza a verificação de banimentos
        var banReason = GetClientBanReasonAsync(steamId).GetAwaiter().GetResult();
        var ipBanReason = GetClientIpBanReasonAsync(ipAddress).GetAwaiter().GetResult();

        if (banReason != null)
        {
            Server.NextFrame(() =>
            {
                Server.ExecuteCommand($"kickid {player.UserId} \"Você foi banido do servidor! Motivo: {banReason}\"");
            });
            return HookResult.Stop;
        }
        else if (ipBanReason != null)
        {
            Server.NextFrame(() =>
            {
                Server.ExecuteCommand($"kickid {player.UserId} \"Seu IP foi banido do servidor! Motivo: {ipBanReason}\"");
            });
            return HookResult.Stop;
        }

        return HookResult.Continue;
    }

    public void OnMapStart(string mapName)
    {
        Task.Run(async () => await GenerateAdminsJsonAsync());
        Server.ExecuteCommand("exec banned_user.cfg");
        Server.ExecuteCommand("exec banned_ip.cfg");
    }

    private CCSPlayerController? FindPlayerByName(CCSPlayerController? caller, string name)
    {
        var players = Utilities.GetPlayers()
            .Where(p => p.IsValid && p.PlayerName.Contains(name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (players.Count == 1)
        {
            return players.First();
        }

        if (players.Count > 1)
        {
            caller?.PrintToChat("❌ Múltiplos jogadores encontrados. Por favor, seja mais específico:");
            foreach (var p in players)
            {
                caller?.PrintToChat($"  - {p.PlayerName} (SteamID: {p.AuthorizedSteamID?.SteamId64})");
            }
        }
        else
        {
            caller?.PrintToChat($"❌ Nenhum jogador com o nome '{name}' encontrado.");
        }

        return null;
    }

    private bool HasRequiredFlags(CCSPlayerController caller)
    {
        if (caller == null || !caller.IsValid) return false;
        foreach (var flag in Config.RequiredFlags)
        {
            if (AdminManager.PlayerHasPermissions(caller, flag))
            {
                return true;
            }
        }
        return false;
    }

    private async Task<string?> GetClientBanReasonAsync(ulong steamId)
    {
        try
        {
            using var connection = await GetOpenConnectionAsync();
            var ban = await connection.QueryFirstOrDefaultAsync<BanEntry>(
                "SELECT reason FROM bans WHERE steamid = @SteamId AND unbanned = FALSE;",
                new { SteamId = steamId });
            return ban?.reason;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminControlPlugin] ERRO ao verificar banimento: {ex.Message}");
            return null;
        }
    }

    private async Task<string?> GetClientIpBanReasonAsync(string? ipAddress)
    {
        if (string.IsNullOrEmpty(ipAddress)) return null;

        try
        {
            using var connection = await GetOpenConnectionAsync();
            var ban = await connection.QueryFirstOrDefaultAsync<IpBanEntry>(
                "SELECT reason FROM ip_bans WHERE ip_address = @IpAddress AND unbanned = FALSE;",
                new { IpAddress = ipAddress });
            return ban?.reason;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminControlPlugin] ERRO ao verificar banimento de IP: {ex.Message}");
            return null;
        }
    }

    private async Task ExecuteDbActionAsync(CCSPlayerController? caller, Func<Task> action, string successMessage, string errorMessage)
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

    // --- Lógica de Banimento por SteamID ---

    private void HandleBan(CCSPlayerController? caller, ulong steamId, string reason)
    {
        Task.Run(async () => await ExecuteDbActionAsync(caller, async () =>
        {
            using var connection = await GetOpenConnectionAsync();
            await connection.ExecuteAsync(@"
                INSERT INTO bans (steamid, reason) VALUES (@SteamId, @Reason)
                ON DUPLICATE KEY UPDATE reason = @Reason, unbanned = FALSE, timestamp = CURRENT_TIMESTAMP;",
                new { SteamId = steamId, Reason = reason });

            Server.NextFrame(() =>
            {
                Server.ExecuteCommand($"banid 0 {steamId}");
                Server.ExecuteCommand($"writeid");

                var playerToKick = Utilities.GetPlayers().FirstOrDefault(p => p.AuthorizedSteamID?.SteamId64 == steamId);
                if (playerToKick != null)
                {
                    Server.ExecuteCommand($"kickid {playerToKick.UserId} \"Voce foi banido do servidor! Motivo: {reason}\"");
                }
            });
        }, $"✅ Jogador {steamId} banido. Motivo: {reason}", "❌ Erro ao banir o jogador."));
    }

    private void HandleUnban(CCSPlayerController? caller, ulong steamId)
    {
        Task.Run(async () => await ExecuteDbActionAsync(caller, async () =>
        {
            using var connection = await GetOpenConnectionAsync();
            await connection.ExecuteAsync("UPDATE bans SET unbanned = TRUE WHERE steamid = @SteamId;",
                new { SteamId = steamId });

            Server.NextFrame(() =>
            {
                Server.ExecuteCommand($"removeid {steamId}");
                Server.ExecuteCommand($"writeid");
            });
        }, $"✅ Jogador {steamId} desbanido.", "❌ Erro ao desbanir o jogador."));
    }

    // --- Lógica de Banimento por IP ---

    private void HandleIpBan(CCSPlayerController? caller, string ipAddress, string reason)
    {
        Task.Run(async () => await ExecuteDbActionAsync(caller, async () =>
        {
            using var connection = await GetOpenConnectionAsync();
            await connection.ExecuteAsync(@"
                INSERT INTO ip_bans (ip_address, reason) VALUES (@IpAddress, @Reason)
                ON DUPLICATE KEY UPDATE reason = @Reason, unbanned = FALSE, timestamp = CURRENT_TIMESTAMP;",
                new { IpAddress = ipAddress, Reason = reason });

            Server.NextFrame(() =>
            {
                Server.ExecuteCommand($"banip 0 {ipAddress}");
                Server.ExecuteCommand($"writeip");

                var playerToKick = Utilities.GetPlayers().FirstOrDefault(p => p.IpAddress == ipAddress);
                if (playerToKick != null)
                {
                    Server.ExecuteCommand($"kickid {playerToKick.UserId} \"Seu IP foi banido do servidor! Motivo: {reason}\"");
                }
            });
        }, $"✅ IP {ipAddress} banido. Motivo: {reason}", "❌ Erro ao banir o IP."));
    }

    private void HandleUnbanIp(CCSPlayerController? caller, string ipAddress)
    {
        Task.Run(async () => await ExecuteDbActionAsync(caller, async () =>
        {
            using var connection = await GetOpenConnectionAsync();
            await connection.ExecuteAsync("UPDATE ip_bans SET unbanned = TRUE WHERE ip_address = @IpAddress;",
                new { IpAddress = ipAddress });

            Server.NextFrame(() =>
            {
                Server.ExecuteCommand($"removeip {ipAddress}");
                Server.ExecuteCommand($"writeip");
            });
        }, $"✅ IP {ipAddress} desbanido.", "❌ Erro ao desbanir o IP."));
    }


    // --- Comandos de Console ---

    [RequiresPermissions("@css/ban")]
    public void BanPlayer(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2 || !ulong.TryParse(info.GetArg(1), out var steamId))
        {
            caller?.PrintToChat("Uso: css_ban <steamid64> <motivo>");
            return;
        }
        var reason = string.Join(" ", Enumerable.Range(2, info.ArgCount - 2).Select(i => info.GetArg(i)));
        HandleBan(caller, steamId, reason);
    }

    [RequiresPermissions("@css/unban")]
    public void UnbanPlayer(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2 || !ulong.TryParse(info.GetArg(1), out var steamId))
        {
            caller?.PrintToChat("Uso: css_unban <steamid64>");
            return;
        }
        HandleUnban(caller, steamId);
    }

    [RequiresPermissions("@css/ban")]
    public void IpBanPlayer(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2)
        {
            caller?.PrintToChat("Uso: css_ipban <endereço de IP> <motivo>");
            return;
        }
        var ipAddress = info.GetArg(1);
        var reason = string.Join(" ", Enumerable.Range(2, info.ArgCount - 2).Select(i => info.GetArg(i)));
        HandleIpBan(caller, ipAddress, reason);
    }

    [RequiresPermissions("@css/unban")]
    public void UnbanIpPlayer(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2)
        {
            caller?.PrintToChat("Uso: css_unbanip <endereço de IP>");
            return;
        }
        var ipAddress = info.GetArg(1);
        HandleUnbanIp(caller, ipAddress);
    }

    [RequiresPermissions("@css/ban")]
    public async void ListBans(CCSPlayerController? caller, CommandInfo info)
    {
        try
        {
            using var connection = await GetOpenConnectionAsync();
            var steamBans = await connection.QueryAsync<BanEntry>("SELECT steamid, reason, timestamp FROM bans WHERE unbanned = FALSE;");
            var ipBans = await connection.QueryAsync<IpBanEntry>("SELECT ip_address, reason, timestamp FROM ip_bans WHERE unbanned = FALSE;");

            caller?.PrintToChat("--- Lista de Banimentos Ativos ---");
            caller?.PrintToChat("Banimentos por SteamID:");
            foreach (var ban in steamBans)
            {
                caller?.PrintToChat($"🚫 SteamID: {ban.steamid} | Motivo: {ban.reason} | Data: {ban.timestamp:dd/MM/yyyy}");
            }
            caller?.PrintToChat("Banimentos por IP:");
            foreach (var ban in ipBans)
            {
                caller?.PrintToChat($"🚫 IP: {ban.ip_address} | Motivo: {ban.reason} | Data: {ban.timestamp:dd/MM/yyyy}");
            }
            caller?.PrintToChat("----------------------------------");
        }
        catch (Exception ex)
        {
            caller?.PrintToChat($"❌ Erro ao listar banimentos: {ex.Message}");
        }
    }

    [RequiresPermissions("@css/rcon")]
    public void ExecuteRcon(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2)
        {
            caller?.PrintToChat("Uso: css_rcon <comando>");
            return;
        }
        var command = string.Join(" ", Enumerable.Range(1, info.ArgCount - 1).Select(i => info.GetArg(i)));
        Server.ExecuteCommand(command);
        caller?.PrintToChat($"📡 Comando RCON executado: {command}");
    }

    [RequiresPermissions("@css/admin")]
    public void GrantBasicAdmin(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2 || !ulong.TryParse(info.GetArg(1), out var steamId))
        {
            caller?.PrintToChat("Uso: css_admin <steamid64>");
            return;
        }
        var name = Utilities.GetPlayers().FirstOrDefault(p => p.AuthorizedSteamID?.SteamId64 == steamId)?.PlayerName ?? "Admin";
        HandleGrantAdmin(caller, steamId, name, "@css/basic", 1, null);
    }

    [RequiresPermissions("@css/admin")]
    public void RemoveAdmin(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2 || !ulong.TryParse(info.GetArg(1), out var steamId))
        {
            caller?.PrintToChat("Uso: css_removeadmin <steamid64>");
            return;
        }
        HandleRemoveAdmin(caller, steamId);
    }

    [RequiresPermissions("@css/root")]
    public void GrantCustomAdmin(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 6 ||
            !ulong.TryParse(info.GetArg(1), out var steamId) ||
            !int.TryParse(info.GetArg(4), out var level) ||
            !int.TryParse(info.GetArg(5), out var durationMinutes))
        {
            caller?.PrintToChat("Uso: css_addadmin <steamid64> <nome> <permissao> <nivel> <tempo_em_minutos>");
            return;
        }
        var name = info.GetArg(2);
        var permission = info.GetArg(3);
        var expiresAt = DateTime.UtcNow.AddMinutes(durationMinutes);
        HandleGrantAdmin(caller, steamId, name, permission, level, expiresAt);
    }

    // --- Comandos de Chat ---

    public void ChatBanPlayer(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null || !HasRequiredFlags(caller))
        {
            caller?.PrintToChat("❌ Você não tem permissão para usar este comando.");
            return;
        }
        if (info.ArgCount < 2)
        {
            caller?.PrintToChat("Uso: !ban <nome do jogador> [motivo]");
            return;
        }
        var playerName = info.GetArg(1);
        var reason = string.Join(" ", Enumerable.Range(2, info.ArgCount - 2).Select(i => info.GetArg(i)));
        if (string.IsNullOrEmpty(reason))
        {
            reason = "Sem motivo especificado.";
        }
        var playerToBan = FindPlayerByName(caller, playerName);
        if (playerToBan == null) return;
        HandleBan(caller, playerToBan.AuthorizedSteamID!.SteamId64, reason);
    }

    public void ChatUnbanPlayer(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null || !HasRequiredFlags(caller))
        {
            caller?.PrintToChat("❌ Você não tem permissão para usar este comando.");
            return;
        }
        if (info.ArgCount < 2)
        {
            caller?.PrintToChat("Uso: !unban <nome do jogador>");
            return;
        }
        var playerName = info.GetArg(1);
        var playerToUnban = FindPlayerByName(caller, playerName);
        if (playerToUnban == null) return;
        HandleUnban(caller, playerToUnban.AuthorizedSteamID!.SteamId64);
    }

    public void ChatIpBanPlayer(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null || !HasRequiredFlags(caller))
        {
            caller?.PrintToChat("❌ Você não tem permissão para usar este comando.");
            return;
        }
        if (info.ArgCount < 2)
        {
            caller?.PrintToChat("Uso: !ipban <nome do jogador> [motivo]");
            return;
        }
        var playerName = info.GetArg(1);
        var reason = string.Join(" ", Enumerable.Range(2, info.ArgCount - 2).Select(i => info.GetArg(i)));
        if (string.IsNullOrEmpty(reason))
        {
            reason = "Sem motivo especificado.";
        }
        var playerToBan = FindPlayerByName(caller, playerName);
        if (playerToBan == null) return;
        HandleIpBan(caller, playerToBan.IpAddress!, reason);
    }

    public void ChatUnbanIpPlayer(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null || !HasRequiredFlags(caller))
        {
            caller?.PrintToChat("❌ Você não tem permissão para usar este comando.");
            return;
        }
        if (info.ArgCount < 2)
        {
            caller?.PrintToChat("Uso: !unbanip <nome do jogador>");
            return;
        }
        var playerName = info.GetArg(1);
        var playerToUnban = FindPlayerByName(caller, playerName);
        if (playerToUnban == null) return;
        HandleUnbanIp(caller, playerToUnban.IpAddress!);
    }

    public void ChatGrantBasicAdmin(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null || !HasRequiredFlags(caller))
        {
            caller?.PrintToChat("❌ Você não tem permissão para usar este comando.");
            return;
        }
        if (info.ArgCount < 2)
        {
            caller?.PrintToChat("Uso: !admin <nome do jogador>");
            return;
        }
        var playerName = info.GetArg(1);
        var playerToGrant = FindPlayerByName(caller, playerName);
        if (playerToGrant == null) return;
        HandleGrantAdmin(caller, playerToGrant.AuthorizedSteamID!.SteamId64, playerName, "@css/basic", 1, null);
    }

    public void ChatRemoveAdmin(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null || !HasRequiredFlags(caller))
        {
            caller?.PrintToChat("❌ Você não tem permissão para usar este comando.");
            return;
        }
        if (info.ArgCount < 2)
        {
            caller?.PrintToChat("Uso: !removeadm <nome do jogador>");
            return;
        }
        var playerName = info.GetArg(1);
        var playerToRemove = FindPlayerByName(caller, playerName);
        if (playerToRemove == null) return;
        HandleRemoveAdmin(caller, playerToRemove.AuthorizedSteamID!.SteamId64);
    }

    [RequiresPermissions("@css/root")]
    public void ReloadAdminsCommand(CCSPlayerController? caller, CommandInfo info)
    {
        Task.Run(async () =>
        {
            await GenerateAdminsJsonAsync();
            caller?.PrintToChat("✅ Admins recarregados com sucesso!");
        });
    }

    public void ChatCheckStatus(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null || !caller.IsValid)
        {
            return;
        }

        Task.Run(async () =>
        {
            var steamId = caller.AuthorizedSteamID!.SteamId64;
            var banReason = await GetClientBanReasonAsync(steamId);
            var ipBanReason = await GetClientIpBanReasonAsync(caller.IpAddress!);

            Server.NextFrame(() =>
            {
                if (banReason != null)
                {
                    caller.PrintToChat($"🚫 Seu status: Você está banido. Motivo: {banReason}");
                }
                else if (ipBanReason != null)
                {
                    caller.PrintToChat($"🚫 Seu status: Seu IP está banido. Motivo: {ipBanReason}");
                }
                else
                {
                    caller.PrintToChat("✅ Seu status: Você não está banido.");
                }
            });
        });
    }

    private void EnsureSharedConfigFilesExist()
    {
        var configsDir = Path.Combine(ModuleDirectory, "../../configs/");
        Directory.CreateDirectory(configsDir);

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

    private void HandleGrantAdmin(CCSPlayerController? caller, ulong steamId, string name, string permission, int level, DateTime? expiresAt)
    {
        Task.Run(async () => await ExecuteDbActionAsync(caller, async () =>
        {
            using var connection = await GetOpenConnectionAsync();
            await connection.ExecuteAsync(@"
                INSERT INTO admins (steamid, name, permission, level, expires_at, granted_by)
                VALUES (@SteamId, @Name, @Permission, @Level, @ExpiresAt, @GrantedBy)
                ON DUPLICATE KEY UPDATE
                    name = @Name,
                    permission = @Permission,
                    level = @Level,
                    expires_at = @ExpiresAt,
                    granted_by = @GrantedBy,
                    timestamp = CURRENT_TIMESTAMP;",
            new
            {
                SteamId = steamId,
                Name = name,
                Permission = permission,
                Level = level,
                ExpiresAt = expiresAt,
                GrantedBy = caller?.AuthorizedSteamID?.SteamId64
            });

            await GenerateAdminsJsonAsync();
        }, $"✅ Admin {name} adicionado com permissão {permission}.", "❌ Erro ao adicionar admin."));
    }

    private void HandleRemoveAdmin(CCSPlayerController? caller, ulong steamId)
    {
        Task.Run(async () => await ExecuteDbActionAsync(caller, async () =>
        {
            using var connection = await GetOpenConnectionAsync();
            await connection.ExecuteAsync("DELETE FROM admins WHERE steamid = @SteamId;", new { SteamId = steamId });
            await GenerateAdminsJsonAsync();
        }, $"✅ Admin removido com sucesso.", "❌ Erro ao remover admin."));
    }
}