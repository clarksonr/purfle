// ============================================================
// Purfle IdentityHub — Admin attestation manager
// ============================================================

async function renderAdminAttestations() {
    if (!API.isAdmin()) {
        renderAdminLogin(() => renderAdminAttestations());
        return;
    }

    Router.root.innerHTML = `
        <div class="container" style="padding-top: 32px;">
            <h1 style="font-size: 24px; margin-bottom: 8px;">Attestation Manager</h1>
            <nav style="margin-bottom: 32px; display: flex; gap: 16px; font-size: 14px;">
                <a href="/admin">Dashboard</a>
                <a href="/admin/agents">Agents</a>
                <a href="/admin/publishers">Publishers</a>
                <a href="/admin/keys">Keys</a>
                <a href="/admin/attestations" class="active">Attestations</a>
            </nav>

            <!-- Issue new attestation -->
            <div style="margin-bottom: 32px; padding: 20px; background: var(--bg-card); border: 1px solid var(--border); border-radius: var(--radius);">
                <h3 style="margin-bottom: 16px;">Issue Manual Attestation</h3>
                <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 16px;">
                    <div class="form-group">
                        <label>Agent ID</label>
                        <input type="text" id="att-agent-id" placeholder="Agent ID..." />
                    </div>
                    <div class="form-group">
                        <label>Attestation Type</label>
                        <select id="att-type">
                            <option value="marketplace-listed">marketplace-listed</option>
                            <option value="publisher-verified">publisher-verified</option>
                            <option value="community-reviewed">community-reviewed</option>
                            <option value="revoked">revoked</option>
                            <option value="flagged">flagged</option>
                        </select>
                    </div>
                </div>
                <div class="form-group">
                    <label>Notes</label>
                    <textarea id="att-notes" placeholder="Optional notes..."></textarea>
                </div>
                <button class="btn btn-primary" onclick="issueAttestation()">Issue Attestation</button>
                <p id="att-result" style="font-size: 13px; margin-top: 8px; display: none;"></p>
            </div>

            <!-- Filter -->
            <div class="toolbar">
                <input type="search" id="att-filter-agent" placeholder="Filter by agent ID..." />
                <select id="att-filter-type">
                    <option value="">All types</option>
                    <option value="marketplace-listed">marketplace-listed</option>
                    <option value="publisher-verified">publisher-verified</option>
                    <option value="community-reviewed">community-reviewed</option>
                    <option value="revoked">revoked</option>
                    <option value="flagged">flagged</option>
                </select>
                <button class="btn" onclick="loadAdminAttestations()">Search</button>
            </div>

            <table class="data-table">
                <thead>
                    <tr>
                        <th>Agent</th>
                        <th>Type</th>
                        <th>Issued By</th>
                        <th>Date</th>
                        <th>Actions</th>
                    </tr>
                </thead>
                <tbody id="admin-att-body">
                    <tr><td colspan="5" style="text-align: center; color: var(--text-muted);">Enter an agent ID to search attestations</td></tr>
                </tbody>
            </table>
        </div>
    `;
}

async function issueAttestation() {
    const agentId = document.getElementById('att-agent-id').value.trim();
    const type = document.getElementById('att-type').value;
    const notes = document.getElementById('att-notes').value.trim();
    const result = document.getElementById('att-result');

    if (!agentId) {
        result.textContent = 'Agent ID is required.';
        result.style.color = 'var(--red)';
        result.style.display = 'block';
        return;
    }

    try {
        await API.adminPost('/api/admin/attestations', {
            agentId,
            type,
            issuedBy: 'admin',
            details: notes || null
        });
        result.textContent = `Attestation "${type}" issued for ${agentId}.`;
        result.style.color = 'var(--green)';
        result.style.display = 'block';

        // Clear form
        document.getElementById('att-agent-id').value = '';
        document.getElementById('att-notes').value = '';
    } catch (e) {
        result.textContent = 'Failed: ' + e.message;
        result.style.color = 'var(--red)';
        result.style.display = 'block';
    }
}

async function loadAdminAttestations() {
    const agentId = document.getElementById('att-filter-agent').value.trim();
    const typeFilter = document.getElementById('att-filter-type').value;
    const tbody = document.getElementById('admin-att-body');

    if (!agentId) {
        tbody.innerHTML = '<tr><td colspan="5" style="text-align: center; color: var(--text-muted);">Enter an agent ID to search attestations</td></tr>';
        return;
    }

    tbody.innerHTML = '<tr><td colspan="5" style="text-align: center; color: var(--text-muted);">Loading...</td></tr>';

    try {
        let attestations = await API.adminGet(`/api/admin/attestations?agentId=${encodeURIComponent(agentId)}`);
        if (!Array.isArray(attestations)) attestations = [];

        if (typeFilter) {
            attestations = attestations.filter(a => a.type === typeFilter);
        }

        if (attestations.length === 0) {
            tbody.innerHTML = '<tr><td colspan="5" style="text-align: center; color: var(--text-muted);">No attestations found</td></tr>';
            return;
        }

        tbody.innerHTML = attestations.map(a => `
            <tr>
                <td><a href="/agents/${escapeHtml(a.agentId)}">${escapeHtml(a.agentId)}</a></td>
                <td><span class="badge ${a.type === 'revoked' ? 'badge-revoked' : a.type === 'publisher-verified' ? 'badge-verified' : a.type === 'flagged' ? 'badge-revoked' : 'badge-listed'}">${escapeHtml(a.type)}</span></td>
                <td>${escapeHtml(a.issuedBy)}</td>
                <td>${formatDateTime(a.issuedAt)}</td>
                <td>
                    <button class="btn btn-sm btn-danger" onclick="revokeAttestation('${escapeHtml(a.agentId)}', '${escapeHtml(a.id || '')}')">Revoke</button>
                </td>
            </tr>
        `).join('');
    } catch (e) {
        tbody.innerHTML = `<tr><td colspan="5" style="color: var(--red);">${escapeHtml(e.message)}</td></tr>`;
    }
}

async function revokeAttestation(agentId, attestationId) {
    if (!confirm('Revoke this attestation?')) return;
    try {
        // Issue a counter-attestation of type "revoked"
        await API.adminPost('/api/admin/attestations', {
            agentId,
            type: 'attestation-revoked',
            issuedBy: 'admin',
            details: `Revoked attestation ${attestationId}`
        });
        loadAdminAttestations();
    } catch (e) {
        alert('Revoke failed: ' + e.message);
    }
}
