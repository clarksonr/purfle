// ============================================================
// Purfle IdentityHub — Admin dashboard
// ============================================================

async function renderAdminDashboard() {
    if (!API.isAdmin()) {
        renderAdminLogin(() => renderAdminDashboard());
        return;
    }

    Router.root.innerHTML = `
        <div class="container" style="padding-top: 32px;">
            <h1 style="font-size: 24px; margin-bottom: 8px;">Admin Dashboard</h1>
            <nav style="margin-bottom: 32px; display: flex; gap: 16px; font-size: 14px;">
                <a href="/admin" class="active">Dashboard</a>
                <a href="/admin/agents">Agents</a>
                <a href="/admin/publishers">Publishers</a>
                <a href="/admin/keys">Keys</a>
                <a href="/admin/attestations">Attestations</a>
                <a href="/admin/backup">Backup</a>
                <a href="#" onclick="event.preventDefault(); API.setAdminToken(''); Router.navigate('/admin');">Logout</a>
            </nav>

            <div class="admin-stats" id="admin-stats">
                <div class="admin-stat"><div class="value" id="as-agents">—</div><div class="label">Total Agents</div></div>
                <div class="admin-stat"><div class="value" id="as-pending" style="color: var(--yellow);">—</div><div class="label">Pending Review</div></div>
                <div class="admin-stat"><div class="value" id="as-publishers" style="color: var(--green);">—</div><div class="label">Verified Publishers</div></div>
                <div class="admin-stat"><div class="value" id="as-keys" style="color: var(--blue);">—</div><div class="label">Registered Keys</div></div>
                <div class="admin-stat"><div class="value" id="as-revoked" style="color: var(--red);">—</div><div class="label">Revoked</div></div>
            </div>

            <div class="detail-section">
                <h2>Recent Activity</h2>
                <ul class="activity-feed" id="admin-activity">
                    <li><span class="action">Loading...</span></li>
                </ul>
            </div>
        </div>
    `;

    loadAdminStats();
    loadAdminActivity();
}

async function loadAdminStats() {
    try {
        const data = await API.adminGet('/api/admin/stats');
        document.getElementById('as-agents').textContent = data.totalAgents ?? 0;
    } catch { }

    // These would need more API endpoints to get exact numbers
    document.getElementById('as-pending').textContent = '0';
    document.getElementById('as-publishers').textContent = '—';
    document.getElementById('as-keys').textContent = '—';
    document.getElementById('as-revoked').textContent = '0';
}

async function loadAdminActivity() {
    const feed = document.getElementById('admin-activity');
    try {
        // Load recent attestations as proxy for activity
        const attestations = await API.adminGet('/api/admin/attestations?agentId=*');
        if (Array.isArray(attestations) && attestations.length > 0) {
            feed.innerHTML = attestations.slice(0, 20).map(a => `
                <li>
                    <span class="action">
                        <span class="badge ${a.type === 'revoked' ? 'badge-revoked' : a.type === 'publisher-verified' ? 'badge-verified' : 'badge-listed'}">${escapeHtml(a.type)}</span>
                        on <a href="/agents/${escapeHtml(a.agentId)}">${escapeHtml(a.agentId)}</a>
                        by ${escapeHtml(a.issuedBy)}
                    </span>
                    <span class="time">${formatDateTime(a.issuedAt)}</span>
                </li>
            `).join('');
        } else {
            feed.innerHTML = '<li><span class="action" style="color: var(--text-muted);">No recent activity</span></li>';
        }
    } catch {
        feed.innerHTML = '<li><span class="action" style="color: var(--text-muted);">No recent activity</span></li>';
    }
}

function renderAdminLogin(callback) {
    Router.root.innerHTML = `
        <div class="admin-login">
            <h2>Admin Login</h2>
            <p style="color: var(--text-secondary); font-size: 14px; margin-bottom: 16px;">Enter the PURFLE_ADMIN_TOKEN to access the admin panel.</p>
            <div class="form-group">
                <input type="password" id="admin-token-input" placeholder="Admin token..." />
            </div>
            <button class="btn btn-primary" style="width: 100%;" onclick="adminLogin()">Login</button>
            <p id="admin-login-error" style="color: var(--red); font-size: 13px; margin-top: 12px; display: none;"></p>
        </div>
    `;

    window.adminLogin = async function () {
        const token = document.getElementById('admin-token-input').value.trim();
        if (!token) return;
        API.setAdminToken(token);
        try {
            await API.adminGet('/api/admin/stats');
            callback();
        } catch {
            API.setAdminToken('');
            const err = document.getElementById('admin-login-error');
            err.textContent = 'Invalid token. Check PURFLE_ADMIN_TOKEN.';
            err.style.display = 'block';
        }
    };

    document.getElementById('admin-token-input').addEventListener('keydown', e => {
        if (e.key === 'Enter') adminLogin();
    });
}
