<?php
header('Content-Type: application/json');

// Defina as credenciais do seu banco de dados
$servername = "localhost";
$username = "root";
$password = "0073007"; // Insira sua senha aqui
$dbname = "mariusbd";

// Objeto de resposta para depuração
$debug_response = [
    "status" => "error",
    "message" => "",
    "database_name" => $dbname,
    "tables" => []
];

// Conecta ao MySQL
$conn = new mysqli($servername, $username, $password);

// 1. Verifica a conexão inicial
if ($conn->connect_error) {
    $debug_response["message"] = "Falha na conexão com o MySQL: " . $conn->connect_error;
    echo json_encode($debug_response, JSON_PRETTY_PRINT);
    exit();
}

// 2. Tenta selecionar o banco de dados
if (!$conn->select_db($dbname)) {
    $debug_response["message"] = "Banco de dados '$dbname' n\u00e3o encontrado ou n\u00e3o acess\u00edvel.";
    echo json_encode($debug_response, JSON_PRETTY_PRINT);
    $conn->close();
    exit();
}

$debug_response["status"] = "success";
$debug_response["message"] = "Conex\u00e3o com o banco de dados '$dbname' bem-sucedida.";

// Lista das tabelas que queremos inspecionar
$tables_to_check = ['admins', 'bans', 'ip_bans'];

foreach ($tables_to_check as $table) {
    $table_data = [
        "name" => $table,
        "exists" => false,
        "structure" => [],
        "content" => [],
        "error" => null
    ];

    // 3. Verifica se a tabela existe
    $table_exists_query = $conn->query("SHOW TABLES LIKE '{$table}'");
    if ($table_exists_query->num_rows > 0) {
        $table_data["exists"] = true;

        // 4. Obtém a estrutura da tabela
        $structure_query = $conn->query("DESCRIBE `{$table}`");
        if ($structure_query) {
            while ($row = $structure_query->fetch_assoc()) {
                $table_data["structure"][] = $row;
            }
        } else {
            $table_data["error"] = "Erro ao obter a estrutura da tabela '{$table}': " . $conn->error;
        }

        // 5. Obtém o conteúdo da tabela (limitado para evitar sobrecarga)
        $content_query = $conn->query("SELECT * FROM `{$table}` ORDER BY timestamp DESC LIMIT 10");
        if ($content_query) {
            while ($row = $content_query->fetch_assoc()) {
                $table_data["content"][] = $row;
            }
        } else {
            $table_data["error"] = "Erro ao obter o conte\u00fado da tabela '{$table}': " . $conn->error;
        }
    } else {
        $table_data["message"] = "Tabela '{$table}' n\u00e3o existe no banco de dados.";
    }

    $debug_response["tables"][] = $table_data;
}

echo json_encode($debug_response, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);

$conn->close();
?>