<?php

// Toggles whether user registration is allowed (true) or not (false).
$allow_registration = true;

// Mysql Config
$host = 'localhost';
$db   = 'mariusbd';
$user = 'root';
$pass = '0073007';
$charset = 'utf8mb4';
// ----------------

// server Rcon config 
$cs2_rcon_host = '100.114.210.67';
$cs2_rcon_port = 27018;
$cs2_rcon_password = 'GZPWA3PyZ7zonPf';
$rcon_timeout = 3;
// -----------------------------

function getUserLanguage() {
    $geo = @json_decode(file_get_contents("https://ipapi.co/json/"), true);
    $countryCode = $geo['country_code'] ?? 'US';
    $langCode = ($countryCode === 'BR') ? 'pt' : 'en';
    $langFile = __DIR__ . "/lang/{$langCode}.json";
    if (!file_exists($langFile)) {
        $langFile = __DIR__ . "/lang/en.json";
    }
    return json_decode(file_get_contents($langFile), true);
}

//$lang = getUserLanguage();// ALTOMATIC
$lang = json_decode(file_get_contents(__DIR__ . "/lang/en.json"), true);// EUA 
//$lang = json_decode(file_get_contents(__DIR__ . "/lang/pt.json"), true);

$dsn = "mysql:host=$host;dbname=$db;charset=$charset";
$options = [
    PDO::ATTR_ERRMODE            => PDO::ERRMODE_EXCEPTION,
    PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
    PDO::ATTR_EMULATE_PREPARES   => false,
];

try {
    $pdo = new PDO($dsn, $user, $pass, $options);
} catch (\PDOException $e) {
    throw new \PDOException($e->getMessage(), (int)$e->getCode());
}
?>