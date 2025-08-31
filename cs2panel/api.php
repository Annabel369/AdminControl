<?php
// Set the content type to JSON for the API response
header('Content-Type: application/json');

// Include the database connection file that uses PDO
require_once 'db_connect.php';

// Get URL parameters
$type = $_GET['type'] ?? 'admins';
$page = $_GET['page'] ?? 1;
$itemsPerPage = 10;
$offset = ($page - 1) * $itemsPerPage;

$data = [];
$totalCount = 0;

try {
    // Determine which query to run based on the 'type' parameter
    switch ($type) {
        case 'admins':
            $countSql = "SELECT COUNT(*) AS total_count FROM admins";
            $sql = "SELECT name, steamid, permission, level, timestamp AS granted_at FROM admins ORDER BY timestamp DESC LIMIT :itemsPerPage OFFSET :offset";
            break;
        case 'bans':
            $countSql = "SELECT COUNT(*) AS total_count FROM bans";
            $sql = "SELECT steamid, reason, unbanned, timestamp FROM bans ORDER BY timestamp DESC LIMIT :itemsPerPage OFFSET :offset";
            break;
        case 'ip_bans':
            $countSql = "SELECT COUNT(*) AS total_count FROM ip_bans";
            $sql = "SELECT ip_address, reason, unbanned, timestamp FROM ip_bans ORDER BY timestamp DESC LIMIT :itemsPerPage OFFSET :offset";
            break;
        case 'mutes':
            $countSql = "SELECT COUNT(*) AS total_count FROM mutes";
            $sql = "SELECT steamid, reason, unmuted, timestamp FROM mutes ORDER BY timestamp DESC LIMIT :itemsPerPage OFFSET :offset";
            break;
        default:
            // Handle invalid data type and return an error
            http_response_code(400); // Bad Request
            echo json_encode(["error" => "Invalid data type."], JSON_UNESCAPED_UNICODE);
            exit;
    }

    // First, get the total count of items
    $countStmt = $pdo->prepare($countSql);
    $countStmt->execute();
    $totalCount = $countStmt->fetchColumn();
    
    // Now, fetch the paginated data using a prepared statement
    $dataStmt = $pdo->prepare($sql);
    $dataStmt->bindValue(':itemsPerPage', $itemsPerPage, PDO::PARAM_INT);
    $dataStmt->bindValue(':offset', $offset, PDO::PARAM_INT);
    $dataStmt->execute();

    // Fetch all results as an associative array
    $data = $dataStmt->fetchAll(PDO::FETCH_ASSOC);

    // Convert boolean-like values for JavaScript
    foreach ($data as &$row) {
        if (isset($row['unbanned'])) {
            $row['unbanned'] = (bool)$row['unbanned'];
        }
        if (isset($row['unmuted'])) {
            $row['unmuted'] = (bool)$row['unmuted'];
        }
    }
    unset($row); // Break the reference

    // Return the data and total count
    echo json_encode(["data" => $data, "total_count" => $totalCount], JSON_UNESCAPED_UNICODE);

} catch (\PDOException $e) {
    // Handle database connection or query errors
    http_response_code(500); // Internal Server Error
    echo json_encode(["error" => "Database error: " . $e->getMessage()], JSON_UNESCAPED_UNICODE);
    exit;
}
?>