// ============================================================
// Purfle IdentityHub — Admin backup/restore
// ============================================================

async function renderAdminBackup() {
    if (!API.isAdmin()) {
        renderAdminLogin(() => renderAdminBackup());
        return;
    }

    Router.root.innerHTML = `
        <div class="container" style="padding-top: 32px;">
            <h1 style="font-size: 24px; margin-bottom: 8px;">Backup &amp; Restore</h1>
            <nav style="margin-bottom: 32px; display: flex; gap: 16px; font-size: 14px;">
                <a href="/admin">Dashboard</a>
                <a href="/admin/agents">Agents</a>
                <a href="/admin/publishers">Publishers</a>
                <a href="/admin/keys">Keys</a>
                <a href="/admin/attestations">Attestations</a>
                <a href="/admin/backup" class="active">Backup</a>
                <a href="#" onclick="event.preventDefault(); API.setAdminToken(''); Router.navigate('/admin');">Logout</a>
            </nav>

            <div class="admin-stats" id="backup-stats">
                <div class="admin-stat"><div class="value" id="bs-last">—</div><div class="label">Last Backup</div></div>
                <div class="admin-stat"><div class="value" id="bs-size">—</div><div class="label">Size</div></div>
                <div class="admin-stat"><div class="value" id="bs-azure-count" style="color: var(--blue);">—</div><div class="label">Azure Backups</div></div>
            </div>

            <div class="detail-section" style="margin-bottom: 24px;">
                <h2>Actions</h2>
                <div style="display: flex; gap: 12px; flex-wrap: wrap; margin-top: 12px;">
                    <button class="btn btn-primary" id="btn-download">Download Local Backup</button>
                    <button class="btn btn-primary" id="btn-push-azure">Push to Azure</button>
                    <button class="btn btn-primary" id="btn-pull-azure">Pull from Azure</button>
                    <button class="btn" id="btn-restore" style="background: var(--red); color: #fff;">Restore</button>
                </div>
                <p id="backup-status" style="font-size: 13px; margin-top: 12px; color: var(--text-secondary);"></p>
            </div>

            <div class="detail-section" id="restore-section" style="display: none; margin-bottom: 24px;">
                <h2>Restore from File</h2>
                <p style="font-size: 13px; color: var(--text-secondary); margin-bottom: 12px;">
                    Upload a backup zip file and type <strong>RESTORE</strong> to confirm.
                </p>
                <input type="file" id="restore-file" accept=".zip" style="margin-bottom: 8px;" />
                <input type="text" id="restore-confirm" placeholder="Type RESTORE to confirm" style="margin-bottom: 8px; width: 260px;" />
                <button class="btn" id="btn-restore-confirm" style="background: var(--red); color: #fff;">Confirm Restore</button>
            </div>

            <div class="detail-section" id="azure-list-section" style="display: none; margin-bottom: 24px;">
                <h2>Azure Backups</h2>
                <table style="width: 100%; font-size: 13px;">
                    <thead><tr><th>Name</th><th>Size</th><th>Created</th><th></th></tr></thead>
                    <tbody id="azure-list-body"></tbody>
                </table>
            </div>

            <div class="detail-section">
                <h2>Backup History (Azure)</h2>
                <table style="width: 100%; font-size: 13px;">
                    <thead><tr><th>Name</th><th>Size</th><th>Created</th><th></th></tr></thead>
                    <tbody id="backup-history"></tbody>
                </table>
            </div>
        </div>
    `;

    document.getElementById('btn-download').addEventListener('click', downloadBackup);
    document.getElementById('btn-push-azure').addEventListener('click', pushToAzure);
    document.getElementById('btn-pull-azure').addEventListener('click', showAzureList);
    document.getElementById('btn-restore').addEventListener('click', showRestoreSection);
    document.getElementById('btn-restore-confirm').addEventListener('click', doRestore);

    loadBackupHistory();
}

async function downloadBackup() {
    setStatus('Creating backup...');
    try {
        const resp = await fetch('/api/admin/backup/download', {
            headers: { 'Authorization': `Bearer ${API.adminToken}` }
        });
        if (!resp.ok) throw new Error(`Failed: ${resp.status}`);
        const blob = await resp.blob();
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `identityhub-backup-${new Date().toISOString().slice(0, 10)}.zip`;
        a.click();
        URL.revokeObjectURL(url);
        setStatus('Backup downloaded.');
    } catch (e) {
        setStatus(`Error: ${e.message}`);
    }
}

