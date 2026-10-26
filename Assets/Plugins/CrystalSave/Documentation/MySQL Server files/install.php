<?php
// Basic installer script for Crystal Save's MySQL backend.
// Fill in your own credentials below and run this file once to
// create the required table.

// ── Config ───────────────────────────────────────────────────────
$db_host = getenv('CRYSTALSAVE_DB_HOST') ?: '127.0.0.1';
$db_port = getenv('CRYSTALSAVE_DB_PORT') ?: '3306';
$db_name = getenv('CRYSTALSAVE_DB_NAME') ?: 'game';
$db_user = getenv('CRYSTALSAVE_DB_USER') ?: 'unityuser';
$db_pass = getenv('CRYSTALSAVE_DB_PASSWORD') !== false ? getenv('CRYSTALSAVE_DB_PASSWORD') : 'ChangeMe!';
$table   = 'CrystalSaveData';

// ── Connect and create table ─────────────────────────────────────
try {
    $pdo = new PDO(
        "mysql:host=$db_host;port=$db_port;dbname=$db_name;charset=utf8mb4",
        $db_user, $db_pass,
        [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION]
    );

    $sql = "CREATE TABLE IF NOT EXISTS `$table` (
        UserID             VARCHAR(64) NOT NULL,
        Slot               INT         NOT NULL,
        Data               MEDIUMBLOB  NOT NULL,
        SlotName           VARCHAR(64),
        LastSavedTicks     BIGINT,
        LastActiveScene    VARCHAR(64),
        ScreenshotFileName VARCHAR(255),
        CustomMetadata     JSON,
        PRIMARY KEY (UserID, Slot)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

    $pdo->exec($sql);
    $pdo->exec("CREATE TABLE IF NOT EXISTS CrystalSaveUsers (
        Username VARCHAR(64) NOT NULL PRIMARY KEY,
        PasswordHash VARCHAR(255) NOT NULL
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");
    echo 'Tables created or already exist.';
} catch (PDOException $e) {
    http_response_code(500);
    echo 'Installation failed: ' . $e->getMessage();
}
