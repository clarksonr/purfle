// ============================================================
// Purfle IdentityHub — Admin agent moderation
// ============================================================

async function renderAdminAgents() {
    if (!API.isAdmin()) {
        renderAdminLogin(() => renderAdminAgents());
        return;
    }

    Router.root.innerHTML = `
        <div class="container" style="padding-top: 32px;">
            <h1 style="font-size: 24px; margin-bottom: 8px;">Agent Moderation</h1>
            <nav style="margin-bottom: 32px; display: flex; gap: 16px; font-size: 14px;">
                <a href="/admin">Dashboard</a>
                <a href="/admin/agents" class="active">Agents</a>
                <a href="/admin/publishers">Publishers</a>
                <a href="/admin/keys">Keys</a>
                <a href="/admin/attestations">Attestations</a>
            </nav>

            <table class="data-table" id="admin-agents-table">
                <thead>
                    <tr>
                        <th>Agent</th>
                        <th>Author</th>
                        <th>Version</th>
                        <th>Status</th>
                        <th>Actions</th>
                    </tr>
                </thead>
                <tbody id="admin-agents-body">
                    <tr><td colspan="5" style="text-align: center; color: var(--text-muted);">Loading...</td></tr>
                </tbody>
            </table>
        </div>

        <div class="modal-overlay" id="revoke-modal">
            <div class="modal">
                <h3>Revoke Agent</h3>
                <p style="color: var(--text-secondary); font-size: 14px; margin-bottom: 16px;">This will issue a revocation attestation. The agent will be marked as revoked on the public detail page.</p>
                <div class="form-group">
                    <label>Reason</label>
                    <textarea id="revoke-reason" placeholder="Reason for revocation..."></textarea>
                </div>
                <input type="hidden" id="revoke-agent-id" />
                <div class="modal-actions">
                    <button class="btn" onclick="document.getElementById('revoke-modal').classList.remove('open')">Cancel</button>
                    <button class="btn btn-danger" onclick="confirmRevoke()">Revoke</button>
                </div>
            </div>
        </div>

        <div class="modal-overlay" id="flag-modal">
            <div class="modal">
                <h3>Flag Agent</h3>
                <p style="color: var(--text-secondary); font-size: 14px; margin-bottom: 16px;">This will issue a flagged attestation visible on the public detail page.</p>
                <div class="form-group">
                    <label>Reason</label>
                    <textarea id="flag-reason" placeholder="Reason for flagging..."></textarea>
                </div>
                <input type="hidden" id="flag-agent-id" />
                <div class="modal-actions">
                    <button class="btn" onclick="document.getElementById('flag-modal').classList.remove('open')">Cancel</button>
                    <button class="btn btn-primary" onclick="confirmFlag()">Flag</button>
                </div>
            </div>
        </div>
    `;

    loadAdminAgentsList();
}

async function loadAdminAgentsList() {
    const tbody = document.getElementById('admin-agents-body');
    try {
        const data = await API.get('/api/agents?pageSize=100');
        const agents = data.agents || [];

        if (agents.length === 0) {
            tbody.innerHTML = '<tr><td colspan="5" style="text-align: center; color: var(--text-muted);">No agents listed</td></tr>';
            return;
        }

        tbody.innerHTML = agents.map(a => {
            const id = a.agentId || a.id || '';
            return `
                <tr>
                    <td><a href="/agents/${escapeHtml(id)}">${escapeHtml(a.name || id)}</a></td>
                    <td>${escapeHtml(a.author || a.publisherName || '')}</td>
                    <td>${escapeHtml(a.latestVersion || '')}</td>
                    <td><span class="badge badge-listed">Listed</span></td>
                    <td style="display: flex; gap: 6px;">
                        <a href="/agents/${escapeHtml(id)}" class="btn btn-sm">View</a>
                        <button class="btn btn-sm" onclick="showFlagModal('${escapeHtml(id)}')">Flag</button>
                        <button class="btn btn-sm btn-danger" onclick="showRevokeModal('${escapeHtml(id)}')">Revoke</button>
                        <button class="btn btn-sm btn-danger" onclick="deleteAgent('${escapeHtml(id)}')">Delete</button>
                    </td>
                </tr>
            `;
        }).join('');
    } catch (e) {
        tbody.innerHTML = `<tr><td colspan="5" style="color: var(--red);">${escapeHtml(e.message)}</td></tr>`;
    }
}

function showRevokeModal(agentId) {
    document.getElementById('revoke-agent-id').value = agentId;
    document.getElementById('revoke-reason').value = '';
    document.getElementById('revoke-modal').classList.add('open');
}

function showFlagModal(agentId) {
    document.getElementById('flag-agent-id').value = agentId;
    document.getElementById('flag-reason').value = '';
    document.getElementById('flag-modal').classList.add('open');
}

async function confirmRevoke() {
    const agentId = document.getElementById('revoke-agent-id').value;
    const reason = document.getElementById('revoke-reason').value.trim() || 'Revoked by admin';
    try {
        await API.adminPost(`/api/admin/agents/${encodeURIComponent(agentId)}/revoke`, { reason });
        document.getElementById('revoke-modal').classList.remove('open');
        loadAdminAgentsList();
    } catch (e) {
        alert('Revoke failed: ' + e.message);
    }
}

async function confirmFlag() {
    const agentId = document.getElementById('flag-agent-id').value;
    const reason = document.getElementById('flag-reason').value.trim() || 'Flagged by admin';
    try {
        await API.adminPost(`/api/admin/agents/${encodeURIComponent(agentId)}/flag`, {
            agentId,
            type: 'flagged',
            issuedBy: 'admin',
            details: reason
        });
        document.getElementById('flag-modal').classList.remove('open');
        loadAdminAgentsList();
    } catch (e) {
        alert('Flag failed: ' + e.message);
    }
}

async function deleteAgent(agentId) {
    if (!confirm(`Delete agent "${agentId}"? This cannot be undone.`)) return;
    try {
        await API.adminDelete(`/api/admin/agents/${encodeURIComponent(agentId)}`);
        loadAdminAgentsList();
    } catch (e) {
        alert('Delete failed: ' + e.message);
    }
}
