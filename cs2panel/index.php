<?php
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

$lang = getUserLanguage();// ALTOMATIC

//$lang = json_decode(file_get_contents(__DIR__ . "/lang/en.json"), true);// EUA
//$lang = json_decode(file_get_contents(__DIR__ . "/lang/br.json"), true);// Brazil

?>


<!DOCTYPE html>
<html lang="<?= $lang['lang_code'] ?>">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <link rel="icon" href="./favicon.png" type="image/png" />
  <title><?= $lang['title'] ?></title>

  <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700&display=swap" rel="stylesheet" />
  <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css" />
  <link rel="stylesheet" href="style.css" />
  <script>
  const lang = <?= json_encode($lang, JSON_UNESCAPED_UNICODE) ?>;
</script>

</head>
<body>
  <div class="container">
    <div class="header">
      <h1>
        <img src="tdmueatdmueatdmu.png" alt="Counter-Strike 2" style="width: 50px;" />
        <?= $lang['panel_title'] ?>
      </h1>
    </div>

    <div class="tab-container">
      <button class="tab-button active" data-tab="admins"><i class="fas fa-user-shield icon-small"></i><?= $lang['tab_admins'] ?></button>
      <button class="tab-button" data-tab="bans"><i class="fas fa-gavel icon-small"></i><?= $lang['tab_bans'] ?></button>
      <button class="tab-button" data-tab="ip_bans"><i class="fas fa-network-wired icon-small"></i><?= $lang['tab_ip_bans'] ?></button>
      <button class="tab-button" data-tab="mutes"><i class="fas fa-volume-mute icon-small"></i><?= $lang['tab_mutes'] ?></button>
    </div>

    <div id="admins" class="content-section active">
      <h2><?= $lang['section_admins'] ?></h2>
      <table>
        <thead>
          <tr>
            <th><?= $lang['col_name'] ?></th>
            <th>SteamID</th>
            <th><?= $lang['col_permission'] ?></th>
            <th><?= $lang['col_level'] ?></th>
            <th><?= $lang['col_date'] ?></th>
          </tr>
        </thead>
        <tbody id="admins-list"></tbody>
      </table>
      <div id="admins-pagination" class="pagination"></div>
    </div>

    <div id="bans" class="content-section">
      <h2><?= $lang['section_bans'] ?></h2>
      <table>
        <thead>
          <tr>
            <th><?= $lang['col_status'] ?></th>
            <th>SteamID</th>
            <th><?= $lang['col_reason'] ?></th>
            <th><?= $lang['col_date'] ?></th>
          </tr>
        </thead>
        <tbody id="bans-list"></tbody>
      </table>
      <div id="bans-pagination" class="pagination"></div>
    </div>

    <div id="ip_bans" class="content-section">
      <h2><?= $lang['section_ip_bans'] ?></h2>
      <table>
        <thead>
          <tr>
            <th><?= $lang['col_status'] ?></th>
            <th><?= $lang['col_ip'] ?></th>
            <th><?= $lang['col_reason'] ?></th>
            <th><?= $lang['col_date'] ?></th>
          </tr>
        </thead>
        <tbody id="ip-bans-list"></tbody>
      </table>
      <div id="ip-bans-pagination" class="pagination"></div>
    </div>

    <div id="mutes" class="content-section">
      <h2><?= $lang['section_mutes'] ?></h2>
      <table>
        <thead>
          <tr>
            <th><?= $lang['col_status'] ?></th>
            <th>SteamID</th>
            <th><?= $lang['col_reason'] ?></th>
            <th><?= $lang['col_date'] ?></th>
          </tr>
        </thead>
        <tbody id="mutes-list"></tbody>
      </table>
      <div id="mutes-pagination" class="pagination"></div>
    </div>

    <footer style="margin-top: 40px; font-size: 0.85em; color: #666;">
      <?= $lang['footer'] ?>
    </footer>
  </div>

  <script src="AjaxJquery.js"></script>
</body>
</html>