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
using System.Text.Json;
using System.Text.Json.Serialization;
using Timer = System.Timers.Timer;

namespace AdminControlPlugin;

[MinimumApiVersion(130)]
public class AdminControlPlugin : BasePlugin, IPluginConfig<AdminControlConfig>
{
    public override string ModuleName => "Admin Control with MySQL & CFG Sync";
    public override string ModuleVersion => "15.3.2";
    public override string ModuleAuthor => "Amauri Bueno dos Santos & Gemini";
    public override string ModuleDescription => "Plugin completo para banimentos, admins e RCON com MySQL e sincronização com arquivos de configuração nativos do servidor.";

    private string _connectionString = string.Empty;
    public AdminControlConfig Config { get; set; } = new AdminControlConfig();
    private Timer? _adminCheckTimer;

    private HashSet<ulong> _bannedPlayers = new HashSet<ulong>();
    private HashSet<string> _bannedIps = new HashSet<string>();
    private Dictionary<ulong, string> _banReasons = new Dictionary<ulong, string>();

    public Ban BanCommands { get; private set; }
    public Admin AdminCommands { get; private set; }
    public Mute MuteCommands { get; private set; }
    private Player? _playerCommand;

    public string T(string key, params object[] args)
    {
        // Supondo que Localizer implementa IStringLocalizer
        // e que o método correto é this[string name, params object[] arguments]
        return Localizer[key, args];
    }

    public void OnConfigParsed(AdminControlConfig config)
    {
        Config = config;
        _connectionString = $"server={Config.Host};uid={Config.User};pwd={Config.Password};database={Config.Database}";
    }

    public override void Load(bool hotReload)
    {
        try
        {
            BanCommands = new Ban(this);
            AdminCommands = new Admin(this);
            MuteCommands = new Mute(this);
            _playerCommand = new Player(this);

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
        AddCommand("css_ipban", "Ban a player by IP Address", BanCommands.IpBanPlayerCommand);
        AddCommand("css_unbanip", "Unban a player by IP Address", BanCommands.UnbanIpPlayerCommand);
        AddCommand("css_listbans", "List all banned players", BanCommands.ListBans);
        AddCommand("css_rcon", "Execute RCON command", ExecuteRcon);
        AddCommand("css_addadmin", "Grant custom admin with permission and duration", AdminCommands.GrantCustomAdmin);
        AddCommand("css_removeadmin", "Remove a custom admin by SteamID64", RemoveAdminCommand);
        AddCommand("css_reloadadmins", "Reloads admins from the database", AdminCommands.ReloadAdminsCommand);
        AddCommand("css_unmute", "Desmuta um jogador pelo SteamID", MuteCommands.UnmutePlayerCommand);
        AddCommand("css_mute", "Muta um jogador por nome ou SteamID", MuteCommands.MutePlayerCommand);

        AddCommand("css_menu", "Abre o menu de admins e banidos", OpenMenuCommand);
        AddCommand("!adminmenu", "Abre o menu de admins e banidos", OpenMenuChatCommand);
        AddCommand("/adminmenu", "Abre o menu", OpenMenuChatCommand);
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

    public HookResult OnPlayerConnectFullCheckBan(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;

        if (player == null || !player.IsValid || player.IsBot)
        {
            return HookResult.Continue;
        }

        ulong steamId = player.AuthorizedSteamID?.SteamId64 ?? 0;

        // Aplica a máscara para remover a porta, se houver
        string? rawIp = string.IsNullOrWhiteSpace(player.IpAddress) ? null : player.IpAddress;
        string? ip = rawIp?.Split(':')[0];

        if (BanCommands.IsBanned(steamId))
        {
            string reason = BanCommands.GetBanReason(steamId) ?? T("no_reason");
            Server.ExecuteCommand($"kickid {player.UserId} \"{T("kick_ban_message", reason)}\"");
            Server.PrintToConsole($"[AdminControlPlugin] {T("log_player_banned", player.UserId, steamId, reason)}");
            return HookResult.Stop;
        }

        if (ip != null && BanCommands.IsIpBanned(ip))
        {
            string reason = BanCommands.GetIpBanReason(ip) ?? T("no_reason");
            Server.ExecuteCommand($"kickip {ip} \"{T("kick_ip_ban_message", reason)}\"");
            Server.PrintToConsole($"[AdminControlPlugin] {T("log_ip_banned", ip, reason)}");
            return HookResult.Stop;
        }

        if (MuteCommands.IsMuted(steamId))
        {
            player.VoiceFlags = (VoiceFlags)0;
            player.PrintToChat(T("you_are_muted"));
            Server.PrintToConsole($"[AdminControlPlugin] {T("log_player_muted", player.UserId)}");
        }

        Server.PrintToConsole($"[AdminControlPlugin] {T("log_player_connected", player.UserId, steamId, ip ?? "n/a")}");
        return HookResult.Continue;
    }

    [RequiresPermissions("@css/root")]
    public void RemoveAdminCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2 || !ulong.TryParse(info.GetArg(1), out var steamId))
        {
            caller?.PrintToChat("Uso: css_removeadmin <steamid64>");
            Server.PrintToConsole("Uso: css_removeadmin <steamid64>");
            return;
        }
        HandleRemoveAdmin(caller, steamId);
    }

