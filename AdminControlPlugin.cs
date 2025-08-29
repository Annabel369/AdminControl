using AdminControlPlugin.commands;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using CS2MenuManager.API;
using CS2MenuManager.API.Menu;
using Dapper;
using MySqlConnector;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;

namespace AdminControlPlugin;

[MinimumApiVersion(130)]
public class AdminControlPlugin : BasePlugin, IPluginConfig<AdminControlPlugin.AdminControlConfig>
{
    public override string ModuleName => "Admin Control with MySQL & CFG Sync";
    public override string ModuleVersion => "12.0.0"; // Versão Super Legal
    public override string ModuleAuthor => "Amauri Bueno dos Santos & Gemini";
    public override string ModuleDescription => "Plugin completo para banimentos, admins e RCON com MySQL e sincronização com arquivos de configuração nativos do servidor.";

    private string _connectionString = string.Empty;
    public AdminControlConfig Config { get; set; } = new AdminControlConfig();
    private Timer? _adminCheckTimer;

    // Cache de banidos na memória para verificações rápidas
    private HashSet<ulong> _bannedPlayers = new HashSet<ulong>();
    private HashSet<string> _bannedIps = new HashSet<string>();
    private Dictionary<ulong, string> _banReasons = new Dictionary<ulong, string>();
    private Dictionary<string, string> _ipBanReasons = new Dictionary<string, string>();

