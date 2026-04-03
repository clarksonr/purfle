// ============================================================
// Purfle IdentityHub — Home page
// ============================================================

async function renderHomePage() {
    Router.root.innerHTML = `
        <section class="hero">
            <h1><span>Purfle</span> — Trusted AI Agents for Your Desktop</h1>
            <p class="tagline">Every agent is signed, sandboxed, and declared. You see exactly what it can do before it runs.</p>
            <form class="search-bar" onsubmit="event.preventDefault(); Router.navigate('/agents?q=' + encodeURIComponent(this.q.value))">
                <input type="search" name="q" placeholder="Search agents..." autocomplete="off" />
                <button type="submit">Search</button>
            </form>
        </section>

        <div class="stats-bar" id="home-stats">
            <div class="stat"><div class="value" id="stat-agents">—</div><div class="label">Agents Listed</div></div>
            <div class="stat"><div class="value" id="stat-publishers">—</div><div class="label">Verified Publishers</div></div>
            <div class="stat"><div class="value" id="stat-keys">—</div><div class="label">Keys Registered</div></div>
        </div>

        <div class="container">
            <h2 class="section-title">Featured Agents</h2>
            <div class="agent-grid" id="featured-agents">
                <div class="empty-state"><p>Loading agents...</p></div>
            </div>

            <h2 class="section-title">How It Works</h2>
            <div class="how-it-works">
                <div class="how-step">
                    <div class="step-num">1</div>
                    <h3>Browse</h3>
                    <p>Find agents that do what you need. Every agent declares its capabilities — you know exactly what it can access before you install it.</p>
                </div>
                <div class="how-step">
                    <div class="step-num">2</div>
                    <h3>Install</h3>
                    <p>One click installs the agent into Purfle on your desktop. Review its permissions on the consent screen, then approve.</p>
                </div>
                <div class="how-step">
                    <div class="step-num">3</div>
                    <h3>Runs Unattended</h3>
                    <p>Agents run on a schedule — every 15 minutes, daily at 7 AM, or on startup. They work in the background while you do other things.</p>
                </div>
            </div>
        </div>
    `;

    // Load stats
    loadHomeStats();
    // Load featured agents
    loadFeaturedAgents();
}

async function loadHomeStats() {
    try {
        const data = await API.get('/api/agents?pageSize=1');
        document.getElementById('stat-agents').textContent = data.totalCount ?? 0;
    } catch { document.getElementById('stat-agents').textContent = '0'; }

    // Publishers and keys — try, but these endpoints may not exist yet
    try {
        const data = await API.get('/api/hub/agents?pageSize=1');
        // Use total from hub as rough key count proxy
        document.getElementById('stat-keys').textContent = '—';
    } catch { }
    document.getElementById('stat-publishers').textContent = '—';
}

async function loadFeaturedAgents() {
    const grid = document.getElementById('featured-agents');
    try {
        const data = await API.get('/api/agents?pageSize=6&page=1');
        const agents = data.agents || [];
        if (agents.length === 0) {
            grid.innerHTML = '<div class="empty-state"><h3>No agents listed yet</h3><p>Be the first to publish an agent!</p></div>';
            return;
        }
        grid.innerHTML = agents.map(a => agentCardHtml(a)).join('');
    } catch (e) {
        grid.innerHTML = `<div class="empty-state"><h3>Could not load agents</h3><p>${escapeHtml(e.message)}</p></div>`;
    }
}
