// ============================================================
// Purfle IdentityHub — Key lookup page
// ============================================================

async function renderKeyLookupPage(keyId) {
    Router.root.innerHTML = '<div class="container" style="padding-top: 32px;"><p>Loading key...</p></div>';

    let key = null;
    let revocationStatus = null;

    // Try marketplace key registry
    try {
        key = await API.get(`/api/keys/${encodeURIComponent(keyId)}`);
    } catch { }

    // Try identityhub key revocation check
    try {
        revocationStatus = await API.get(`/api/hub/keys/${encodeURIComponent(keyId)}`);
    } catch { }

    const isRevoked = key?.isRevoked || revocationStatus?.isRevoked;

    Router.root.innerHTML = `
        <div class="container" style="padding-top: 32px;">
            ${isRevoked ? '<div class="alert alert-danger"><strong>This key has been revoked.</strong> Agents signed with this key should not be trusted.</div>' : ''}

            <div class="detail-header">
                <h1>Key: ${escapeHtml(keyId)}</h1>
                <div class="meta">
                    ${isRevoked
                        ? '<span class="badge badge-revoked">Revoked</span>'
                        : '<span class="badge badge-verified">Active</span>'}
                </div>
            </div>

            <div class="detail-section">
                <h2>Key Details</h2>
                <dl class="key-info">
                    <dt>Key ID</dt><dd>${escapeHtml(keyId)}</dd>
                    <dt>Algorithm</dt><dd>${escapeHtml(key?.algorithm || 'ES256')}</dd>
                    <dt>Status</dt><dd style="color: ${isRevoked ? 'var(--red)' : 'var(--green)'};">${isRevoked ? 'Revoked' : 'Active'}</dd>
                    ${key?.x ? `<dt>Public Key X</dt><dd style="font-family: var(--mono); font-size: 12px;">${escapeHtml(key.x)}</dd>` : ''}
                    ${key?.y ? `<dt>Public Key Y</dt><dd style="font-family: var(--mono); font-size: 12px;">${escapeHtml(key.y)}</dd>` : ''}
                </dl>
            </div>

            <div class="detail-section">
                <h2>Agents Signed with This Key</h2>
                <div class="agent-grid" id="key-agents-grid">
                    <div class="empty-state"><p>Loading...</p></div>
                </div>
            </div>
        </div>
    `;

    loadKeyAgents(keyId);
}

async function loadKeyAgents(keyId) {
    const grid = document.getElementById('key-agents-grid');
    try {
        // Search hub registry for agents with this key
        const data = await API.get(`/api/hub/agents?q=${encodeURIComponent(keyId)}&pageSize=50`);
        // The hub may return registry entries with keyId matching
        let entries = [];
        if (Array.isArray(data)) {
            entries = data.filter(e => e.keyId === keyId);
        } else if (data.items) {
            entries = data.items.filter(e => e.keyId === keyId);
        }

        if (entries.length === 0) {
            grid.innerHTML = '<div class="empty-state"><p>No agents found signed with this key, or data not yet indexed.</p></div>';
            return;
        }

        grid.innerHTML = entries.map(e => `
            <a href="/agents/${escapeHtml(e.agentId)}" class="agent-card">
                <div class="card-header">
                    <span class="card-name">${escapeHtml(e.name)}</span>
                    <span class="card-version">${escapeHtml(e.version || '')}</span>
                </div>
                <span class="card-desc">${escapeHtml(e.description || '')}</span>
            </a>
        `).join('');
    } catch {
        grid.innerHTML = '<div class="empty-state"><p>Could not load agents for this key.</p></div>';
    }
}
