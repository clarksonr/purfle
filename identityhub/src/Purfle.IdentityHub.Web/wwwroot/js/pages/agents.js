// ============================================================
// Purfle IdentityHub — Agent listing page
// ============================================================

let agentsState = {
    q: '',
    sort: 'newest',
    page: 1,
    pageSize: 20,
    trust: 'all',
    capabilities: [],
    engine: 'any'
};

async function renderAgentsPage() {
    // Parse query params
    const params = new URLSearchParams(location.search);
    agentsState.q = params.get('q') || '';
    agentsState.page = parseInt(params.get('page') || '1', 10);
    agentsState.sort = params.get('sort') || 'newest';

    Router.root.innerHTML = `
        <div class="container" style="padding-top: 32px;">
            <h1 style="font-size: 24px; margin-bottom: 24px;">Agents</h1>
            <div class="page-with-sidebar">
                <div class="main">
                    <div class="toolbar">
                        <input type="search" id="agent-search" placeholder="Search agents..." value="${escapeHtml(agentsState.q)}" />
                        <select id="agent-sort">
                            <option value="newest" ${agentsState.sort === 'newest' ? 'selected' : ''}>Newest</option>
                            <option value="attested" ${agentsState.sort === 'attested' ? 'selected' : ''}>Most Attested</option>
                            <option value="name" ${agentsState.sort === 'name' ? 'selected' : ''}>Name A–Z</option>
                        </select>
                    </div>
                    <div class="agent-grid" id="agents-grid">
                        <div class="empty-state"><p>Loading...</p></div>
                    </div>
                    <div class="pagination" id="agents-pagination"></div>
                </div>
                <aside class="sidebar">
                    <div class="filter-group">
                        <h3>Trust Level</h3>
                        <label><input type="radio" name="trust" value="all" checked /> All</label>
                        <label><input type="radio" name="trust" value="marketplace-listed" /> Marketplace Listed</label>
                        <label><input type="radio" name="trust" value="publisher-verified" /> Publisher Verified</label>
                        <label><input type="radio" name="trust" value="both" /> Both</label>
                    </div>
                    <div class="filter-group">
                        <h3>Capabilities</h3>
                        <label><input type="checkbox" value="llm.chat" /> llm.chat</label>
                        <label><input type="checkbox" value="network.outbound" /> network.outbound</label>
                        <label><input type="checkbox" value="fs.read" /> fs.read</label>
                        <label><input type="checkbox" value="fs.write" /> fs.write</label>
                        <label><input type="checkbox" value="env.read" /> env.read</label>
                        <label><input type="checkbox" value="mcp.tool" /> mcp.tool</label>
                    </div>
                    <div class="filter-group">
                        <h3>Engine</h3>
                        <label><input type="radio" name="engine" value="any" checked /> Any</label>
                        <label><input type="radio" name="engine" value="anthropic" /> Anthropic</label>
                        <label><input type="radio" name="engine" value="openai" /> OpenAI</label>
                        <label><input type="radio" name="engine" value="gemini" /> Gemini</label>
                        <label><input type="radio" name="engine" value="ollama" /> Ollama</label>
                    </div>
                </aside>
            </div>
        </div>
    `;

    // Wire up events
    const searchInput = document.getElementById('agent-search');
    let searchTimeout;
    searchInput.addEventListener('input', () => {
        clearTimeout(searchTimeout);
        searchTimeout = setTimeout(() => {
            agentsState.q = searchInput.value;
            agentsState.page = 1;
            loadAgentsList();
        }, 300);
    });

    document.getElementById('agent-sort').addEventListener('change', e => {
        agentsState.sort = e.target.value;
        agentsState.page = 1;
        loadAgentsList();
    });

    document.querySelectorAll('input[name="trust"]').forEach(r => {
        r.addEventListener('change', e => {
            agentsState.trust = e.target.value;
            agentsState.page = 1;
            loadAgentsList();
        });
    });

    document.querySelectorAll('.sidebar input[type="checkbox"]').forEach(cb => {
        cb.addEventListener('change', () => {
            agentsState.capabilities = [...document.querySelectorAll('.sidebar input[type="checkbox"]:checked')].map(c => c.value);
            agentsState.page = 1;
            loadAgentsList();
        });
    });

    document.querySelectorAll('input[name="engine"]').forEach(r => {
        r.addEventListener('change', e => {
            agentsState.engine = e.target.value;
            agentsState.page = 1;
            loadAgentsList();
        });
    });

    loadAgentsList();
}

async function loadAgentsList() {
    const grid = document.getElementById('agents-grid');
    const pagination = document.getElementById('agents-pagination');
    grid.innerHTML = '<div class="empty-state"><p>Loading...</p></div>';

    try {
        const params = new URLSearchParams();
        if (agentsState.q) params.set('q', agentsState.q);
        params.set('page', agentsState.page);
        params.set('pageSize', agentsState.pageSize);

        const data = await API.get(`/api/agents?${params}`);
        let agents = data.agents || [];
        const totalCount = data.totalCount || 0;
        const totalPages = Math.ceil(totalCount / agentsState.pageSize);

        // Client-side sort (API may not support all sort modes)
        if (agentsState.sort === 'name') {
            agents.sort((a, b) => (a.name || '').localeCompare(b.name || ''));
        } else if (agentsState.sort === 'attested') {
            agents.sort((a, b) => (b.totalDownloads || 0) - (a.totalDownloads || 0));
        }

        // Client-side filter by engine (if manifest data available)
        if (agentsState.engine !== 'any') {
            agents = agents.filter(a => {
                const eng = (a.engine || '').toLowerCase();
                return eng.includes(agentsState.engine);
            });
        }

        if (agents.length === 0) {
            grid.innerHTML = `<div class="empty-state"><h3>No agents found</h3><p>Try adjusting your search or filters.</p></div>`;
            pagination.innerHTML = '';
            return;
        }

        grid.innerHTML = agents.map(a => agentCardHtml(a)).join('');

        // Pagination
        if (totalPages > 1) {
            let phtml = '';
            phtml += `<button ${agentsState.page <= 1 ? 'disabled' : ''} onclick="agentsState.page--; loadAgentsList();">&laquo; Prev</button>`;
            for (let i = 1; i <= totalPages && i <= 10; i++) {
                phtml += `<button class="${i === agentsState.page ? 'active' : ''}" onclick="agentsState.page=${i}; loadAgentsList();">${i}</button>`;
            }
            phtml += `<button ${agentsState.page >= totalPages ? 'disabled' : ''} onclick="agentsState.page++; loadAgentsList();">Next &raquo;</button>`;
            pagination.innerHTML = phtml;
        } else {
            pagination.innerHTML = '';
        }

    } catch (e) {
        grid.innerHTML = `<div class="empty-state"><h3>Error loading agents</h3><p>${escapeHtml(e.message)}</p></div>`;
        pagination.innerHTML = '';
    }
}
