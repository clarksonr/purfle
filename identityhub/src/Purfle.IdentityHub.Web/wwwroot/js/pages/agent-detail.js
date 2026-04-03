// ============================================================
// Purfle IdentityHub — Agent detail page
// ============================================================

async function renderAgentDetailPage(id) {
    Router.root.innerHTML = '<div class="container" style="padding-top: 32px;"><p>Loading agent...</p></div>';

    let agent = null;
    let attestations = [];

    try {
        agent = await API.get(`/api/agents/${encodeURIComponent(id)}`);
    } catch (e) {
        Router.root.innerHTML = `<div class="container"><div class="empty-state"><h3>Agent not found</h3><p>${escapeHtml(e.message)}</p><p><a href="/agents">Back to agents</a></p></div></div>`;
        return;
    }

    try {
        attestations = await API.get(`/api/attestations?agentId=${encodeURIComponent(id)}`);
        if (!Array.isArray(attestations)) attestations = [];
    } catch { attestations = []; }

    const attTypes = new Set(attestations.map(a => a.type));
    const isRevoked = attTypes.has('revoked');
    const isVerified = attTypes.has('publisher-verified');
    const isListed = attTypes.has('marketplace-listed');

    // Try to parse manifest from versions if available
    let manifest = null;
    let schedule = null;
    let capabilities = [];
    let permissions = {};
    let tools = [];
    let identity = null;

    // The detail response may include manifest data
    if (agent.manifest) {
        manifest = agent.manifest;
    }
    if (manifest) {
        schedule = manifest.schedule;
        capabilities = manifest.capabilities || [];
        permissions = manifest.permissions || {};
        tools = manifest.tools || [];
        identity = manifest.identity;
    }

    const versions = agent.versions || [];
    const latestVer = versions.length > 0 ? versions[0] : null;

    Router.root.innerHTML = `
        <div class="container" style="padding-top: 32px;">
            ${isRevoked ? '<div class="alert alert-danger"><strong>Security Advisory:</strong> This agent has been revoked. It should not be installed or run.</div>' : ''}

            <div class="detail-header">
                <h1>${escapeHtml(agent.name || id)}</h1>
                <div class="meta">
                    <span>v${escapeHtml(latestVer?.version || agent.latestVersion || '?')}</span>
                    <span>&middot;</span>
                    ${agent.publisherName ? `<a href="/publishers/${escapeHtml(agent.publisherId || '')}">${escapeHtml(agent.publisherName)}</a>` : `<span>${escapeHtml(agent.author || 'Unknown')}</span>`}
                    <span>&middot;</span>
                    <span>${escapeHtml(agent.description || '')}</span>
                </div>
                <div style="margin-top: 12px; display: flex; gap: 8px; flex-wrap: wrap;">
                    ${isVerified ? '<span class="badge badge-verified">Publisher Verified</span>' : '<span class="badge" style="background: var(--bg-card); color: var(--text-muted);">Publisher Verified</span>'}
                    ${isListed ? '<span class="badge badge-listed">Marketplace Listed</span>' : '<span class="badge" style="background: var(--bg-card); color: var(--text-muted);">Marketplace Listed</span>'}
                    ${isRevoked ? '<span class="badge badge-revoked">Revoked</span>' : ''}
                </div>
            </div>

            <!-- Install button -->
            <div class="detail-section">
                <a href="purfle://install?id=${encodeURIComponent(id)}" class="install-btn">Install in Purfle</a>
                <div class="install-fallback">
                    Or run: <code>purfle install ${escapeHtml(id)}</code>
                </div>
            </div>

            <!-- Signature status -->
            <div class="detail-section">
                <h2>Signature</h2>
                ${renderSignatureStatus(identity, isRevoked)}
            </div>

            <!-- Schedule -->
            ${schedule ? `
            <div class="detail-section">
                <h2>Schedule</h2>
                <p style="font-size: 15px; color: var(--text-secondary);">${escapeHtml(translateSchedule(schedule))}</p>
            </div>
            ` : ''}

            <!-- Capabilities -->
            ${capabilities.length > 0 ? `
            <div class="detail-section">
                <h2>Capabilities</h2>
                <ul class="cap-list">
                    ${capabilities.map(cap => {
                        const t = translateCapability(cap, permissions);
                        return `<li><div class="cap-icon ${t.icon}">${capIconChar(t.icon)}</div><span>${escapeHtml(t.text)}</span></li>`;
                    }).join('')}
                </ul>
            </div>
            ` : ''}

            <!-- MCP tools -->
            ${tools.length > 0 ? `
            <div class="detail-section">
                <h2>Required MCP Servers</h2>
                <table class="data-table">
                    <thead><tr><th>Name</th><th>Server</th><th>Description</th></tr></thead>
                    <tbody>
                        ${tools.map(t => `<tr><td>${escapeHtml(t.name)}</td><td><code>${escapeHtml(t.server)}</code></td><td>${escapeHtml(t.description || '')}</td></tr>`).join('')}
                    </tbody>
                </table>
            </div>
            ` : ''}

            <!-- Version history -->
            ${versions.length > 0 ? `
            <div class="detail-section">
                <h2>Version History</h2>
                <table class="data-table">
                    <thead><tr><th>Version</th><th>Released</th><th>Downloads</th></tr></thead>
                    <tbody>
                        ${versions.map(v => `<tr><td>${escapeHtml(v.version)}</td><td>${formatDate(v.publishedAt)}</td><td>${v.downloads ?? 0}</td></tr>`).join('')}
                    </tbody>
                </table>
            </div>
            ` : ''}

            <!-- Attestations -->
            ${attestations.length > 0 ? `
            <div class="detail-section">
                <h2>Attestations</h2>
                <table class="data-table">
                    <thead><tr><th>Type</th><th>Issued By</th><th>Date</th></tr></thead>
                    <tbody>
                        ${attestations.map(a => `
                            <tr>
                                <td><span class="badge ${a.type === 'revoked' ? 'badge-revoked' : a.type === 'publisher-verified' ? 'badge-verified' : 'badge-listed'}">${escapeHtml(a.type)}</span></td>
                                <td>${escapeHtml(a.issuedBy)}</td>
                                <td>${formatDate(a.issuedAt)}</td>
                            </tr>
                        `).join('')}
                    </tbody>
                </table>
            </div>
            ` : ''}

            <!-- Publisher info -->
            ${agent.publisherName ? `
            <div class="detail-section">
                <h2>Publisher</h2>
                <div class="publisher-card">
                    <a href="/publishers/${escapeHtml(agent.publisherId || '')}" style="font-size: 16px; font-weight: 600;">${escapeHtml(agent.publisherName)}</a>
                </div>
            </div>
            ` : ''}

            <!-- Raw manifest -->
            <div class="detail-section">
                <button class="collapsible-toggle" onclick="this.nextElementSibling.classList.toggle('open')">View Raw Manifest</button>
                <div class="collapsible-content">
                    <pre id="raw-manifest">Loading...</pre>
                </div>
            </div>
        </div>
    `;

    // Try to load raw manifest
    loadRawManifest(id);
}

