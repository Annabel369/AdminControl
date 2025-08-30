<!DOCTYPE html>
<html lang="pt-BR">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
	<link rel="icon" href="./favicon.png" type="image/png">
    <title>Painel de Administrador - CS2</title>
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700&display=swap" rel="stylesheet">
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css">
    <link rel="stylesheet" href="style.css">
</head>
<body>
<div class="imagem-container">
    <img src="tdmueatdmueatdmu.png" alt="Counter-Strike 2">
    <p class="imagem-legenda">Counter-Strike 2 - Tela de informacao</p>
</div>
    <div class="container">
        <div class="header">
            <h1>Painel de Administração</h1>
        </div>
        <div class="tab-container">
            <button class="tab-button active" data-tab="admins">
                <i class="fas fa-user-shield icon-small"></i>Admins
            </button>
            <button class="tab-button" data-tab="bans">
                <i class="fas fa-gavel icon-small"></i>Banidos (SteamID)
            </button>
            <button class="tab-button" data-tab="ip-bans">
                <i class="fas fa-network-wired icon-small"></i>Banidos (IP)
            </button>
            <button class="tab-button" data-tab="mutes">
                <i class="fas fa-volume-mute icon-small"></i>Mutados
            </button>
        </div>
        
        <div id="admins" class="content-section active">
            <h2>Lista de Administradores</h2>
            <div class="table-container">
                <table>
                    <thead>
                        <tr>
                            <th>Nome</th>
                            <th>SteamID</th>
                            <th>Permissão</th>
                            <th>Nível de Imunidade</th>
                            <th>Data de Concessão</th>
                        </tr>
                    </thead>
                    <tbody id="admins-list">
                    </tbody>
                </table>
            </div>
            <div id="admins-pagination" class="pagination"></div>
        </div>
        
        <div id="bans" class="content-section">
            <h2>Lista de Banidos e Desbanidos (SteamID)</h2>
            <div class="table-container">
                <table>
                    <thead>
                        <tr>
                            <th>Status</th>
                            <th>SteamID</th>
                            <th>Motivo</th>
                            <th>Data do Ban/Desban</th>
                        </tr>
                    </thead>
                    <tbody id="bans-list">
                    </tbody>
                </table>
            </div>
            <div id="bans-pagination" class="pagination"></div>
        </div>
        
        <div id="ip-bans" class="content-section">
            <h2>Lista de Banidos e Desbanidos (IP)</h2>
            <div class="table-container">
                <table>
                    <thead>
                        <tr>
                            <th>Status</th>
                            <th>Endereço IP</th>
                            <th>Motivo</th>
                            <th>Data do Ban/Desban</th>
                        </tr>
                    </thead>
                    <tbody id="ip-bans-list">
                    </tbody>
                </table>
            </div>
            <div id="ip-bans-pagination" class="pagination"></div>
        </div>

        <div id="mutes" class="content-section">
            <h2>Lista de Mutados e Desmutados</h2>
            <div class="table-container">
                <table>
                    <thead>
                        <tr>
                            <th>Status</th>
                            <th>SteamID</th>
                            <th>Motivo</th>
                            <th>Data do Mute/Desmute</th>
                        </tr>
                    </thead>
                    <tbody id="mutes-list">
                    </tbody>
                </table>
            </div>
            <div id="mutes-pagination" class="pagination"></div>
        </div>

        <footer style="margin-top: 40px; font-size: 0.85em; color: #666;">
            © 2025 — Criado por Amauri Bueno dos Santos com apoio da Copilot. Código limpo, servidor afiado.
        </footer>
    </div>
    
    <script src="AjaxJquery.js"></script>
</body>
</html>