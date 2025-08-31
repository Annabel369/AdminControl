<?php
session_start();
require_once 'db_connect.php';

$error = '';

try {
    // Attempt to get information from the 'users' table. If it does not exist, a
    // PDOException will be thrown and caught below.
    $pdo->query("SELECT 1 FROM users LIMIT 1");
} catch (\PDOException $e) {
    // The table does not exist, so we create it.
    $sql = "CREATE TABLE users (
        id INT AUTO_INCREMENT PRIMARY KEY,
        username VARCHAR(50) NOT NULL UNIQUE,
        password VARCHAR(255) NOT NULL,
        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
    );";
    $pdo->exec($sql);
    
    // Add a default user for the first login
    $default_user = 'admin';
    $default_pass = 'admin123';
    $hashed_password = password_hash($default_pass, PASSWORD_DEFAULT);

    $stmt = $pdo->prepare("INSERT INTO users (username, password) VALUES (?, ?)");
    $stmt->execute([$default_user, $hashed_password]);

    // Set a translated success message
    $error = $lang['db_success_message'];
}

if ($_SERVER["REQUEST_METHOD"] == "POST") {
    $username = $_POST['username'] ?? '';
    $password = $_POST['password'] ?? '';

    $stmt = $pdo->prepare("SELECT * FROM users WHERE username = ?");
    $stmt->execute([$username]);
    $user = $stmt->fetch();

    if ($user && password_verify($password, $user['password'])) {
        $_SESSION['loggedin'] = true;
        $_SESSION['username'] = $user['username'];
        header("Location: index.php");
        exit;
    } else {
        // Set a translated error message
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
        /* CSS to make the layout smaller */
        .container {
            max-width: 400px; /* Reduces the maximum width */
            margin-top: 5rem;
            padding: 1.5rem; /* Reduces padding */
        }
        .header {
            padding-bottom: 0.75rem; /* Reduces padding */
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