using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Commands;
using Dapper;
using MySqlConnector;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Cvars;

namespace AdminControlPlugin;

[MinimumApiVersion(130)] // Usa a versão moderna da API.
public class AdminControlPlugin : BasePlugin, IPluginConfig<AdminControlPlugin.AdminControlConfig>
{
    public override string ModuleName => "Admin Control with MySQL";
    public override string ModuleVersion => "4.0.10";
    public override string ModuleAuthor => "Amauri Bueno dos Santos & Gemini (Code Fixes)";
    public override string ModuleDescription => "Plugin completo para banimentos, admins e RCON com MySQL e segurança aprimorada.";

    private MySqlConnection? _connection;

    // A API preenche esta propriedade automaticamente.
    public AdminControlConfig Config { get; set; } = new AdminControlConfig();

    // Este método é chamado automaticamente pela API após a leitura do arquivo de configuração.
    public void OnConfigParsed(AdminControlConfig config)
    {
        Config = config;
    }

    // Classe de modelo de dados para a configuração do plugin.
    // Herda de BasePluginConfig para ser compatível com a API.
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

    // Classe de modelos de dados para o arquivo admins.json.
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

    // Classe de modelo de dados para a tabela de admins no banco de dados.
    public class DbAdmin
    {
        public ulong steamid { get; set; }
        public string? name { get; set; }
        public string? permission { get; set; }
        public int level { get; set; }
    }

    public override void Load(bool hotReload)
    {
        try
        {
            EnsureSharedConfigFilesExist();

            EnsureDatabaseAndTablesExistAsync().GetAwaiter().GetResult();
            _connection = new MySqlConnection(GetConnectionString());
            _connection.OpenAsync().GetAwaiter().GetResult();

            AddCommand("css_ban", "Ban a player by SteamID64", BanPlayer);
            AddCommand("css_unban", "Unban a player by SteamID64", UnbanPlayer);
            AddCommand("css_listbans", "List all banned players", ListBans);
            AddCommand("css_rcon", "Execute RCON command", ExecuteRcon);
            AddCommand("css_admin", "Grant basic admin", GrantBasicAdmin);
            AddCommand("css_removeadmin", "Remove admin", RemoveAdmin);
            AddCommand("css_addadmin", "Grant custom admin with permission and duration", GrantCustomAdmin);

            AddCommand("!ban", "Ban a player by name", ChatBanPlayer);
            AddCommand("!unban", "Unban a player by name", ChatUnbanPlayer);
            AddCommand("!admin", "Grant basic admin to a player by name", ChatGrantBasicAdmin);
            AddCommand("!removeadm", "Remove admin from a player by name", ChatRemoveAdmin);

            Task.Run(async () => await GenerateAdminsJsonAsync());

            Console.WriteLine("[AdminControlPlugin] Plugin carregado com sucesso!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminControlPlugin] ERRO ao carregar o plugin: {ex.Message}");
        }
    }

    public override void Unload(bool hotReload)
    {
        _connection?.Close();
        _connection?.Dispose();
        Console.WriteLine("[AdminControlPlugin] Plugin descarregado.");
    }

