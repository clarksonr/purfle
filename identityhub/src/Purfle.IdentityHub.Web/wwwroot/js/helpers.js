// ============================================================
// Purfle IdentityHub — Shared helpers
// ============================================================

function escapeHtml(str) {
    const d = document.createElement('div');
    d.textContent = str ?? '';
    return d.innerHTML;
}

function formatDate(iso) {
    if (!iso) return '—';
    try { return new Date(iso).toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' }); }
    catch { return iso; }
}

function formatDateTime(iso) {
    if (!iso) return '—';
    try { return new Date(iso).toLocaleString('en-US', { year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' }); }
    catch { return iso; }
}

function trustBadges(attestations) {
    if (!attestations || !Array.isArray(attestations)) return '';
    const types = new Set(attestations.map(a => a.type));
    let html = '';
    if (types.has('publisher-verified')) html += '<span class="badge badge-verified">Publisher Verified</span>';
    if (types.has('marketplace-listed')) html += '<span class="badge badge-listed">Marketplace Listed</span>';
    if (types.has('revoked')) html += '<span class="badge badge-revoked">Revoked</span>';
    return html;
}

function translateCapability(cap, perms) {
    const p = perms || {};
    switch (cap) {
        case 'llm.chat': return { icon: 'ai', text: 'Uses AI inference (chat)' };
        case 'llm.completion': return { icon: 'ai', text: 'Uses AI inference (completion)' };
        case 'network.outbound': {
            const hosts = p['network.outbound']?.hosts?.join(', ') || 'any';
            return { icon: 'net', text: `Connects to: ${hosts}` };
        }
        case 'fs.read': {
            const paths = p['fs.read']?.paths?.join(', ') || 'declared paths';
            return { icon: 'fs', text: `Reads from: ${paths}` };
        }
        case 'fs.write': {
            const paths = p['fs.write']?.paths?.join(', ') || 'declared paths';
            return { icon: 'fs', text: `Writes to: ${paths}` };
        }
        case 'env.read': {
            const vars = p['env.read']?.vars?.join(', ') || 'declared variables';
            return { icon: 'env', text: `Reads environment variables: ${vars}` };
        }
        case 'mcp.tool': return { icon: 'tool', text: 'Uses MCP tool bindings' };
        default: return { icon: 'tool', text: cap };
    }
}

function translateSchedule(schedule) {
    if (!schedule) return 'No schedule configured';
    switch (schedule.trigger) {
        case 'interval': return `Runs every ${schedule.interval_minutes || schedule.intervalMinutes || '?'} minutes`;
        case 'cron': return `Cron: ${schedule.cron || '?'}`;
        case 'startup': return 'Runs on startup';
        default: return schedule.trigger || 'Unknown';
    }
}

function agentCardHtml(agent, attestations) {
    const id = agent.agentId || agent.id || '';
    return `
        <a href="/agents/${escapeHtml(id)}" class="agent-card">
            <div class="card-header">
                <span class="card-name">${escapeHtml(agent.name)}</span>
                <span class="card-version">${escapeHtml(agent.latestVersion || agent.version || '')}</span>
            </div>
            <span class="card-author">${escapeHtml(agent.author || agent.publisherName || '')}</span>
            <span class="card-desc">${escapeHtml(agent.description)}</span>
            <div class="card-badges">${trustBadges(attestations)}</div>
        </a>
    `;
}
