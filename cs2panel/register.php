<?php
// Include the database connection file.
require_once 'db_connect.php';

// Initialize a variable to hold messages (success or error).
$message = '';

// Check if registration is allowed before processing the form.
if (!$allow_registration) {
    // If registration is disabled, set a message to inform the user
    $message = $lang['registration_disabled'];
} else if ($_SERVER["REQUEST_METHOD"] === "POST") {
    // Get the username and password from the form input.
    $username = trim($_POST['username'] ?? '');
    $password = $_POST['password'] ?? '';

    // Validate that the fields are not empty.
    if (!empty($username) && !empty($password)) {
        try {
            // Check if the username already exists in the database.
            $stmt = $pdo->prepare("SELECT COUNT(*) FROM users WHERE username = ?");
            $stmt->execute([$username]);
            $count = $stmt->fetchColumn();

            // If count is greater than 0, the username already exists.
            if ($count > 0) {
                $message = $lang['user_exists_error'];
            } else {
                // Hash the password for secure storage.
                $hashed_password = password_hash($password, PASSWORD_DEFAULT);

                // Insert the new user into the 'users' table.
                $insert_stmt = $pdo->prepare("INSERT INTO users (username, password) VALUES (?, ?)");
                $insert_stmt->execute([$username, $hashed_password]);

                // Set a success message.
                $message = sprintf($lang['registration_success'], htmlspecialchars($username));
            }
        } catch (\PDOException $e) {
            // Handle any database-related errors.
            $message = $lang['database_error'] . ": " . $e->getMessage();
        }
    } else {
        $message = $lang['empty_fields_error'];
    }
}
?>
<!DOCTYPE html>
<html lang="<?= $lang['lang_code'] ?>">
<head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <link rel="icon" href="./img/favicon.png" type="image/png" />
    <title><?= $lang['register_title'] ?></title>
    <link rel="stylesheet" href="style.css" />
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700&display=swap" rel="stylesheet">
    <style>
        /* Additional styling for this specific page to match the theme */
        body {
            display: flex;
            justify-content: center;
            align-items: center;
            min-height: 100vh;
        }
        .container {
            max-width: 400px; /* Reduces the maximum width */
            margin-top: 5rem;
            padding: 1.5rem; /* Reduces padding */
        }
        .header {
            padding-bottom: 0.75rem; /* Reduces padding */
        }
        .contact-message {
            margin-top: 2rem;
            font-size: 0.9em;
            color: #ccc;
        }
        .contact-message a {
            color: var(--accent-purple);
            text-decoration: none;
            font-weight: 600;
        }
    </style>
</head>
<body>
    <div class="container">
        <div class="header" style="justify-content: center;">
            <h1>
                <img src="./img/tdmueatdmueatdmu.png" alt="Counter-Strike 2" style="width: 50px;" />
                <?= $lang['register_h1'] ?>
            </h1>
        </div>

        <div class="content-section active">
            <h2 style="text-align: center; margin-bottom: 2rem;"><?= $lang['register_h2'] ?></h2>
            
            <?php if (!empty($message)): ?>
                <p style="text-align: center; color: var(--accent-purple);"><?= htmlspecialchars($message) ?></p>
            <?php endif; ?>

            <?php if ($allow_registration): ?>
                <form action="register.php" method="POST" style="display: flex; flex-direction: column; gap: 1rem;">
                    <label for="username" style="font-weight: 600;"><?= $lang['username_label'] ?>:</label>
                    <input type="text" id="username" name="username" required 
                           style="padding: 0.75rem; border-radius: 8px; border: none; background-color: #2a2a2a; color: #f0f0f0;">
                    
                    <label for="password" style="font-weight: 600;"><?= $lang['password_label'] ?>:</label>
                    <input type="password" id="password" name="password" required
                           style="padding: 0.75rem; border-radius: 8px; border: none; background-color: #2a2a2a; color: #f0f0f0;">
                    
                    <button type="submit" 
                            style="margin-top: 1rem; padding: 0.75rem 1.5rem; background: linear-gradient(45deg, var(--accent-purple), var(--accent-pink)); border: none; border-radius: 8px; color: #fff; font-weight: 600; cursor: pointer;">
                        <?= $lang['register_button'] ?>
                    </button>
                </form>
            <?php else: ?>
                <div class="contact-message">
                    <p style="text-align: center;">
                        <?= $lang['contact_admin'] ?>
                    </p>
                </div>
            <?php endif; ?>
        </div>
    </div>
</body>
</html>