function renderSignatureStatus(identity, isRevoked) {
    if (isRevoked) {
        return `<div class="alert alert-danger">Key has been <strong>revoked</strong>.</div>`;
    }
    if (!identity) {
        return `<p style="color: var(--text-muted);">No signature information available.</p>`;
    }
    const expired = identity.expires_at && new Date(identity.expires_at) < new Date();
    if (expired) {
        return `<div class="alert alert-danger">Signature <strong>expired</strong> on ${formatDate(identity.expires_at)}.</div>`;
    }
    return `
        <dl class="key-info">
            <dt>Status</dt><dd style="color: var(--green);">Verified</dd>
            <dt>Key ID</dt><dd>${identity.key_id ? `<a href="/keys/${escapeHtml(identity.key_id)}">${escapeHtml(identity.key_id)}</a>` : '—'}</dd>
            <dt>Algorithm</dt><dd>${escapeHtml(identity.algorithm || 'ES256')}</dd>
            <dt>Issued</dt><dd>${formatDate(identity.issued_at)}</dd>
            <dt>Expires</dt><dd>${formatDate(identity.expires_at)}</dd>
        </dl>
    `;
}

function capIconChar(icon) {
    switch (icon) {
        case 'ai': return '&#x2728;';
        case 'net': return '&#x1F310;';
        case 'fs': return '&#x1F4C1;';
        case 'env': return '&#x1F511;';
        case 'tool': return '&#x1F527;';
        default: return '?';
    }
}

async function loadRawManifest(id) {
    const pre = document.getElementById('raw-manifest');
    if (!pre) return;
    try {
        const resp = await fetch(`/api/agents/${encodeURIComponent(id)}`);
        const data = await resp.json();
        pre.textContent = JSON.stringify(data, null, 2);
    } catch (e) {
        pre.textContent = 'Could not load manifest: ' + e.message;
    }
}