async function pushToAzure() {
    setStatus('Pushing backup to Azure...');
    try {
        await API.adminPost('/api/admin/backup/push-azure', {});
        setStatus('Backup pushed to Azure.');
        loadBackupHistory();
    } catch (e) {
        setStatus(`Error: ${e.message}`);
    }
}

function showRestoreSection() {
    document.getElementById('restore-section').style.display = 'block';
}

async function doRestore() {
    const fileInput = document.getElementById('restore-file');
    const confirm = document.getElementById('restore-confirm').value.trim();
    if (confirm !== 'RESTORE') {
        setStatus('Type RESTORE to confirm.');
        return;
    }
    if (!fileInput.files.length) {
        setStatus('Select a backup file.');
        return;
    }
    setStatus('Restoring...');
    try {
        const formData = new FormData();
        formData.append('file', fileInput.files[0]);
        const resp = await fetch('/api/admin/backup/restore', {
            method: 'POST',
            headers: { 'Authorization': `Bearer ${API.adminToken}` },
            body: formData
        });
        if (!resp.ok) throw new Error(`Failed: ${resp.status}`);
        setStatus('Restore complete. Reload the page to see updated data.');
        document.getElementById('restore-section').style.display = 'none';
    } catch (e) {
        setStatus(`Error: ${e.message}`);
    }
}

async function showAzureList() {
    const section = document.getElementById('azure-list-section');
    section.style.display = 'block';
    const tbody = document.getElementById('azure-list-body');
    tbody.innerHTML = '<tr><td colspan="4">Loading...</td></tr>';
    try {
        const backups = await API.adminGet('/api/admin/backup/azure');
        if (!Array.isArray(backups) || backups.length === 0) {
            tbody.innerHTML = '<tr><td colspan="4" style="color: var(--text-muted);">No Azure backups found</td></tr>';
            return;
        }
        tbody.innerHTML = backups.map(b => `
            <tr>
                <td>${escapeHtml(b.name)}</td>
                <td>${formatBytes(b.size)}</td>
                <td>${formatDateTime(b.createdAt)}</td>
                <td><a href="/api/admin/backup/azure/${encodeURIComponent(b.name)}" data-external>Download</a></td>
            </tr>
        `).join('');
    } catch (e) {
        tbody.innerHTML = `<tr><td colspan="4" style="color: var(--red);">${escapeHtml(e.message)}</td></tr>`;
    }
}

async function loadBackupHistory() {
    const tbody = document.getElementById('backup-history');
    const countEl = document.getElementById('bs-azure-count');
    try {
        const backups = await API.adminGet('/api/admin/backup/azure');
        if (!Array.isArray(backups) || backups.length === 0) {
            tbody.innerHTML = '<tr><td colspan="4" style="color: var(--text-muted);">No backups yet</td></tr>';
            countEl.textContent = '0';
            return;
        }
        countEl.textContent = backups.length;
        if (backups.length > 0) {
            document.getElementById('bs-last').textContent = formatDateTime(backups[0].createdAt);
            document.getElementById('bs-size').textContent = formatBytes(backups[0].size);
        }
        tbody.innerHTML = backups.map(b => `
            <tr>
                <td>${escapeHtml(b.name)}</td>
                <td>${formatBytes(b.size)}</td>
                <td>${formatDateTime(b.createdAt)}</td>
                <td><a href="/api/admin/backup/azure/${encodeURIComponent(b.name)}" data-external>Download</a></td>
            </tr>
        `).join('');
    } catch {
        tbody.innerHTML = '<tr><td colspan="4" style="color: var(--text-muted);">Could not load Azure backups</td></tr>';
        countEl.textContent = '—';
    }
}

function formatBytes(bytes) {
    if (!bytes || bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return (bytes / Math.pow(k, i)).toFixed(1) + ' ' + sizes[i];
}

function setStatus(msg) {
    const el = document.getElementById('backup-status');
    if (el) el.textContent = msg;
}
