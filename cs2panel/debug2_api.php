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
    die(json_encode(["error" => "Falha na conexão: " . $conn->connect_error], JSON_UNESCAPED_UNICODE));
}

$itemsPerPage = 10;
$page = $_GET['page'] ?? 1;
$offset = ($page - 1) * $itemsPerPage;

// Consulta para contar o total de bans por IP
$countSql = "SELECT COUNT(*) AS total_count FROM ip_bans";
$countResult = $conn->query($countSql);
$totalCount = $countResult->fetch_assoc()['total_count'];

// Consulta para obter os dados paginados e ordenados
$sql = "SELECT ip_address, reason, unbanned, timestamp FROM ip_bans ORDER BY timestamp DESC LIMIT $itemsPerPage OFFSET $offset";
$result = $conn->query($sql);

$data = [];
if ($result && $result->num_rows > 0) {
    while($row = $result->fetch_assoc()) {
        if (isset($row['unbanned'])) {
            $row['unbanned'] = (bool)$row['unbanned'];
        }
        $data[] = $row;
    }
}

echo json_encode(["data" => $data, "total_count" => $totalCount], JSON_UNESCAPED_UNICODE);

$conn->close();
?>