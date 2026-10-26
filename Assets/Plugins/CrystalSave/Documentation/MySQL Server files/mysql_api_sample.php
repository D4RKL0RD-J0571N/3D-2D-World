<?php
// ── CORS ─────────────────────────────────────────────────────────
header('Access-Control-Allow-Origin: *');  // change * to your domain in prod
header('Access-Control-Allow-Headers: Content-Type, X-API-KEY');
header('Access-Control-Allow-Methods: POST, GET, OPTIONS');
header('Access-Control-Expose-Headers: Content-Length');

if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') {
    // Pre-flight probe – no body, just confirm the CORS rules
    http_response_code(204);  // No Content
    exit;
}

// ── Config ───────────────────────────────────────────────────────
$db_host = getenv('CRYSTALSAVE_DB_HOST') ?: '127.0.0.1';
$db_port = getenv('CRYSTALSAVE_DB_PORT') ?: '3306';
$db_name = getenv('CRYSTALSAVE_DB_NAME') ?: 'game';
$db_user = getenv('CRYSTALSAVE_DB_USER') ?: 'unityuser';
$db_pass = getenv('CRYSTALSAVE_DB_PASSWORD') !== false ? getenv('CRYSTALSAVE_DB_PASSWORD') : 'ChangeMe!';
$table   = 'CrystalSaveData';
$userTable = 'CrystalSaveUsers';
$apiKey  = getenv('CRYSTALSAVE_API_KEY') !== false ? getenv('CRYSTALSAVE_API_KEY') : 'SUPER_SECRET_TOKEN';
$imgRoot = getenv('CRYSTALSAVE_IMAGE_ROOT') ?: __DIR__ . '/img';
// This shared-key example is for trusted clients. A public service must authorize
// the authenticated user's uid on every endpoint; login alone does not do that.

// ── Simple API-key gate ─────────────────────────────────────────
if ($apiKey !== '' && ($_SERVER['HTTP_X_API_KEY'] ?? '') !== $apiKey) {
    http_response_code(401);
    exit;
}

// ── DB connect ──────────────────────────────────────────────────
try {
    $pdo = new PDO(
        "mysql:host=$db_host;port=$db_port;dbname=$db_name;charset=utf8mb4",
        $db_user, $db_pass,
        [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION]
    );
} catch (PDOException $e) {
    http_response_code(500);
    echo json_encode(['error' => 'DB connection failed']);
    exit;
}

// ── Routing ─────────────────────────────────────────────────────
// Accept action either as ?action=save or as /save via PATH_INFO
$action = $_GET['action'] ?? trim($_SERVER['PATH_INFO'] ?? parse_url($_SERVER['REQUEST_URI'], PHP_URL_PATH), '/');
$input  = json_decode(file_get_contents('php://input'), true);

function validKey($uid, $slot) {
    return is_string($uid) && $uid !== '' && filter_var($slot, FILTER_VALIDATE_INT) !== false && (int)$slot > 0;
}
function imagePath($root, $uid, $name) {
    // Nested user folders are supported, but no caller may escape the image root.
    $parts = preg_split('~[/\\\\]~', $uid);
    if (array_filter($parts, fn($p) => $p === '' || $p === '.' || $p === '..') || strpos($uid, "\0") !== false)
        return null;
    return $root . '/' . implode('/', $parts) . '/' . basename(str_replace('\\', '/', $name));
}
function wireMetadata($row) {
    $obj = json_decode($row['meta'] ?? '{}', true) ?: [];
    $row['slot'] = (int)$row['slot'];
    $row['ticks'] = (int)($row['ticks'] ?? 0);
    $row['name'] = $row['name'] ?? 'Slot ' . $row['slot'];
    $row['scene'] = $row['scene'] ?? '';
    $row['shot'] = $row['shot'] ?? '';
    $row['meta'] = array_map(fn($k,$v) => ['key'=>(string)$k,'value'=>$v], array_keys($obj), $obj);
    return $row;
}

