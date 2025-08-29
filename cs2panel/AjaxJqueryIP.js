 document.addEventListener('DOMContentLoaded', async () => {
            const dataOutput = document.getElementById('data-output');
            const ipBansList = document.getElementById('ip-bans-list');
            const url = `debug2_api.php`;

            try {
                const response = await fetch(url);
                const data = await response.json();

                // Exibe a resposta completa da API
                dataOutput.innerHTML = `<div class="log-entry success"><strong>Resposta JSON da API:</strong><pre>${JSON.stringify(data, null, 2)}</pre></div>`;
                console.log('Dados da API recebidos:', data);

                // Renderiza os dados na tabela
                ipBansList.innerHTML = '';
                if (data.data.length > 0) {
                    data.data.forEach(item => {
                        const row = document.createElement('tr');
                        const statusText = item.unbanned ? 'Desbanido' : 'Banido';
                        row.innerHTML = `
                            <td>${item.ip_address}</td>
                            <td>${item.reason}</td>
                            <td>${statusText}</td>
                            <td>${item.timestamp}</td>
                        `;
                        ipBansList.appendChild(row);
                    });
                } else {
                    const row = document.createElement('tr');
                    row.innerHTML = `<td colspan="4" style="text-align:center;">Nenhum registro encontrado.</td>`;
                    ipBansList.appendChild(row);
                }

            } catch (error) {
                dataOutput.innerHTML = `<div class="log-entry error"><strong>Erro na requisição:</strong><pre>${error.message}</pre></div>`;
                console.error('Falha ao buscar dados da API:', error);
            }
        });