    // Instância da nova classe de comandos de jogador
    private PlayerCommand? _playerCommand;

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
        public string ip_address { get; set; } = string.Empty;
        public string reason { get; set; } = string.Empty;
        public DateTime timestamp { get; set; }
    }

    public class MuteEntry
    {
        public ulong steamid { get; set; }
        public string? reason { get; set; }
        public DateTime timestamp { get; set; }
        public bool unmuted { get; set; }
    }

    public override void Load(bool hotReload)
    {
        try
        {
            // Inicializa a classe de comandos
            _playerCommand = new PlayerCommand(this);

            // Eventos e listeners
            RegisterListener<Listeners.OnMapStart>(OnMapStart);
            RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFullCheckBan);

            // Inicialização assíncrona segura
            Task.Run(async () =>
            {
                await EnsureDatabaseAndTablesExistAsync();
                await GenerateAdminsJsonAsync();
                await LoadBansFromDatabaseAsync();
            }).GetAwaiter().GetResult();

            // Comandos de Console (mantidos aqui)
            AddCommand("css_ban", "Ban a player by SteamID64", BanPlayer);
            AddCommand("css_unban", "Unban a player by SteamID64", UnbanPlayer);
            AddCommand("css_ipban", "Ban a player by IP Address", IpBanPlayer);
            AddCommand("css_unbanip", "Unban a player by IP Address", UnbanIpPlayer);
            AddCommand("css_listbans", "List all banned players", ListBans);
            AddCommand("css_rcon", "Execute RCON command", ExecuteRcon);
            AddCommand("css_addadmin", "Grant custom admin with permission and duration", GrantCustomAdmin);
            AddCommand("css_reloadadmins", "Reloads admins from the database", ReloadAdminsCommand);
            AddCommand("css_unmute", "Desmuta um jogador pelo SteamID", UnmutePlayerCommand);
            AddCommand("css_mute", "Muta um jogador por nome ou SteamID", MutePlayerCommand);

            // Novos comandos para abrir o menu
            AddCommand("css_menu", "Abre o menu de admins e banidos", OpenMenuCommand);
            AddCommand("!adminmenu", "Abre o menu de admins e banidos", OpenMenuChatCommand);
            AddCommand("/adminmenu", "Abre o menu", OpenMenuChatCommand);

            AddCommand("!kick", "Kicka um jogador", KickCommand);
            AddCommand("!mute", "Muta um jogador", MuteCommand);
            AddCommand("!swapteam", "Move jogador para o outro time", SwapTeamCommand);

            // Timer de verificação de admins
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

    public HookResult OnPlayerConnectFullCheckBan(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;

        if (player == null || !player.IsValid || player.IsBot)
        {
            return HookResult.Continue;
        }

        ulong steamId = player.AuthorizedSteamID!.SteamId64;
        string ip = player.IpAddress ?? "desconhecido";

        if (_bannedPlayers.Contains(steamId))
        {
            string reason = _banReasons.TryGetValue(steamId, out var r) ? r : "Banido";
            Server.ExecuteCommand($"kickid {player.UserId} \"You have been banned from this server! Reason: {reason}\"");
            return HookResult.Stop;
        }

        else if (_bannedIps.Contains(ip))
        {
            string reason = _ipBanReasons.TryGetValue(ip, out var r) ? r : "Banido por IP";
            Server.ExecuteCommand($"kickid {player.UserId} \"Your IP has been banned from this server! Reason: {reason}\"");
            return HookResult.Stop;
        }

        return HookResult.Continue;
    }

    private void StartAdminCheckTimer()
    {
        _adminCheckTimer = new Timer(TimeSpan.FromMinutes(5).TotalMilliseconds);
        _adminCheckTimer.Elapsed += async (sender, e) => await RemoveExpiredAdminsAsync();
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
        await connection.ExecuteAsync(@"
        CREATE TABLE IF NOT EXISTS bans (
            id INT UNSIGNED NOT NULL AUTO_INCREMENT,
            steamid BIGINT UNSIGNED NOT NULL,
            reason VARCHAR(255),
            timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
            unbanned BOOLEAN NOT NULL DEFAULT FALSE,
            PRIMARY KEY (id),
            INDEX idx_steamid (steamid)
        );
        CREATE TABLE IF NOT EXISTS ip_bans (
            id INT UNSIGNED NOT NULL AUTO_INCREMENT,
            ip_address VARCHAR(45) NOT NULL,
            reason VARCHAR(255),
            timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
            unbanned BOOLEAN NOT NULL DEFAULT FALSE,
            PRIMARY KEY (id),
            INDEX idx_ip (ip_address)
        );
        CREATE TABLE IF NOT EXISTS admins (
            id INT UNSIGNED NOT NULL AUTO_INCREMENT,
            steamid BIGINT UNSIGNED NOT NULL,
            name VARCHAR(64),
            permission VARCHAR(64),
            level INT NOT NULL,
            expires_at DATETIME,
            granted_by BIGINT UNSIGNED,
            timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
            PRIMARY KEY (id),
            INDEX idx_admin_steamid (steamid)
        );
        CREATE TABLE IF NOT EXISTS mutes (
            id INT UNSIGNED NOT NULL AUTO_INCREMENT,
            steamid BIGINT UNSIGNED NOT NULL,
            reason VARCHAR(255),
            timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
            unmuted BOOLEAN NOT NULL DEFAULT FALSE,
            PRIMARY KEY (id),
            INDEX idx_mute_steamid (steamid)
        );
        ");
    }

    public async Task GenerateAdminsJsonAsync()
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

    private async Task LoadBansFromDatabaseAsync()
    {
        try
        {
            using var connection = await GetOpenConnectionAsync();
            var steamBans = await connection.QueryAsync<BanEntry>("SELECT steamid, reason FROM bans WHERE unbanned = FALSE;");
            var ipBans = await connection.QueryAsync<IpBanEntry>("SELECT ip_address, reason FROM ip_bans WHERE unbanned = FALSE;");

            _bannedPlayers.Clear();
            _bannedIps.Clear();
            _banReasons.Clear();
            _ipBanReasons.Clear();

            foreach (var ban in steamBans)
            {
                _bannedPlayers.Add(ban.steamid);
                _banReasons[ban.steamid] = ban.reason ?? "Sem motivo";
            }

            foreach (var ban in ipBans)
            {
                if (ban.ip_address != null)
                {
                    _bannedIps.Add(ban.ip_address);
                    _ipBanReasons[ban.ip_address] = ban.reason ?? "Sem motivo";
                }
            }
            Console.WriteLine($"[AdminControlPlugin] Cache de banimentos carregado com sucesso. {steamBans.Count()} banimentos de SteamID e {ipBans.Count()} de IP.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminControlPlugin] ERRO ao carregar banimentos do banco de dados: {ex.Message}");
        }
    }

    public void OnMapStart(string mapName)
    {
        Task.Run(async () =>
        {
            await GenerateAdminsJsonAsync();
            await LoadBansFromDatabaseAsync();
        });
        Server.ExecuteCommand("exec banned_user.cfg");
        Server.ExecuteCommand("exec banned_ip.cfg");
    }

    public void HandleMute(CCSPlayerController? caller, ulong steamId, string reason)
    {
        Task.Run(async () => await ExecuteDbActionAsync(caller, async () =>
        {
            using var connection = await GetOpenConnectionAsync();

            // Insere novo registro de mute (histórico)
            await connection.ExecuteAsync(@"
            INSERT INTO mutes (steamid, reason)
            VALUES (@SteamId, @Reason);",
                new { SteamId = steamId, Reason = reason });

        },
        $"✅ Jogador {steamId} mutado. Motivo: {reason}",
        "❌ Erro ao mutar o jogador."));
    }

    public void HandleUnmute(CCSPlayerController? caller, ulong steamId)
    {
        Task.Run(async () => await ExecuteDbActionAsync(caller, async () =>
        {
            using var connection = await GetOpenConnectionAsync();

            // Marca todos os mutes ativos desse SteamID como desmutados
            await connection.ExecuteAsync(@"
            UPDATE mutes 
            SET unmuted = TRUE 
            WHERE steamid = @SteamId AND unmuted = FALSE;",
                new { SteamId = steamId });

            caller?.PrintToChat($"✅ Jogador {steamId} desmutado.");
            Console.WriteLine($"[AdminControlPlugin] Admin {caller?.PlayerName} desmutou o jogador {steamId}.");
        },
        $"✅ Jogador {steamId} desmutado.",
        "❌ Erro ao desmutar o jogador."));
    }


    public void KickCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null || info.ArgCount < 1) return;

        var targetName = info.GetArg(0);
        var target = Utilities.GetPlayers()
            .FirstOrDefault(p => p.IsValid && p.PlayerName.Contains(targetName, StringComparison.OrdinalIgnoreCase));

        if (target != null)
        {
            Server.ExecuteCommand($"kick \"{target.PlayerName}\"");
            caller.PrintToChat($"✅ {target.PlayerName} foi kickado.");
        }
        else
        {
            caller.PrintToChat("❌ Jogador não encontrado.");
        }
    }

    public void MuteCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null || info.ArgCount < 1) return;

        var targetName = info.GetArg(0);
        var target = Utilities.GetPlayers()
            .FirstOrDefault(p => p.IsValid && p.PlayerName.Contains(targetName, StringComparison.OrdinalIgnoreCase));

        if (target != null)
        {
            var steamId = target.AuthorizedSteamID!.SteamId64;
            var reason = info.ArgCount > 1
                ? string.Join(" ", Enumerable.Range(1, info.ArgCount - 1).Select(i => info.GetArg(i)))
                : "Mute from command";

            // Muta o jogador no jogo
            target.VoiceFlags = 0;
            caller.PrintToChat($"🔇 {target.PlayerName} foi mutado.");

            // Registra no banco de dados (novo registro sempre)
            Task.Run(async () => await ExecuteDbActionAsync(caller, async () =>
            {
                using var connection = await GetOpenConnectionAsync();
                await connection.ExecuteAsync(@"
                INSERT INTO mutes (steamid, reason)
                VALUES (@SteamId, @Reason);",
                    new { SteamId = steamId, Reason = reason });

            }, $"✅ Jogador {target.PlayerName} mutado. Motivo: {reason}",
               "❌ Erro ao mutar o jogador."));
        }
        else
        {
            caller?.PrintToChat("❌ Jogador não encontrado.");
        }
    }


    

    public void SwapTeamCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null || info.ArgCount < 1) return;
        var targetName = info.GetArg(0);
        var target = Utilities.GetPlayers().FirstOrDefault(p => p.PlayerName.Contains(targetName, StringComparison.OrdinalIgnoreCase));
        if (target != null)
        {
            var newTeam = target.Team == CsTeam.Terrorist ? CsTeam.CounterTerrorist : CsTeam.Terrorist;
            target.SwitchTeam(newTeam);
            caller.PrintToChat($"🔄 {target.PlayerName} foi movido para o time {newTeam}.");
        }
    }

    public CCSPlayerController? FindPlayerByName(CCSPlayerController? caller, string name)
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

    public bool HasRequiredFlags(CCSPlayerController caller)
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

    public async Task<string?> GetClientBanReasonAsync(ulong steamId)
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


    public async Task<string?> GetClientIpBanReasonAsyncIP(string? ipAddress)
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

    public void HandleBan(CCSPlayerController? caller, ulong steamId, string reason)
    {
        Task.Run(async () => await ExecuteDbActionAsync(caller, async () =>
        {
            using var connection = await GetOpenConnectionAsync();
            await connection.ExecuteAsync(@"
                INSERT INTO bans (steamid, reason) VALUES (@SteamId, @Reason)
                ON DUPLICATE KEY UPDATE reason = @Reason, unbanned = FALSE, timestamp = CURRENT_TIMESTAMP;",
                new { SteamId = steamId, Reason = reason });

            _bannedPlayers.Add(steamId);
            _banReasons[steamId] = reason;

            Server.NextFrame(() =>
            {
                Server.ExecuteCommand($"banid 0 {steamId}");
                Server.ExecuteCommand($"writeid");

                var playerToKick = Utilities.GetPlayers().FirstOrDefault(p => p.AuthorizedSteamID?.SteamId64 == steamId);
                if (playerToKick != null)
                {
                    Server.ExecuteCommand($"kickid {playerToKick.UserId} \"You've been banned from the server! Reason: {reason}\"");
                }
            });
        }, $"✅ Jogador {steamId} banido. Motivo: {reason}", "❌ Erro ao banir o jogador."));
    }

    public void HandleUnban(CCSPlayerController? caller, ulong steamId)
    {
        Task.Run(async () => await ExecuteDbActionAsync(caller, async () =>
        {
            using var connection = await GetOpenConnectionAsync();
            await connection.ExecuteAsync("UPDATE bans SET unbanned = TRUE WHERE steamid = @SteamId;",
                new { SteamId = steamId });

            _bannedPlayers.Remove(steamId);
            _banReasons.Remove(steamId);

            Server.NextFrame(() =>
            {
                Server.ExecuteCommand($"removeid {steamId}");
                Server.ExecuteCommand($"writeid");
            });
        }, $"✅ Jogador {steamId} desbanido.", "❌ Erro ao desbanir o jogador."));
    }

    public void HandleIpBan(CCSPlayerController? caller, string ipAddress, string reason)
    {
        Task.Run(async () => await ExecuteDbActionAsync(caller, async () =>
        {
            using var connection = await GetOpenConnectionAsync();

            // Insere novo registro de ban por IP
            await connection.ExecuteAsync(@"
            INSERT INTO ip_bans (ip_address, reason) 
            VALUES (@IpAddress, @Reason);",
                new { IpAddress = ipAddress, Reason = reason });

            _bannedIps.Add(ipAddress);
            _ipBanReasons[ipAddress] = reason;

            Server.NextFrame(() =>
            {
                Server.ExecuteCommand($"banip 0 {ipAddress}");
                Server.ExecuteCommand($"writeip");

                var playerToKick = Utilities.GetPlayers()
                    .FirstOrDefault(p => p.IpAddress == ipAddress);

                if (playerToKick != null)
                {
                    Server.ExecuteCommand($"kickid {playerToKick.UserId} \"You've been banned from the server! Reason: {reason}\"");
                }
            });
        },
        $"✅ IP {ipAddress} banido. Motivo: {reason}",
        "❌ Erro ao banir o IP."));
    }

    public void HandleUnbanIp(CCSPlayerController? caller, string ipAddress)
    {
        Task.Run(async () => await ExecuteDbActionAsync(caller, async () =>
        {
            using var connection = await GetOpenConnectionAsync();

            // Marca todos os bans ativos desse IP como desbanidos
            await connection.ExecuteAsync(@"
            UPDATE ip_bans 
            SET unbanned = TRUE 
            WHERE ip_address = @IpAddress AND unbanned = FALSE;",
                new { IpAddress = ipAddress });

            if (_bannedIps.Remove(ipAddress))
            {
                _ipBanReasons.Remove(ipAddress);
            }

            Server.NextFrame(() =>
            {
                Server.ExecuteCommand($"removeip {ipAddress}");
                Server.ExecuteCommand($"writeip");
            });

            caller?.PrintToChat($"✅ IP {ipAddress} desbanido.");
            Console.WriteLine($"[AdminControlPlugin] Admin {caller?.PlayerName} desbaniu o IP {ipAddress}.");
        },
        $"✅ IP {ipAddress} desbanido.",
        "❌ Erro ao desbanir o IP."));
    }


    public void HandleGrantAdmin(CCSPlayerController? caller, ulong steamId, string name, string permission, int level, DateTime? expiresAt)
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
    

    // Adicione esta função para encontrar o jogador pelo nome ou SteamID
    private CCSPlayerController? FindPlayerByNameOrSteamId(CCSPlayerController? caller, string identifier)
    {
        // Tenta encontrar pelo SteamID
        if (ulong.TryParse(identifier, out var steamId))
        {
            var player = Utilities.GetPlayers().FirstOrDefault(p => p.AuthorizedSteamID?.SteamId64 == steamId);
            if (player != null)
            {
                return player;
            }
        }

        // Tenta encontrar pelo nome (parcial)
        var players = Utilities.GetPlayers()
            .Where(p => p.IsValid && p.PlayerName.Contains(identifier, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (players.Count == 1)
        {
            return players.First();
        }

        // Múltiplos jogadores encontrados ou nenhum
        if (players.Count > 1)
        {
            caller?.PrintToChat("❌ Múltiplos jogadores encontrados. Por favor, seja mais específico.");
        }
        else
        {
            caller?.PrintToChat($"❌ Nenhum jogador com o nome ou SteamID '{identifier}' encontrado.");
        }

        return null;
    }

    public void HandleRemoveAdmin(CCSPlayerController? caller, ulong steamId)
    {
        Task.Run(async () => await ExecuteDbActionAsync(caller, async () =>
        {
            using var connection = await GetOpenConnectionAsync();
            await connection.ExecuteAsync("DELETE FROM admins WHERE steamid = @SteamId;", new { SteamId = steamId });
            await GenerateAdminsJsonAsync();
        }, $"✅ Admin removido com sucesso.", "❌ Erro ao remover admin."));
    }

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

    [RequiresPermissions("@css/chat")] // A permissão pode ser ajustada conforme sua necessidade
    // Adicione este comando para mutar jogadores via console ou RCON
    [RequiresPermissions("@css/chat")]
    public void MutePlayerCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2)
        {
            caller?.PrintToChat("Uso: css_mute <nome_ou_steamid> [motivo]");
            return;
        }

        var targetIdentifier = info.GetArg(1);
        var target = FindPlayerByNameOrSteamId(caller, targetIdentifier);

        if (target != null)
        {
            var reason = info.ArgCount > 2 ? string.Join(" ", Enumerable.Range(2, info.ArgCount - 2).Select(i => info.GetArg(i))) : "Mute from console";

            // Muta o jogador no jogo e registra no banco de dados
            target.VoiceFlags = 0;
            caller?.PrintToChat($"🔇 Jogador {target.PlayerName} foi mutado.");
            HandleMute(caller, target.AuthorizedSteamID!.SteamId64, reason);
        }
    }

    [RequiresPermissions("@css/chat")] // Ou a permissão que você preferir
    public void UnmutePlayerCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2 || !ulong.TryParse(info.GetArg(1), out var steamId))
        {
            caller?.PrintToChat("Uso: css_unmute <steamid64>");
            return;
        }
        HandleUnmute(caller, steamId);
    }

    [RequiresPermissions("@css/ban")]
    public void IpBanPlayer(CCSPlayerController? caller, CommandInfo info)
    {
        // Verifica se há pelo menos um argumento (o endereço de IP)
        if (info.ArgCount < 2)
        {
            caller?.PrintToChat("Uso: css_ipban <endereço de IP> [motivo]");
            return;
        }

        var ipAddress = info.GetArg(1);

        // Pega o motivo do banimento, se ele existir.
        // Se não houver, a string "reason" ficará vazia.
        var reason = info.ArgCount > 2 ?
            string.Join(" ", Enumerable.Range(2, info.ArgCount - 2).Select(i => info.GetArg(i))) :
            string.Empty;

        // A chamada correta para a sua função HandleIpBan
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

    private void ShowBanConfirmation(CCSPlayerController admin, CCSPlayerController target, string reason)
    {
        var confirmMenu = new ChatMenu($"⚠️ Confirmar banimento de {target.PlayerName}?", this)
        {
            ExitButton = true,
            MenuTime = 20
        };

        confirmMenu.AddItem("✅ Confirmar", (p, o) => HandleBan(p, target.AuthorizedSteamID!.SteamId64, reason));
        confirmMenu.AddItem("❌ Cancelar", (p, o) => ShowAdminAndBanMenu(p)); // Corrigido aqui

        confirmMenu.Display(admin, 20);
    }

    // Adicione este método para evitar o erro CS0103
    public void ShowAdminAndBanMenu(CCSPlayerController player)
    {
        _playerCommand?.ShowAdminAndBanMenu(player);
    }

    public void StartMapVote(CCSPlayerController caller)
    {
        var maps = new List<string> { "de_dust2", "de_inferno", "de_mirage" };

        var vote = new PanoramaVote(
            "🗳 Votação de Mapa",
            "Escolha o próximo mapa",
            result =>
            {
                string winner = maps[0]; // Aqui você pode usar lógica real com base nos votos
                Server.ExecuteCommand($"changelevel {winner}");
                caller.PrintToChat($"✅ Mapa escolhido: {winner}");
                return true;
            },
            (action, slot, voteOption) =>
            {
                Console.WriteLine($"[MapVote] Slot {slot} votou opção {voteOption}");
            },
            this
        );

        vote.DisplayVoteToAll(20); // Tempo da votação em segundos
    }

    [RequiresPermissions("@css/root")]
    public void ReloadAdminsCommand(CCSPlayerController? caller, CommandInfo info)
    {
        Task.Run(async () =>
        {
            await GenerateAdminsJsonAsync();
            await LoadBansFromDatabaseAsync();
            caller?.PrintToChat("✅ Admins e banimentos recarregados com sucesso!");
        });
    }

    // --- Novos métodos para comandos de menu ---

    [RequiresPermissions("@css/admin")]
    public void OpenMenuCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null || !caller.IsValid)
        {
            info.ReplyToCommand("Este comando só pode ser usado por um jogador.");
            return;
        }
        _playerCommand?.ShowAdminAndBanMenu(caller);
    }

    public void OpenMenuChatCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null || !caller.IsValid)
        {
            return;
        }
        _playerCommand?.ShowAdminAndBanMenu(caller);
        
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
}