    // Método para garantir que os arquivos de configuração compartilhados existam.
    private void EnsureSharedConfigFilesExist()
    {
        var configsDir = Path.Combine(ModuleDirectory, "../../configs/");
        var groupsPath = Path.Combine(configsDir, "groups.json");
        var overridesPath = Path.Combine(configsDir, "admin_overrides.json");
        var corePath = Path.Combine(configsDir, "core.json");

        Directory.CreateDirectory(configsDir);

        var groupsJsonContent = @"
{
  ""#css/admin"": {
    ""flags"": [
      ""@css/reservation"",
      ""@css/generic"",
      ""@css/kick"",
      ""@css/ban"",
      ""@css/unban"",
      ""@css/vip"",
      ""@css/slay"",
      ""@css/changemap"",
      ""@css/cvar"",
      ""@css/config"",
      ""@css/chat"",
      ""@css/vote"",
      ""@css/password"",
      ""@css/rcon"",
      ""@css/cheats"",
      ""@css/root""
    ],
    ""immunity"": 99
  },
  
  ""#css/custom-permission"": {
    ""flags"": [
      ""@css/reservation"",
      ""@css/vip"",
      ""@css/generic"",
      ""@css/chat"",
      ""@css/vote"",
      ""@css/custom-permission""
    ],
    ""immunity"": 40
  }
}";

        var overridesJsonContent = @"
{
  ""vip_store_given_by"": {
    ""flags"": [
      ""@css/custom-permission""
    ],
    ""check_type"": ""all"",
    ""enabled"": true
  }
}";

        var coreJsonContent = @"
{
  ""PublicChatTrigger"": [ ""!"" ],
  ""SilentChatTrigger"": [ ""/"" ],
  ""FollowCS2ServerGuidelines"": false,
  ""PluginHotReloadEnabled"": true,
  ""PluginAutoLoadEnabled"": true,
  ""ServerLanguage"": ""en"",
  ""UnlockConCommands"": true,
  ""UnlockConVars"": true
}";

        if (!File.Exists(groupsPath))
        {
            File.WriteAllText(groupsPath, groupsJsonContent);
        }

        if (!File.Exists(overridesPath))
        {
            File.WriteAllText(overridesPath, overridesJsonContent);
        }

        if (!File.Exists(corePath))
        {
            File.WriteAllText(corePath, coreJsonContent);
        }
    }

    private async Task EnsureDatabaseAndTablesExistAsync()
    {
        var baseConnectionString = $"server={Config.Host};uid={Config.User};pwd={Config.Password};";
        using var connection = new MySqlConnection(baseConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync($"CREATE DATABASE IF NOT EXISTS {Config.Database};");
        await connection.ChangeDatabaseAsync(Config.Database);
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS bans (
                steamid BIGINT UNSIGNED NOT NULL,
                reason VARCHAR(255),
                timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                unbanned BOOLEAN NOT NULL DEFAULT FALSE,
                PRIMARY KEY (steamid)
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

    private string GetConnectionString()
    {
        return $"server={Config.Host};uid={Config.User};pwd={Config.Password};database={Config.Database}";
    }

    private async Task GenerateAdminsJsonAsync()
    {
        try
        {
            var admins = await _connection!.QueryAsync<DbAdmin>("SELECT steamid, name, permission, level FROM admins;");
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

    private CCSPlayerController? FindPlayerByName(CCSPlayerController? caller, string name)
    {
        var players = Utilities.GetPlayers().Where(p => p.IsValid && p.PlayerName.Contains(name, StringComparison.OrdinalIgnoreCase)).ToList();

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

    private void HandleBan(CCSPlayerController? caller, ulong steamId, string reason)
    {
        Task.Run(async () =>
        {
            try
            {
                await _connection!.ExecuteAsync(@"
                    INSERT INTO bans (steamid, reason) VALUES (@SteamId, @Reason)
                    ON DUPLICATE KEY UPDATE reason = @Reason, unbanned = FALSE, timestamp = CURRENT_TIMESTAMP;",
                    new { SteamId = steamId, Reason = reason });

                Server.NextFrame(() =>
                {
                    Server.ExecuteCommand($"banid 0 {steamId} kick");
                    caller?.PrintToChat($"✅ Jogador {steamId} banido. Motivo: {reason}");
                });
            }
            catch (Exception ex)
            {
                caller?.PrintToChat($"❌ Erro ao banir o jogador: {ex.Message}");
            }
        });
    }

    private void HandleUnban(CCSPlayerController? caller, ulong steamId)
    {
        Task.Run(async () =>
        {
            try
            {
                await _connection!.ExecuteAsync("UPDATE bans SET unbanned = TRUE WHERE steamid = @SteamId;",
                    new { SteamId = steamId });

                Server.NextFrame(() =>
                {
                    Server.ExecuteCommand($"removeid {steamId}");
                    caller?.PrintToChat($"✅ Jogador {steamId} desbanido.");
                });
            }
            catch (Exception ex)
            {
                caller?.PrintToChat($"❌ Erro ao desbanir o jogador: {ex.Message}");
            }
        });
    }

    private void HandleGrantAdmin(CCSPlayerController? caller, ulong steamId, string? name)
    {
        var grantedBy = caller?.AuthorizedSteamID?.SteamId64 ?? 0;

        Task.Run(async () =>
        {
            try
            {
                await _connection!.ExecuteAsync(@"
                    INSERT INTO admins (steamid, name, permission, level, expires_at, granted_by)
                    VALUES (@SteamId, @Name, '@css/basic', 1, NULL, @GrantedBy)
                    ON DUPLICATE KEY UPDATE permission = '@css/basic', level = 1, granted_by = @GrantedBy, timestamp = CURRENT_TIMESTAMP;",
                    new { SteamId = steamId, Name = name, GrantedBy = grantedBy });

                await GenerateAdminsJsonAsync();
                caller?.PrintToChat($"👑 Admin básico concedido a {name} ({steamId})");
            }
            catch (Exception ex)
            {
                caller?.PrintToChat($"❌ Erro ao conceder admin: {ex.Message}");
            }
        });
    }

    private void HandleRemoveAdmin(CCSPlayerController? caller, ulong steamId)
    {
        Task.Run(async () =>
        {
            try
            {
                await _connection!.ExecuteAsync("DELETE FROM admins WHERE steamid = @SteamId;", new { SteamId = steamId });
                await GenerateAdminsJsonAsync();
                caller?.PrintToChat($"❌ Admin removido de {steamId}");
            }
            catch (Exception ex)
            {
                caller?.PrintToChat($"❌ Erro ao remover admin: {ex.Message}");
            }
        });
    }

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

    public void UnbanPlayer(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2 || !ulong.TryParse(info.GetArg(1), out var steamId))
        {
            caller?.PrintToChat("Uso: css_unban <steamid64>");
            return;
        }
        HandleUnban(caller, steamId);
    }

    public void ListBans(CCSPlayerController? caller, CommandInfo info)
    {
        Task.Run(async () =>
        {
            try
            {
                var bans = await _connection!.QueryAsync("SELECT steamid, reason, timestamp FROM bans WHERE unbanned = FALSE;");
                foreach (var ban in bans)
                {
                    caller?.PrintToChat($"🚫 Banido: {ban.steamid} | Motivo: {ban.reason} | Data: {ban.timestamp}");
                }
            }
            catch (Exception ex)
            {
                caller?.PrintToChat($"❌ Erro ao listar banimentos: {ex.Message}");
            }
        });
    }

    public void ExecuteRcon(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2)
        {
            caller?.PrintToChat("Uso: css_rcon <comando>");
            return;
        }
        var commandParts = Enumerable.Range(1, info.ArgCount - 1).Select(i => info.GetArg(i));
        var command = string.Join(" ", commandParts);
        Server.ExecuteCommand(command);
        caller?.PrintToChat($"📡 Comando RCON executado: {command}");
    }

    public void GrantBasicAdmin(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2 || !ulong.TryParse(info.GetArg(1), out var steamId))
        {
            caller?.PrintToChat("Uso: css_admin <steamid64>");
            return;
        }

        var name = "Admin";
        var player = Utilities.GetPlayers().FirstOrDefault(p => p.AuthorizedSteamID?.SteamId64 == steamId);
        if (player != null)
        {
            name = player.PlayerName;
        }

        HandleGrantAdmin(caller, steamId, name);
    }

    public void RemoveAdmin(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2 || !ulong.TryParse(info.GetArg(1), out var steamId))
        {
            caller?.PrintToChat("Uso: css_removeadmin <steamid64>");
            return;
        }
        HandleRemoveAdmin(caller, steamId);
    }

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
        var grantedBy = caller?.AuthorizedSteamID?.SteamId64 ?? 0;

        Task.Run(async () =>
        {
            try
            {
                await _connection!.ExecuteAsync(@"
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
                            GrantedBy = grantedBy
                        });

                await GenerateAdminsJsonAsync();
                caller?.PrintToChat($"👑 Admin {name} ({steamId}) adicionado com permissão {permission}, nível {level}, expira em {expiresAt:dd/MM/yyyy HH:mm}.");
            }
            catch (Exception ex)
            {
                caller?.PrintToChat($"❌ Erro ao adicionar admin: {ex.Message}");
            }
        });
    }

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
        var reasonParts = new List<string>();
        for (int i = 2; i < info.ArgCount; i++)
        {
            reasonParts.Add(info.GetArg(i));
        }
        var reason = string.Join(" ", reasonParts);
        if (string.IsNullOrEmpty(reason))
        {
            reason = "Sem motivo especificado.";
        }

        var playerToBan = FindPlayerByName(caller, playerName);

        if (playerToBan == null)
        {
            return;
        }

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

        if (playerToUnban == null)
        {
            return;
        }

        HandleUnban(caller, playerToUnban.AuthorizedSteamID!.SteamId64);
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

        if (playerToGrant == null)
        {
            return;
        }

        HandleGrantAdmin(caller, playerToGrant.AuthorizedSteamID!.SteamId64, playerName);
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

        if (playerToRemove == null)
        {
            return;
        }
        HandleRemoveAdmin(caller, playerToRemove.AuthorizedSteamID!.SteamId64);
    }
}