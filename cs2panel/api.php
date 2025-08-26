<?php
header('Content-Type: application/json');

// Defina as credenciais do seu banco de dados
$servername = "localhost";
$username = "root";
$password = "0073007"; // Insira sua senha aqui
$dbname = "mariusbd";

// Estabelece a conexão
$conn = new mysqli($servername, $username, $password, $dbname);

// Verifica a conexão
if ($conn->connect_error) {
    die(json_encode(["error" => "Falha na conexão: " . $conn->connect_error]));
}

// Obtém os parâmetros da URL
$type = $_GET['type'] ?? 'admins';
$page = $_GET['page'] ?? 1;
$itemsPerPage = 10;
$offset = ($page - 1) * $itemsPerPage;

$sql = "";
$countSql = "";
$result = null;
$totalCount = 0;

if ($type === 'admins') {
    // Consulta para contar o total de admins
    $countSql = "SELECT COUNT(*) AS total_count FROM admins";
    $countResult = $conn->query($countSql);
    $totalCount = $countResult->fetch_assoc()['total_count'];
    
    // Consulta para obter os dados paginados e ordenados
    $sql = "SELECT name, steamid, permission, level, timestamp AS granted_at FROM admins ORDER BY timestamp DESC LIMIT $itemsPerPage OFFSET $offset";
    $result = $conn->query($sql);

} elseif ($type === 'bans') {
    // Consulta para contar o total de bans
    $countSql = "SELECT COUNT(*) AS total_count FROM bans";
    $countResult = $conn->query($countSql);
    $totalCount = $countResult->fetch_assoc()['total_count'];

    // Consulta para obter os dados paginados e ordenados
    $sql = "SELECT steamid, reason, unbanned, timestamp FROM bans ORDER BY timestamp DESC LIMIT $itemsPerPage OFFSET $offset";
    $result = $conn->query($sql);
    
} else {
    die(json_encode(["error" => "Tipo de dados inválido."]));
}

$data = [];
if ($result && $result->num_rows > 0) {
    while($row = $result->fetch_assoc()) {
        // Converte o booleano de 'unbanned' para o tipo correto para o JavaScript
        if (isset($row['unbanned'])) {
            $row['unbanned'] = (bool)$row['unbanned'];
        }
        $data[] = $row;
    }
}

echo json_encode(["data" => $data, "total_count" => $totalCount]);

$conn->close();
?>