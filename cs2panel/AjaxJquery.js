document.addEventListener('DOMContentLoaded', () => {
    const adminList = document.getElementById('admins-list');
    const bansList = document.getElementById('bans-list');
    const ipBansList = document.getElementById('ip-bans-list');
    const mutesList = document.getElementById('mutes-list');
    const adminPagination = document.getElementById('admins-pagination');
    const bansPagination = document.getElementById('bans-pagination');
    const ipBansPagination = document.getElementById('ip-bans-pagination');
    const mutesPagination = document.getElementById('mutes-pagination');
    const tabs = document.querySelectorAll('.tab-button');
    const dataOutput = document.getElementById('data-output'); // opcional para debug

    const ITEMS_PER_PAGE = 10;

    const fetchData = async (type, page = 1) => {
        const url = `api.php?type=${type}&page=${page}`;
        try {
            const response = await fetch(url);
            if (!response.ok) throw new Error(`Erro de rede: ${response.status}`);
            const data = await response.json();
            if (data.error) throw new Error(`Erro do servidor: ${data.error}`);
            if (dataOutput) {
                dataOutput.innerHTML = `<pre>${JSON.stringify(data, null, 2)}</pre>`;
            }
            return data;
        } catch (error) {
            console.error('Erro ao buscar dados:', error);
            if (dataOutput) {
                dataOutput.innerHTML = `<div class="log-entry error"><strong>Erro:</strong><pre>${error.message}</pre></div>`;
            }
            return { data: [], total_count: 0 };
        }
    };

    const renderTable = (data, listElement, paginationElement, type, currentPage, totalCount) => {
        listElement.innerHTML = '';
        if (data.length === 0) {
            const row = document.createElement('tr');
            const colspan = type === 'admins' ? 5 : 4;
            row.innerHTML = `<td colspan="${colspan}" style="text-align:center;">Nenhum registro encontrado.</td>`;
            listElement.appendChild(row);
        } else {
            data.forEach(item => {
                const row = document.createElement('tr');
                let innerHTML = '';

                if (type === 'admins') {
                    innerHTML = `
                        <td><i class="fas fa-user-shield icon-small"></i>${item.name}</td>
                        <td>${item.steamid}</td>
                        <td>${item.permission}</td>
                        <td>${item.level}</td>
                        <td>${item.granted_at}</td>
                    `;
                } else if (type === 'bans') {
                    const statusText = item.unbanned ? lang.status_unbanned : lang.status_banned;
                    const statusIcon = item.unbanned
                        ? '<i class="fas fa-check-circle icon-unban"></i>'
                        : '<i class="fas fa-ban icon-ban"></i>';
                    innerHTML = `
                        <td class="status-cell">${statusIcon} ${statusText}</td>
                        <td>${item.steamid}</td>
                        <td>${item.reason}</td>
                        <td>${item.timestamp}</td>
                    `;
                } else if (type === 'ip_bans') {
                    const statusText = item.unbanned ? lang.status_unbanned : lang.status_banned;
                    const statusIcon = item.unbanned
                        ? '<i class="fas fa-check-circle icon-unban"></i>'
                        : '<i class="fas fa-ban icon-ban"></i>';
                    innerHTML = `
                        <td class="status-cell">${statusIcon} ${statusText}</td>
                        <td>${item.ip_address}</td>
                        <td>${item.reason}</td>
                        <td>${item.timestamp}</td>
                    `;
                } else if (type === 'mutes') {
                    const statusText = item.unmuted ? lang.status_unmuted : lang.status_muted;
                    const statusIcon = item.unmuted
                        ? '<i class="fas fa-check-circle icon-unban"></i>'
                        : '<i class="fas fa-volume-mute icon-ban"></i>';
                    innerHTML = `
                        <td class="status-cell">${statusIcon} ${statusText}</td>
                        <td>${item.steamid}</td>
                        <td>${item.reason}</td>
                        <td>${item.timestamp}</td>
                    `;
                }

                row.innerHTML = innerHTML;
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
            if (i === currentPage) link.classList.add('active');

            link.addEventListener('click', async (e) => {
                e.preventDefault();
                const data = await fetchData(type, i);
                let listTarget, paginationTarget;
                if (type === 'admins') {
                    listTarget = adminList;
                    paginationTarget = adminPagination;
                } else if (type === 'bans') {
                    listTarget = bansList;
                    paginationTarget = bansPagination;
                } else if (type === 'ip_bans') {
                    listTarget = ipBansList;
                    paginationTarget = ipBansPagination;
                } else if (type === 'mutes') {
                    listTarget = mutesList;
                    paginationTarget = mutesPagination;
                }
                renderTable(data.data, listTarget, paginationTarget, type, i, data.total_count);
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
            let listTarget, paginationTarget;
            if (targetTab === 'admins') {
                listTarget = adminList;
                paginationTarget = adminPagination;
            } else if (targetTab === 'bans') {
                listTarget = bansList;
                paginationTarget = bansPagination;
            } else if (targetTab === 'ip_bans') {
                listTarget = ipBansList;
                paginationTarget = ipBansPagination;
            } else if (targetTab === 'mutes') {
                listTarget = mutesList;
                paginationTarget = mutesPagination;
            }
            renderTable(data.data, listTarget, paginationTarget, targetTab, 1, data.total_count);
        });
    });

    const activeTab = document.querySelector('.tab-button.active').getAttribute('data-tab');
    fetchData(activeTab, 1).then(data => {
        const listElement = document.getElementById(activeTab + '-list');
        const paginationElement = document.getElementById(activeTab + '-pagination');
        renderTable(data.data, listElement, paginationElement, activeTab, 1, data.total_count);
    });
});

