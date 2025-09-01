<?php
// Inicia a sessão e verifica se o usuário está logado
session_start();
if (!isset($_SESSION['loggedin']) || $_SESSION['loggedin'] !== true) {
    header("Location: login.php");
    exit;
}

// Inclui os arquivos necessários para a página
require_once 'rcon2.php';
include 'db_connect.php';

$output_message = $lang['no_command'];

if ($_SERVER["REQUEST_METHOD"] === "POST" && ($_POST["cs2_command_action"] ?? '') === "rcon") {
    $command = trim($_POST["rcon_command_input"] ?? '');
    if (!empty($command)) {
        $rcon = new Rcon($cs2_rcon_host, $cs2_rcon_port, $cs2_rcon_password, $rcon_timeout);
        if ($rcon->connect()) {
            $response_rcon = $rcon->send_command($command);
            if ($response_rcon !== false) {
                $clean_response = trim(str_replace("\x01", '', $response_rcon));
                if (stripos($command, 'status') !== false) {
                    $icon = '📊';
                } elseif (stripos($clean_response, 'sucesso') !== false) {
                    $icon = '✅';
                } elseif (stripos($clean_response, 'erro') !== false || stripos($clean_response, 'uso:') !== false) {
                    $icon = '❌';
                } else {
                    $icon = 'ℹ️';
                }
                $output_message = "Command sent: <code>{$command}</code><br>Response:<br><pre>{$icon} " . htmlspecialchars($clean_response) . "</pre>";
            } else {
                $output_message = "<span style='color:red;'>{$lang['error_send']} " . htmlspecialchars($rcon->get_response()) . "</span>";
            }
            $rcon->disconnect();
        } else {
            $output_message = "<span style='color:red;'>{$lang['error_connect']}</span>";
        }
    } else {
        $output_message = "<span style='color:orange;'>{$lang['empty_command']}</span>";
    }
}
?>
<!DOCTYPE html>
<html lang="<?= $lang['lang_code'] ?>">
<head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <link rel="icon" href="./img/favicon.png" type="image/png" />
    <title><?= $lang['title'] ?></title>
    <link rel="stylesheet" href="style.css" />
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700&display=swap" rel="stylesheet">
    <script>
        document.addEventListener('DOMContentLoaded', function() {
            document.getElementById('logout-button').addEventListener('click', function() {
                window.location.href = 'logout.php';
            });
        });
    </script>
</head>
<body>
<div style="display: flex; justify-content: flex-end;">
    <button class="tab-button" id="logout-button">
        <i class="fas fa-terminal icon-small"></i>🔒 Sair
    </button>
</div>
<div class="container">
    <div class="header">
        <h1>
            <img src="./img/tdmueatdmueatdmu.png" alt="Counter-Strike 2" style="width: 50px;" />
            <?= $lang['panel_title'] ?><i class="fas fa-terminal icon-small"></i> RCON
        </h1>
    </div>

    <div class="content-section active">
        <form method="POST">
            <label for="rcon_command_input" style="display:block; margin-top:1rem; font-weight:600;"><?= $lang['rcon_label'] ?></label>
            <input type="text" id="rcon_command_input" name="rcon_command_input" placeholder="<?= $lang['rcon_placeholder'] ?>" style="width:100%; padding:0.75rem; margin-top:0.5rem; border-radius:8px; border:none; background-color:#2a2a2a; color:#f0f0f0;">
            
            <button type="submit" name="cs2_command_action" value="rcon" style="margin-top:1rem; padding:0.75rem 1.5rem; background:linear-gradient(45deg, var(--accent-purple), var(--accent-pink)); border:none; border-radius:8px; color:#fff; font-weight:600; cursor:pointer;"><?= $lang['rcon_button'] ?></button>
        </form>

        <div style="margin-top:2rem;">
            <a href="steam://connect/100.114.210.67:27018">
                <button type="button" style="padding:0.75rem 1.5rem; background-color:#2a2a2a; border:none; border-radius:8px; color:#f0f0f0; font-weight:600; cursor:pointer;"><?= $lang['steam_button'] ?></button>
            </a>
            <div class="output" style="margin-top:2rem; background-color:#232323; padding:1rem; border-radius:8px;">
                <?php echo $output_message; ?>
            </div>
        </div>
        
        <div class="content-section active" id="submenu-css-admin">
            <h2><?= $lang['admin_commands_title'] ?><span style="color: var(--accent-purple);">@css/admin</span></h2>
            <table>
                <thead>
                    <tr>
                        <th>Command</th>
                        <th>Description</th>
                        <th>Category</th>
                    </tr>
                </thead>
                <tbody>
                    <tr><td><span class="icon-small">📊</span> status</td><td><?= $lang['cmd_status'] ?></td><td>@css/admin</td></tr>
                    <tr><td><span class="icon-ban">🚫</span> css_ban</td><td><?= $lang['cmd_css_ban'] ?></td><td>@css/ban</td></tr>
                    <tr><td><span class="icon-unban">✅</span> css_unban</td><td><?= $lang['cmd_css_unban'] ?></td><td>@css/unban</td></tr>
                    <tr><td><span class="icon-ip">🌐</span> css_ipban</td><td><?= $lang['cmd_css_ipban'] ?></td><td>@css/ban</td></tr>
                    <tr><td><span class="icon-unban">🔓</span> css_unbanip</td><td><?= $lang['cmd_css_unbanip'] ?></td><td>@css/unban</td></tr>
                    <tr><td><span class="icon-small">🔇</span> css_mute</td><td><?= $lang['cmd_css_mute'] ?></td><td>@css/admin</td></tr>
                    <tr>
                        <td><span class="icon-unban">👑</span> css_addadmin</td>
                        <td>
                            <?= $lang['cmd_css_addadmin'] ?><br>
                            <span style="font-size: 0.85em; color: #aaa; opacity: 0.7;">
                                <?= $lang['cmd_example_addadmin'] ?>
                            </span>
                        </td>
                        <td>@css/root</td>
                    </tr>
                    <tr><td><span class="icon-ban">❌</span> css_removeadmin</td><td><?= $lang['cmd_css_removeadmin'] ?></td><td>@css/root</td></tr>
                    <tr><td><span class="icon-unban">🔈</span> css_unmute</td><td><?= $lang['cmd_css_unmute'] ?></td><td>@css/admin</td></tr>
                    <tr><td><span class="icon-small">📦</span> meta list</td><td><?= $lang['cmd_meta_list'] ?></td><td>@meta</td></tr>
                    <tr><td><span class="icon-small">📄</span> meta version</td><td><?= $lang['cmd_meta_version'] ?></td><td>@meta</td></tr>
                    <tr><td><span class="icon-small">🧩</span> css_plugins list</td><td><?= $lang['cmd_css_plugins_list'] ?></td><td>@css/plugins</td></tr>
                </tbody>
            </table>
        </div>
        <footer style="margin-top: 40px; font-size: 0.85em; color: #666;">
            <?= $lang['footer'] ?>
        </footer>
    </div>
</div>
</body>
</html>
