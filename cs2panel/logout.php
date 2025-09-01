<?php
// Inicia a sessão para acessar as variáveis de sessão.
session_start();

// Define uma mensagem de sucesso na sessão antes de destruí-la
// para que a página de destino (index.php) possa exibi-la.
$_SESSION['logout_message'] = "Sua sessão foi encerrada com sucesso. Volte sempre!";

// Limpa todas as variáveis da sessão.
$_SESSION = array();

// Se o cookie de sessão estiver definido, o apaga.
// Isso garante que o navegador do usuário não mantenha uma sessão antiga.
if (ini_get("session.use_cookies")) {
    $params = session_get_cookie_params();
    setcookie(session_name(), '', time() - 42000,
        $params["path"], $params["domain"],
        $params["secure"], $params["httponly"]
    );
}

// Finalmente, destrói a sessão.
session_destroy();

// Redireciona o usuário para a página de destino.
header("Location: index.php");
exit;
?>