// ── Upload image  (POST /uploadImage  multipart/form-data) ──────
if ($action === 'uploadImage') {
    // uid = user ID, slot = save slot, name = original filename
    if (!isset($_FILES['shot'], $_POST['uid'], $_POST['slot'], $_POST['name'])) {
        http_response_code(400); exit;
    }
    $uid  = $_POST['uid'];
    $slot = intval($_POST['slot']);
    if (!validKey($uid, $slot)) { http_response_code(400); exit; }
    // Keep the client-supplied name (timestamps etc.) but strip any path
    $targetName = basename($_POST['name']);
    $ext = strtolower(pathinfo($targetName, PATHINFO_EXTENSION));
    if ($ext === '') {
        $ext = strtolower(pathinfo($_FILES['shot']['name'], PATHINFO_EXTENSION)) ?: 'png';
        $targetName .= ".$ext";
    }
    $targetPath = imagePath($imgRoot, $uid, $targetName);
    if ($targetPath === null) { http_response_code(400); exit; }
    $targetDir = dirname($targetPath);

    @mkdir($targetDir, 0750, true);
    if (move_uploaded_file($_FILES['shot']['tmp_name'], $targetPath)) {
        $stmt = $pdo->prepare(
            "UPDATE $table
             SET ScreenshotFileName=?
             WHERE UserID=? AND Slot=?");
        $stmt->execute([$targetName, $uid, $slot]);
        exit; // 200 OK
    }
    http_response_code(500); exit;
}

// ── Read slot metadata  (GET /metadata?uid&slot) ────────────────
if ($action === 'metadata' && $_SERVER['REQUEST_METHOD'] === 'GET') {
    $uid  = $_GET['uid']  ?? '';
    $slot = $_GET['slot'] ?? '';
    if (!validKey($uid, $slot)) { http_response_code(400); exit; }

    $stmt = $pdo->prepare("
        SELECT Slot AS slot,
               COALESCE(SlotName, CONCAT('Slot ', Slot))  AS name,
               COALESCE(LastSavedTicks, 0)                AS ticks,
               COALESCE(LastActiveScene, '')             AS scene,
               COALESCE(ScreenshotFileName, '')          AS shot,
               COALESCE(CustomMetadata , '{}')           AS meta
          FROM $table
         WHERE UserID=? AND Slot=?");
    $stmt->execute([$uid, $slot]);

    if ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
        echo json_encode(wireMetadata($row));
    } else {
        http_response_code(404);
    }
    exit;
}

// ── JSON endpoints ──────────────────────────────────────────────
switch ($action) {

case 'signup': // {username,password}
    $username = $input['username'] ?? '';
    $password = $input['password'] ?? '';
    if ($username === '' || $password === '') { http_response_code(400); break; }
    try {
        $hash = password_hash($password, PASSWORD_DEFAULT);
        $pdo->prepare("INSERT INTO $userTable (Username,PasswordHash) VALUES (?,?)")
            ->execute([$username, $hash]);
    } catch (PDOException $e) {
        http_response_code(409);
    }
    break;

case 'login': // {username,password}
    $username = $input['username'] ?? '';
    $password = $input['password'] ?? '';
    if ($username === '' || $password === '') { http_response_code(400); break; }
    $stmt = $pdo->prepare("SELECT PasswordHash FROM $userTable WHERE Username=?");
    $stmt->execute([$username]);
    $hash = $stmt->fetchColumn();
    if ($hash && password_verify($password, $hash)) {
        echo json_encode(['uid' => $username]);
    } else {
        http_response_code(401);
    }
    break;

case 'save':      // {uid,slot,data}
    $uid  = $input['uid']  ?? '';
    $slot = $input['slot'] ?? '';
    $data = isset($input['data']) && is_string($input['data']) ? base64_decode($input['data'], true) : '';
    if (!validKey($uid, $slot) || $data === '' || $data === false) { http_response_code(400); break; }

    $pdo->prepare("INSERT INTO $table (UserID,Slot,Data)
                   VALUES (?,?,?)
                   ON DUPLICATE KEY UPDATE Data=VALUES(Data)")
        ->execute([$uid,$slot,$data]);
    break;

case 'metadata':  // {uid,slot,name,ticks,scene,shot,meta}
    $uid   = $input['uid']   ?? '';
    $slot  = $input['slot']  ?? '';
    $name  = $input['name']  ?? 'Slot ' . $slot;
    $ticks = $input['ticks'] ?? 0;
    $scene = $input['scene'] ?? '';
    $shot  = $input['shot']  ?? '';
    $meta = $input['meta'] ?? [];
    if (!validKey($uid, $slot) || !is_string($name) || !is_string($scene) || !is_string($shot) ||
        !is_int($ticks) || $ticks < 0 || $ticks > 3155378975999999999 || !is_array($meta) ||
        array_filter($meta, fn($v) => $v !== null && !is_string($v))) { http_response_code(400); break; }
    $metaJson = json_encode((object)$meta);

    // Metadata-only placeholders are valid; an existing payload must remain untouched.
    $pdo->prepare("INSERT INTO $table (UserID,Slot,Data,SlotName,LastSavedTicks,LastActiveScene,ScreenshotFileName,CustomMetadata)
        VALUES (?,?,'',?,?,?,?,?) ON DUPLICATE KEY UPDATE SlotName=VALUES(SlotName),
        LastSavedTicks=VALUES(LastSavedTicks),LastActiveScene=VALUES(LastActiveScene),
        ScreenshotFileName=VALUES(ScreenshotFileName),CustomMetadata=VALUES(CustomMetadata)")
        ->execute([$uid, $slot, $name, $ticks, $scene, $shot, $metaJson]);
    break;

case 'load':      // GET ?uid&slot
    if (!validKey($_GET['uid'] ?? null, $_GET['slot'] ?? null)) { http_response_code(400); break; }
    $stmt = $pdo->prepare(
      "SELECT Data FROM $table WHERE UserID=? AND Slot=?");
    $stmt->execute([$_GET['uid'], $_GET['slot']]);
    if ($row = $stmt->fetch(PDO::FETCH_NUM)) {
        echo base64_encode($row[0]);
    }
    break;

case 'delete':    // {uid,slot}
    $uid  = $input['uid']  ?? '';
    $slot = $input['slot'] ?? '';
    if (!validKey($uid, $slot)) { http_response_code(400); break; }

    // remove image first
    $img = $pdo->prepare(
      "SELECT ScreenshotFileName FROM $table WHERE UserID=? AND Slot=?");
    $img->execute([$uid, $slot]);
    if ($fn = $img->fetchColumn()) {
        $image = imagePath($imgRoot, $uid, $fn);
        if ($image !== null && is_file($image)) unlink($image);
    }
    $pdo->prepare(
      "DELETE FROM $table WHERE UserID=? AND Slot=?")
        ->execute([$uid, $slot]);
    break;

case 'list':      // GET ?uid
    if (!is_string($_GET['uid'] ?? null) || $_GET['uid'] === '') { http_response_code(400); break; }
    $stmt = $pdo->prepare(
      "SELECT Slot AS slot, SlotName AS name, LastSavedTicks AS ticks,
              LastActiveScene AS scene, ScreenshotFileName AS shot,
              CustomMetadata AS meta
         FROM $table
        WHERE UserID=?");
    $stmt->execute([$_GET['uid']]);
    $rows = $stmt->fetchAll(PDO::FETCH_ASSOC);
    echo json_encode(array_map('wireMetadata', $rows));
    break;

default:
    http_response_code(404);
    break;
}