    [RequiresPermissions("@css/ban")]
    public void BanPlayer(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2 || !ulong.TryParse(info.GetArg(1), out var steamId))
        {
            caller?.PrintToChat("Uso: css_ban <steamid64> <motivo>");
            Server.PrintToConsole("Uso: css_ban <steamid64> <motivo>");
            return;
        }
        Server.PrintToConsole($"[AdminControlPlugin] Admin {caller?.PlayerName ?? "Console"} Baniu o jogador {steamId}.");
        var reason = string.Join(" ", Enumerable.Range(2, info.ArgCount - 2).Select(i => info.GetArg(i)));
        HandleBan(caller, steamId, reason);
    }

    public async void HandleBan(CCSPlayerController? caller, ulong steamId, string reason)
    {
        try
        {
            using var connection = await GetOpenConnectionAsync();
            await connection.ExecuteAsync(@"
                INSERT INTO bans (steamid, reason) VALUES (@SteamId, @Reason)
                ON DUPLICATE KEY UPDATE reason = @Reason, unbanned = FALSE, timestamp = CURRENT_TIMESTAMP;",
                new { SteamId = steamId, Reason = reason });

            _bannedPlayers.Add(steamId);
            _banReasons[steamId] = reason;

            Server.PrintToConsole($"[AdminControlPlugin] Admin {caller?.PlayerName ?? "Console"} baniu o jogador {steamId}. Motivo: {reason}");

            var playerToKick = Utilities.GetPlayers().FirstOrDefault(p => p.AuthorizedSteamID?.SteamId64 == steamId);

            Server.ExecuteCommand($"kickid {steamId} Banned from server");



            // Aplica os banimentos nativos para garantir que não volte
            Server.ExecuteCommand($"banid 0 {steamId}");
            Server.ExecuteCommand($"writeid");

            caller?.PrintToChat($"✅ Jogador {steamId} banido. Motivo: {reason}");
        }
        catch (Exception ex)
        {
            caller?.PrintToChat("❌ Erro ao banir o jogador.");
            Console.WriteLine($"[AdminControlPlugin] ERRO: {ex.Message}");
        }
    }

    public void HandleRemoveAdmin(CCSPlayerController? caller, ulong steamId)
    {
        Task.Run(async () => await ExecuteDbActionAsync(caller, async () =>
        {
            await using var connection = await GetOpenConnectionAsync();
            await connection.ExecuteAsync("DELETE FROM admins WHERE steamid = @SteamId;", new { SteamId = steamId });
            Server.PrintToConsole($"[AdminControlPlugin] Admin {caller?.PlayerName ?? "Console"} removeu o admin {steamId}.");
            await GenerateAdminsJsonAsync();
        }, $"✅ Admin removido com sucesso.", "❌ Erro ao remover admin."));
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

    private async Task GenerateAdminsJsonAsync()
    {
        throw new NotImplementedException();
    }

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

        await connection.ExecuteAsync(@"
        CREATE TABLE IF NOT EXISTS bans (
            id INT UNSIGNED NOT NULL AUTO_INCREMENT,
            steamid BIGINT UNSIGNED NOT NULL,
            reason VARCHAR(255),
            timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
            unbanned BOOLEAN NOT NULL DEFAULT FALSE,
            PRIMARY KEY (id),
            INDEX idx_steamid (steamid)
        );");

        await connection.ExecuteAsync(@"
        CREATE TABLE IF NOT EXISTS ip_bans (
            id INT UNSIGNED NOT NULL AUTO_INCREMENT,
            ip_address VARCHAR(45) NOT NULL,
            reason VARCHAR(255),
            timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
            unbanned BOOLEAN NOT NULL DEFAULT FALSE,
            PRIMARY KEY (id),
            INDEX idx_ip (ip_address)
        );");

        await connection.ExecuteAsync(@"
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
        );");

        await connection.ExecuteAsync(@"
        CREATE TABLE IF NOT EXISTS mutes (
            steamid BIGINT UNSIGNED NOT NULL,
            reason VARCHAR(255),
            timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
            unmuted BOOLEAN NOT NULL DEFAULT FALSE,
            PRIMARY KEY (steamid)
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

    [RequiresPermissions("@css/admin")]
    public void OpenMenuCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null || !caller.IsValid)
        {
            info.ReplyToCommand(T("command_player_only"));
            Server.PrintToConsole($"[AdminControlPlugin] {T("log_menu_attempt_non_player")}");
            return;
        }
        Server.PrintToConsole($"[AdminControlPlugin] {T("log_admin_menu_opened", caller.PlayerName)}");
        _playerCommand?.ShowAdminAndBanMenu(caller);
    }

    public void OpenMenuChatCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null || !caller.IsValid)
        {
            return;
        }
        Server.PrintToConsole($"[AdminControlPlugin] {T("log_admin_menu_opened_chat", caller.PlayerName)}");
        _playerCommand?.ShowAdminAndBanMenu(caller);
    }

    public void StartMapVote(CCSPlayerController caller)
    {
        // Implementação da votação de mapa
        // Nota: O código de votação de mapa não foi implementado nas classes de comando e menu
        // Você pode adicionar a lógica aqui ou em uma nova classe (ex: MapVoteCommand)
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
