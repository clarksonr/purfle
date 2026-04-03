// ============================================================
// Purfle IdentityHub — Admin key registry management
// ============================================================

async function renderAdminKeys() {
    if (!API.isAdmin()) {
        renderAdminLogin(() => renderAdminKeys());
        return;
    }

    Router.root.innerHTML = `
        <div class="container" style="padding-top: 32px;">
            <h1 style="font-size: 24px; margin-bottom: 8px;">Key Registry</h1>
            <nav style="margin-bottom: 32px; display: flex; gap: 16px; font-size: 14px;">
                <a href="/admin">Dashboard</a>
                <a href="/admin/agents">Agents</a>
                <a href="/admin/publishers">Publishers</a>
                <a href="/admin/keys" class="active">Keys</a>
                <a href="/admin/attestations">Attestations</a>
            </nav>

            <div style="margin-bottom: 24px;">
                <button class="btn btn-primary" onclick="toggleRegisterKeyForm()">Register New Key</button>
            </div>

            <div id="register-key-form" style="display: none; margin-bottom: 24px; padding: 20px; background: var(--bg-card); border: 1px solid var(--border); border-radius: var(--radius);">
                <h3 style="margin-bottom: 16px;">Register New Key</h3>
                <div class="form-group">
                    <label>Key ID</label>
                    <input type="text" id="reg-key-id" placeholder="e.g., com.example/release-2026" />
                </div>
                <div class="form-group">
                    <label>Owner</label>
                    <input type="text" id="reg-key-owner" placeholder="Owner name" />
                </div>
                <div class="form-group">
                    <label>Public Key PEM</label>
                    <textarea id="reg-key-pem" placeholder="Paste EC P-256 public key PEM..." style="font-family: var(--mono); font-size: 12px; min-height: 120px;"></textarea>
                </div>
                <div style="display: flex; gap: 8px;">
                    <button class="btn btn-primary" onclick="registerKey()">Register</button>
                    <button class="btn" onclick="toggleRegisterKeyForm()">Cancel</button>
                </div>
                <p id="reg-key-error" style="color: var(--red); font-size: 13px; margin-top: 8px; display: none;"></p>
            </div>

            <table class="data-table">
                <thead>
                    <tr>
                        <th>Key ID</th>
                        <th>Algorithm</th>
                        <th>Status</th>
                        <th>Actions</th>
                    </tr>
                </thead>
                <tbody id="admin-keys-body">
                    <tr><td colspan="4" style="text-align: center; color: var(--text-muted);">Loading...</td></tr>
                </tbody>
            </table>
        </div>
    `;

    loadAdminKeys();
}

function toggleRegisterKeyForm() {
    const form = document.getElementById('register-key-form');
    form.style.display = form.style.display === 'none' ? 'block' : 'none';
}

async function registerKey() {
    const keyId = document.getElementById('reg-key-id').value.trim();
    const errEl = document.getElementById('reg-key-error');
    errEl.style.display = 'none';

    if (!keyId) {
        errEl.textContent = 'Key ID is required.';
        errEl.style.display = 'block';
        return;
    }

    try {
        await API.adminPost('/api/admin/keys', { keyId });
        toggleRegisterKeyForm();
        loadAdminKeys();
    } catch (e) {
        errEl.textContent = 'Registration failed: ' + e.message;
        errEl.style.display = 'block';
    }
}

async function loadAdminKeys() {
    const tbody = document.getElementById('admin-keys-body');

    // We don't have a "list all keys" endpoint that returns full data,
    // so we'll show known keys from agents
    try {
        const data = await API.get('/api/agents?pageSize=100');
        const agents = data.agents || [];

        // Extract unique key IDs from agent data (if available)
        const keySet = new Map();
        agents.forEach(a => {
            if (a.keyId && !keySet.has(a.keyId)) {
                keySet.set(a.keyId, { keyId: a.keyId, algorithm: 'ES256', isRevoked: false });
            }
        });

        // Also try to load from hub
        try {
            const hubData = await API.get('/api/hub/agents?pageSize=100');
            const items = Array.isArray(hubData) ? hubData : (hubData.items || []);
            items.forEach(e => {
                if (e.keyId && !keySet.has(e.keyId)) {
                    keySet.set(e.keyId, { keyId: e.keyId, algorithm: 'ES256', isRevoked: false });
                }
            });
        } catch { }

        const keys = [...keySet.values()];

        // Check revocation status for each key
        for (const k of keys) {
            try {
                const status = await API.get(`/api/hub/keys/${encodeURIComponent(k.keyId)}`);
                k.isRevoked = status.isRevoked || false;
            } catch { }
        }

        if (keys.length === 0) {
            tbody.innerHTML = '<tr><td colspan="4" style="text-align: center; color: var(--text-muted);">No keys found. Register a key or publish an agent.</td></tr>';
            return;
        }

        tbody.innerHTML = keys.map(k => `
            <tr>
                <td><a href="/keys/${escapeHtml(k.keyId)}">${escapeHtml(k.keyId)}</a></td>
                <td>${escapeHtml(k.algorithm)}</td>
                <td>${k.isRevoked
                    ? '<span class="badge badge-revoked">Revoked</span>'
                    : '<span class="badge badge-verified">Active</span>'}</td>
                <td>
                    ${!k.isRevoked
                        ? `<button class="btn btn-sm btn-danger" onclick="revokeKey('${escapeHtml(k.keyId)}')">Revoke</button>`
                        : '<span style="color: var(--text-muted); font-size: 12px;">Revoked</span>'}
                </td>
            </tr>
        `).join('');
    } catch (e) {
        tbody.innerHTML = `<tr><td colspan="4" style="color: var(--red);">${escapeHtml(e.message)}</td></tr>`;
    }
}

async function revokeKey(keyId) {
    const reason = prompt('Reason for revoking this key:');
    if (reason === null) return;
    try {
        await API.adminDelete(`/api/admin/keys/${encodeURIComponent(keyId)}?reason=${encodeURIComponent(reason || 'Revoked by admin')}`);
        loadAdminKeys();
    } catch (e) {
        alert('Revoke failed: ' + e.message);
    }
}
