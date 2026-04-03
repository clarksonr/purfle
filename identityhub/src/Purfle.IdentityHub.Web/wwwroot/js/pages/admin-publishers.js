// ============================================================
// Purfle IdentityHub — Admin publisher management
// ============================================================

async function renderAdminPublishers() {
    if (!API.isAdmin()) {
        renderAdminLogin(() => renderAdminPublishers());
        return;
    }

    Router.root.innerHTML = `
        <div class="container" style="padding-top: 32px;">
            <h1 style="font-size: 24px; margin-bottom: 8px;">Publisher Management</h1>
            <nav style="margin-bottom: 32px; display: flex; gap: 16px; font-size: 14px;">
                <a href="/admin">Dashboard</a>
                <a href="/admin/agents">Agents</a>
                <a href="/admin/publishers" class="active">Publishers</a>
                <a href="/admin/keys">Keys</a>
                <a href="/admin/attestations">Attestations</a>
            </nav>

            <table class="data-table">
                <thead>
                    <tr>
                        <th>Publisher</th>
                        <th>Domain</th>
                        <th>Status</th>
                        <th>Created</th>
                        <th>Actions</th>
                    </tr>
                </thead>
                <tbody id="admin-publishers-body">
                    <tr><td colspan="5" style="text-align: center; color: var(--text-muted);">Loading...</td></tr>
                </tbody>
            </table>
        </div>
    `;

    loadAdminPublishers();
}

async function loadAdminPublishers() {
    const tbody = document.getElementById('admin-publishers-body');
    try {
        // Try to get publishers from identityhub
        let publishers = [];
        try {
            const resp = await fetch('/api/hub/agents?pageSize=100');
            const data = await resp.json();
            // Extract unique publishers from agent listings
            const pubMap = new Map();
            const items = Array.isArray(data) ? data : (data.items || []);
            items.forEach(a => {
                if (a.publisherId && !pubMap.has(a.publisherId)) {
                    pubMap.set(a.publisherId, {
                        id: a.publisherId,
                        displayName: a.publisherId,
                        domain: '',
                        isVerified: false,
                        createdAt: a.registeredAt
                    });
                }
            });
            publishers = [...pubMap.values()];
        } catch { }

        // Also try marketplace publishers endpoint
        try {
            const agents = await API.get('/api/agents?pageSize=100');
            const pubMap = new Map();
            (agents.agents || []).forEach(a => {
                if (a.publisherName && !pubMap.has(a.publisherName)) {
                    pubMap.set(a.publisherName, {
                        id: a.publisherId || a.publisherName,
                        displayName: a.publisherName,
                        domain: '',
                        isVerified: false,
                        createdAt: a.publishedAt
                    });
                }
            });
            // Merge
            pubMap.forEach((v, k) => {
                if (!publishers.find(p => p.displayName === k)) publishers.push(v);
            });
        } catch { }

        if (publishers.length === 0) {
            tbody.innerHTML = '<tr><td colspan="5" style="text-align: center; color: var(--text-muted);">No publishers found</td></tr>';
            return;
        }

        tbody.innerHTML = publishers.map(p => `
            <tr>
                <td><a href="/publishers/${escapeHtml(p.id)}">${escapeHtml(p.displayName)}</a></td>
                <td>${escapeHtml(p.domain || '—')}</td>
                <td>${p.isVerified
                    ? '<span class="badge badge-verified">Verified</span>'
                    : '<span class="badge" style="background: var(--bg-primary); color: var(--text-muted);">Unverified</span>'}</td>
                <td>${formatDate(p.createdAt)}</td>
                <td style="display: flex; gap: 6px;">
                    ${!p.isVerified
                        ? `<button class="btn btn-sm btn-primary" onclick="verifyPublisher('${escapeHtml(p.id)}')">Verify</button>`
                        : `<button class="btn btn-sm btn-danger" onclick="revokePublisherVerification('${escapeHtml(p.id)}')">Revoke</button>`}
                    <button class="btn btn-sm btn-danger" onclick="deletePublisher('${escapeHtml(p.id)}')">Delete</button>
                </td>
            </tr>
        `).join('');
    } catch (e) {
        tbody.innerHTML = `<tr><td colspan="5" style="color: var(--red);">${escapeHtml(e.message)}</td></tr>`;
    }
}

async function verifyPublisher(id) {
    try {
        await API.adminPost(`/api/admin/publishers/${encodeURIComponent(id)}/verify`);
        loadAdminPublishers();
    } catch (e) {
        alert('Verification failed: ' + e.message);
    }
}

async function revokePublisherVerification(id) {
    if (!confirm('Revoke publisher verification?')) return;
    try {
        await API.adminPost(`/api/admin/publishers/${encodeURIComponent(id)}/revoke`);
        loadAdminPublishers();
    } catch (e) {
        alert('Revocation failed: ' + e.message);
    }
}

async function deletePublisher(id) {
    if (!confirm(`Delete publisher "${id}" and all their agents? This cannot be undone.`)) return;
    try {
        await API.adminDelete(`/api/admin/publishers/${encodeURIComponent(id)}`);
        loadAdminPublishers();
    } catch (e) {
        alert('Delete failed: ' + e.message);
    }
}
