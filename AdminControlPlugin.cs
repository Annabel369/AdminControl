using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Commands;
using MySqlConnector;
using Dapper;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AdminControlPlugin;

[MinimumApiVersion(80)]
public class AdminControlPlugin : BasePlugin
{
    public override string ModuleName => "Admin Control with MySQL";
    public override string ModuleVersion => "2.0.0";
    public override string ModuleAuthor => "Amauri Bueno dos Santos";
    public override string ModuleDescription => "Plugin completo para banimentos, admins e RCON com MySQL.";

    private MySqlConnection _connection = null!;

    public override void Load(bool hotReload)
    {
        try
        {
            // Garante que o banco de dados e as tabelas existam de forma síncrona.
            EnsureDatabaseAndTablesExistAsync().GetAwaiter().GetResult();

            // Abre a conexão com o banco de dados de forma síncrona.
            _connection = new MySqlConnection(GetConnectionString());
            _connection.OpenAsync().GetAwaiter().GetResult();

            // Registra todos os comandos de console.
            AddCommand("css_ban", "Ban a player by SteamID64", BanPlayer);
            AddCommand("css_unban", "Unban a player by SteamID64", UnbanPlayer);
            AddCommand("css_listbans", "List all banned players", ListBans);
            AddCommand("css_rcon", "Execute RCON command", ExecuteRcon);
            AddCommand("css_admin", "Grant basic admin", GrantBasicAdmin);
            AddCommand("css_removeadmin", "Remove admin", RemoveAdmin);
            AddCommand("css_addadmin", "Grant custom admin with permission and duration", GrantCustomAdmin);

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

    private async Task EnsureDatabaseAndTablesExistAsync()
    {
        var baseConnectionString = "server=localhost;uid=root;pwd=0073007;";
        using var connection = new MySqlConnection(baseConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync("CREATE DATABASE IF NOT EXISTS mariusbd;");
        await connection.ChangeDatabaseAsync("mariusbd");
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
        return "server=localhost;uid=root;pwd=0073007;database=mariusbd";
    }

    public void BanPlayer(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2 || !ulong.TryParse(info.GetArg(1), out var steamId))
        {
            caller?.PrintToChat("Uso: css_ban <steamid64> <motivo>");
            return;
        }

        var reasonParts = new List<string>();
        for (int i = 2; i < info.ArgCount; i++)
        {
            reasonParts.Add(info.GetArg(i));
        }
        var reason = string.Join(" ", reasonParts);

        Task.Run(async () =>
        {
            try
            {
                await _connection.ExecuteAsync(@"
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

    public void UnbanPlayer(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2 || !ulong.TryParse(info.GetArg(1), out var steamId))
        {
            caller?.PrintToChat("Uso: css_unban <steamid64>");
            return;
        }

        Task.Run(async () =>
        {
            try
            {
                await _connection.ExecuteAsync("UPDATE bans SET unbanned = TRUE WHERE steamid = @SteamId;",
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

    public void ListBans(CCSPlayerController? caller, CommandInfo info)
    {
        Task.Run(async () =>
        {
            try
            {
                var bans = await _connection.QueryAsync("SELECT steamid, reason, timestamp FROM bans WHERE unbanned = FALSE;");
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

        var commandParts = new List<string>();
        for (int i = 1; i < info.ArgCount; i++)
        {
            commandParts.Add(info.GetArg(i));
        }
        var command = string.Join(" ", commandParts);

        // Comando RCON pode ser executado diretamente no thread principal.
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

        var grantedBy = caller?.AuthorizedSteamID?.SteamId64 ?? 0;

        Task.Run(async () =>
        {
            try
            {
                await _connection.ExecuteAsync(@"
                    INSERT INTO admins (steamid, name, permission, level, expires_at, granted_by)
                    VALUES (@SteamId, 'Admin', '@css/basic', 1, NULL, @GrantedBy)
                    ON DUPLICATE KEY UPDATE permission = '@css/basic', level = 1, granted_by = @GrantedBy, timestamp = CURRENT_TIMESTAMP;",
                    new { SteamId = steamId, GrantedBy = grantedBy });

                caller?.PrintToChat($"👑 Admin básico concedido a {steamId}");
            }
            catch (Exception ex)
            {
                caller?.PrintToChat($"❌ Erro ao conceder admin: {ex.Message}");
            }
        });
    }

    public void RemoveAdmin(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2 || !ulong.TryParse(info.GetArg(1), out var steamId))
        {
            caller?.PrintToChat("Uso: css_removeadmin <steamid64>");
            return;
        }

        Task.Run(async () =>
        {
            try
            {
                await _connection.ExecuteAsync("DELETE FROM admins WHERE steamid = @SteamId;", new { SteamId = steamId });
                caller?.PrintToChat($"❌ Admin removido de {steamId}");
            }
            catch (Exception ex)
            {
                caller?.PrintToChat($"❌ Erro ao remover admin: {ex.Message}");
            }
        });
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
                await _connection.ExecuteAsync(@"
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

                caller?.PrintToChat($"👑 Admin {name} ({steamId}) adicionado com permissão {permission}, nível {level}, expira em {expiresAt:dd/MM/yyyy HH:mm}.");
            }
            catch (Exception ex)
            {
                caller?.PrintToChat($"❌ Erro ao adicionar admin: {ex.Message}");
            }
        });
    }
}
