// ============================================================
// Purfle IdentityHub — Publisher page
// ============================================================

async function renderPublisherPage(id) {
    Router.root.innerHTML = '<div class="container" style="padding-top: 32px;"><p>Loading publisher...</p></div>';

    let publisher = null;
    try {
        publisher = await API.get(`/api/publishers/${encodeURIComponent(id)}`);
    } catch (e) {
        Router.root.innerHTML = `<div class="container"><div class="empty-state"><h3>Publisher not found</h3><p>${escapeHtml(e.message)}</p><p><a href="/agents">Back to agents</a></p></div></div>`;
        return;
    }

    const verified = publisher.isVerified;

    Router.root.innerHTML = `
        <div class="container" style="padding-top: 32px;">
            <div class="detail-header">
                <h1>${escapeHtml(publisher.displayName || id)}</h1>
                <div class="meta">
                    ${publisher.domain ? `<span>${escapeHtml(publisher.domain)}</span><span>&middot;</span>` : ''}
                    ${verified
                        ? `<span class="badge badge-verified">Domain Verified</span>`
                        : `<span class="badge" style="background: var(--bg-card); color: var(--text-muted);">Unverified</span>`}
                    ${verified && publisher.verifiedAt ? `<span>&middot;</span><span>Verified since ${formatDate(publisher.verifiedAt || publisher.createdAt)}</span>` : ''}
                </div>
            </div>

            <div class="detail-section">
                <h2>Trust Summary</h2>
                <div id="pub-trust-summary" style="color: var(--text-secondary); font-size: 14px;">Loading...</div>
            </div>

            <div class="detail-section">
                <h2>Agents by ${escapeHtml(publisher.displayName || id)}</h2>
                <div class="agent-grid" id="pub-agents-grid">
                    <div class="empty-state"><p>Loading...</p></div>
                </div>
            </div>
        </div>
    `;

    loadPublisherAgents(id, publisher.displayName);
}

async function loadPublisherAgents(publisherId, publisherName) {
    const grid = document.getElementById('pub-agents-grid');
    const summary = document.getElementById('pub-trust-summary');

    try {
        // Search for agents by this publisher
        const data = await API.get(`/api/agents?q=${encodeURIComponent(publisherName || publisherId)}&pageSize=50`);
        const agents = data.agents || [];

        // Filter to only this publisher's agents
        const pubAgents = agents.filter(a =>
            a.publisherId === publisherId ||
            a.author === publisherName ||
            a.publisherName === publisherName
        );

        const verifiedCount = pubAgents.length; // simplified
        summary.textContent = `${pubAgents.length} agent${pubAgents.length !== 1 ? 's' : ''} published`;

        if (pubAgents.length === 0) {
            grid.innerHTML = '<div class="empty-state"><h3>No agents found</h3><p>This publisher has no listed agents.</p></div>';
            return;
        }

        grid.innerHTML = pubAgents.map(a => agentCardHtml(a)).join('');
    } catch (e) {
        grid.innerHTML = `<div class="empty-state"><h3>Error loading agents</h3><p>${escapeHtml(e.message)}</p></div>`;
        summary.textContent = 'Could not load trust summary.';
    }
}
