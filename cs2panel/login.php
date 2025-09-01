<?php
// Inicia a sessão no início do arquivo para garantir que esteja disponível
session_start();

// Inclui a conexão com o banco de dados
require_once 'db_connect.php';

// Variável para armazenar mensagens de erro
$error = '';

// Verifica se a requisição é do tipo POST (se o formulário foi enviado)
if ($_SERVER["REQUEST_METHOD"] == "POST") {
    $username = $_POST['username'] ?? '';
    $password = $_POST['password'] ?? '';

    // Prepara a consulta para evitar injeção SQL
    $stmt = $pdo->prepare("SELECT * FROM users WHERE username = ?");
    $stmt->execute([$username]);
    $user = $stmt->fetch();

    // Verifica se o usuário existe e se a senha está correta
    if ($user && password_verify($password, $user['password'])) {
        // Se as credenciais estiverem corretas, define as variáveis de sessão
        $_SESSION['loggedin'] = true;
        $_SESSION['username'] = $user['username'];

        // Redireciona o usuário para a página protegida
        header("Location: Protocon.php");
        exit;
    } else {
        // Define uma mensagem de erro em caso de falha no login
        $error = $lang['login_error_message'];
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
    <style>
        /* CSS para o layout menor, como solicitado */
        .container {
            max-width: 400px;
            margin-top: 5rem;
            padding: 1.5rem;
        }
        .header {
            padding-bottom: 0.75rem;
        }
    </style>
</head>
<body>
    <div class="container">
        <div class="header" style="justify-content: center;">
            <h1>
                <img src="./img/tdmueatdmueatdmu.png" alt="Counter-Strike 2" style="width: 50px;" />
                <?= $lang['panel_title'] ?>
            </h1>
        </div>

        <div class="content-section active">
            <h2 style="text-align: center; margin-bottom: 2rem;"><?= $lang['login_h2'] ?></h2>
            <?php if ($error): ?>
                <p style="color: var(--ban-red); text-align: center;"><?= $error ?></p>
            <?php endif; ?>

            <form action="login.php" method="POST" style="display: flex; flex-direction: column; gap: 1rem;">
                <label for="username" style="font-weight: 600;"><?= $lang['username_label'] ?>:</label>
                <input type="text" id="username" name="username" required 
                       style="padding: 0.75rem; border-radius: 8px; border: none; background-color: #2a2a2a; color: #f0f0f0;">
                
                <label for="password" style="font-weight: 600;"><?= $lang['password_label'] ?>:</label>
                <input type="password" id="password" name="password" required
                       style="padding: 0.75rem; border-radius: 8px; border: none; background-color: #2a2a2a; color: #f0f0f0;">
                
                <button type="submit" 
                        style="margin-top: 1rem; padding: 0.75rem 1.5rem; background: linear-gradient(45deg, var(--accent-purple), var(--accent-pink)); border: none; border-radius: 8px; color: #fff; font-weight: 600; cursor: pointer;">
                    <?= $lang['login_button'] ?>
                </button>
            </form>

            <?php if ($allow_registration): ?>
                <div style="text-align: center; margin-top: 1.5rem;">
                    <a href="register.php" style="color: var(--accent-purple); text-decoration: none; font-weight: 600;">
                        <?= $lang['register_link'] ?>
                    </a>
                </div>
            <?php endif; ?>
        </div>
    </div>
</body>
</html>