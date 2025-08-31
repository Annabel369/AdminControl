using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace AdminControlPlugin;

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