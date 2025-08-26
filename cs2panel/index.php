<!DOCTYPE html>
<html lang="pt-BR">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Painel de Administrador - CS2</title>
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700&display=swap" rel="stylesheet">
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css">
    <style>
        :root {
            --bg-dark: #121212;
            --bg-card: #1e1e1e;
            --text-light: #f0f0f0;
            --accent-purple: #8e2de2;
            --accent-pink: #4a00e0;
            --ban-red: #ff4d4f;
            --unban-green: #52c41a;
        }

        body {
            font-family: 'Inter', sans-serif;
            background-color: var(--bg-dark);
            color: var(--text-light);
            margin: 0;
            padding: 0;
            min-height: 100vh;
            display: flex;
            justify-content: center;
            align-items: flex-start;
        }

        .container {
            width: 95%;
            max-width: 1200px;
            margin-top: 2rem;
            background-color: var(--bg-card);
            border-radius: 12px;
            box-shadow: 0 8px 32px 0 rgba(0, 0, 0, 0.37);
            padding: 2rem;
        }

        .header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding-bottom: 1rem;
            border-bottom: 1px solid #333;
        }

        h1 {
            background: linear-gradient(45deg, var(--accent-purple), var(--accent-pink));
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            font-weight: 700;
            font-size: 2.5rem;
            margin: 0;
        }

        .tab-container {
            display: flex;
            justify-content: flex-start;
            margin-top: 1rem;
            border-bottom: 1px solid #333;
        }

        .tab-button {
            background: none;
            border: none;
            color: var(--text-light);
            font-size: 1rem;
            font-weight: 600;
            padding: 0.75rem 1.5rem;
            cursor: pointer;
            transition: all 0.3s ease;
            position: relative;
        }

        .tab-button::after {
            content: '';
            position: absolute;
            left: 0;
            bottom: -1px;
            width: 100%;
            height: 2px;
            background: linear-gradient(90deg, var(--accent-purple), var(--accent-pink));
            transform: scaleX(0);
            transform-origin: bottom right;
            transition: transform 0.3s ease-out;
        }

        .tab-button.active::after {
            transform: scaleX(1);
            transform-origin: bottom left;
        }
        
        .tab-button:hover:not(.active) {
            color: #ccc;
        }

        .content-section {
            display: none;
            margin-top: 2rem;
        }

        .content-section.active {
            display: block;
        }

        table {
            width: 100%;
            border-collapse: collapse;
            margin-top: 1.5rem;
        }

        th, td {
            padding: 1rem;
            text-align: left;
            border-bottom: 1px solid #333;
        }

        thead {
            background-color: #2a2a2a;
        }

        tbody tr:nth-child(even) {
            background-color: #232323;
        }

        .status-cell {
            font-weight: bold;
        }

        .ban-status {
            color: var(--ban-red);
        }

        .unban-status {
            color: var(--unban-green);
        }

        .icon-small {
            font-size: 0.8em;
            margin-right: 0.5rem;
            background: linear-gradient(45deg, var(--accent-purple), var(--accent-pink));
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
        }
        
        .icon-ban { color: var(--ban-red); }
        .icon-unban { color: var(--unban-green); }

        .pagination {
            display: flex;
            justify-content: center;
            align-items: center;
            margin-top: 2rem;
            gap: 0.5rem;
        }

        .pagination-link {
            background-color: #2a2a2a;
            border: 1px solid #444;
            color: var(--text-light);
            padding: 0.5rem 1rem;
            text-decoration: none;
            border-radius: 6px;
            transition: all 0.2s ease;
        }

        .pagination-link:hover {
            background-color: #3a3a3a;
            transform: translateY(-2px);
        }

        .pagination-link.active {
            background: linear-gradient(45deg, var(--accent-purple), var(--accent-pink));
            color: #fff;
            border-color: transparent;
            font-weight: 700;
        }

    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <h1>Painel de Administração</h1>
        </div>
        <div class="tab-container">
            <button class="tab-button active" data-tab="admins">
                <i class="fas fa-user-shield icon-small"></i>Admins
            </button>
            <button class="tab-button" data-tab="bans">
                <i class="fas fa-gavel icon-small"></i>Banidos e Desbanidos
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
                        <!-- Conteúdo da tabela de admins será inserido aqui pelo JavaScript -->
                    </tbody>
                </table>
            </div>
            <div id="admins-pagination" class="pagination"></div>
        </div>
        
        <div id="bans" class="content-section">
            <h2>Lista de Banidos e Desbanidos</h2>
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
                        <!-- Conteúdo da tabela de bans será inserido aqui pelo JavaScript -->
                    </tbody>
                </table>
            </div>
            <div id="bans-pagination" class="pagination"></div>
        </div>
    </div>

    <script>
        document.addEventListener('DOMContentLoaded', () => {
            const adminPanel = document.getElementById('admins');
            const bansPanel = document.getElementById('bans');
            const adminList = document.getElementById('admins-list');
            const bansList = document.getElementById('bans-list');
            const adminPagination = document.getElementById('admins-pagination');
            const bansPagination = document.getElementById('bans-pagination');
            const tabs = document.querySelectorAll('.tab-button');

            const ITEMS_PER_PAGE = 10;

            const fetchData = async (type, page = 1) => {
                const url = `api.php?type=${type}&page=${page}`;
                try {
                    const response = await fetch(url);
                    if (!response.ok) {
                        throw new Error(`Erro de rede: ${response.status}`);
                    }
                    const data = await response.json();
                    if (data.error) {
                         throw new Error(`Erro do servidor: ${data.error}`);
                    }
                    return data;
                } catch (error) {
                    console.error('Erro ao buscar dados:', error);
                    // Retorna um objeto vazio para evitar que a aplicação quebre
                    return { data: [], total_count: 0 };
                }
            };

            const renderTable = (data, listElement, paginationElement, type, currentPage, totalCount) => {
                listElement.innerHTML = '';
                if (data.length === 0) {
                     const row = document.createElement('tr');
                     row.innerHTML = `<td colspan="5" style="text-align:center;">Nenhum registro encontrado.</td>`;
                     listElement.appendChild(row);
                } else {
                    data.forEach(item => {
                        const row = document.createElement('tr');
                        if (type === 'bans') {
                            const statusClass = item.unbanned ? 'unban-status' : 'ban-status';
                            const statusText = item.unbanned ? 'Desbanido' : 'Banido';
                            const statusIcon = item.unbanned ? '<i class="fas fa-check-circle icon-unban"></i>' : '<i class="fas fa-ban icon-ban"></i>';
                            row.innerHTML = `
                                <td class="status-cell">${statusIcon} ${statusText}</td>
                                <td>${item.steamid}</td>
                                <td>${item.reason}</td>
                                <td>${item.timestamp}</td>
                            `;
                        } else if (type === 'admins') {
                            row.innerHTML = `
                                <td><i class="fas fa-user-shield icon-small"></i>${item.name}</td>
                                <td>${item.steamid}</td>
                                <td>${item.permission}</td>
                                <td>${item.level}</td>
                                <td>${item.granted_at}</td>
                            `;
                        }
                        listElement.appendChild(row);
                    });
                }

                renderPagination(totalCount, paginationElement, type, currentPage);
            };

            const renderPagination = (totalItems, paginationElement, type, currentPage) => {
                const totalPages = Math.ceil(totalItems / ITEMS_PER_PAGE);
                paginationElement.innerHTML = '';

                for (let i = 1; i <= totalPages; i++) {
                    const link = document.createElement('a');
                    link.href = '#';
                    link.textContent = i;
                    link.classList.add('pagination-link');
                    if (i === currentPage) {
                        link.classList.add('active');
                    }
                    link.addEventListener('click', async (e) => {
                        e.preventDefault();
                        const data = await fetchData(type, i);
                        renderTable(data.data, type === 'admins' ? adminList : bansList, paginationElement, type, i, data.total_count);
                    });
                    paginationElement.appendChild(link);
                }
            };
            
            tabs.forEach(tab => {
                tab.addEventListener('click', async (e) => {
                    tabs.forEach(t => t.classList.remove('active'));
                    e.target.classList.add('active');
                    
                    document.querySelectorAll('.content-section').forEach(section => {
                        section.classList.remove('active');
                    });
                    
                    const targetTab = e.target.getAttribute('data-tab');
                    document.getElementById(targetTab).classList.add('active');
                    
                    const data = await fetchData(targetTab, 1);
                    if (targetTab === 'admins') {
                        renderTable(data.data, adminList, adminPagination, 'admins', 1, data.total_count);
                    } else if (targetTab === 'bans') {
                        renderTable(data.data, bansList, bansPagination, 'bans', 1, data.total_count);
                    }
                });
            });

            fetchData('admins', 1).then(data => {
                renderTable(data.data, adminList, adminPagination, 'admins', 1, data.total_count);
            });
        });
    </script>
</body>
</html>