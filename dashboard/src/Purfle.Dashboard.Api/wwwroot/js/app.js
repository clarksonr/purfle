// ── State ─────────────────────────────────────────────────────────────

let agents = [];
let selectedAgentId = null;
let connection = null;

// ── DOM refs ──────────────────────────────────────────────────────────

const agentGrid = document.getElementById("agent-grid");
const detailPanel = document.getElementById("detail-panel");
const connectionDot = document.getElementById("connection-dot");
const connectionLabel = document.getElementById("connection-label");
const statTotal = document.getElementById("stat-total");
const statRunning = document.getElementById("stat-running");
const statStopped = document.getElementById("stat-stopped");
const statError = document.getElementById("stat-error");

// ── Fetch agents ──────────────────────────────────────────────────────

async function fetchAgents() {
    try {
        const res = await fetch("/api/agents");
        agents = await res.json();
        renderGrid();
        updateStats();
    } catch (err) {
        console.error("Failed to fetch agents:", err);
    }
}

async function fetchLogs(agentId) {
    try {
        const res = await fetch(`/api/agents/${agentId}/logs`);
        return await res.json();
    } catch (err) {
        console.error("Failed to fetch logs:", err);
        return [];
    }
}

// ── Render ────────────────────────────────────────────────────────────

function renderGrid() {
    agentGrid.innerHTML = agents.map(a => `
        <div class="agent-card ${a.id === selectedAgentId ? 'selected' : ''}"
             data-id="${a.id}" onclick="selectAgent('${a.id}')">
            <div class="card-header">
                <div>
                    <span class="card-name">${esc(a.name)}</span>
                    <span class="card-version">v${esc(a.version)}</span>
                </div>
                <span class="status-badge ${a.status.toLowerCase()}">
                    <span class="status-dot"></span>
                    ${a.status}
                </span>
            </div>
            <div class="card-description">${esc(a.description)}</div>
            <div class="card-meta">
                <span class="card-meta-item">${esc(a.engine)}</span>
                <span class="card-meta-item">${esc(a.trigger)}${a.intervalMinutes ? ' / ' + a.intervalMinutes + 'm' : ''}</span>
                ${a.lastRun ? `<span class="card-meta-item">${formatTime(a.lastRun)}</span>` : ''}
            </div>
        </div>
    `).join("");
}

function updateStats() {
    statTotal.textContent = agents.length;
    statRunning.textContent = agents.filter(a => a.status === "Running").length;
    statStopped.textContent = agents.filter(a => a.status === "Stopped").length;
    statError.textContent = agents.filter(a => a.status === "Error").length;
}

async function selectAgent(id) {
    selectedAgentId = id;
    const agent = agents.find(a => a.id === id);
    if (!agent) return;

    renderGrid();

    const logs = await fetchLogs(id);

    detailPanel.classList.remove("hidden");
    detailPanel.innerHTML = `
        <div class="detail-header">
            <div class="detail-title">${esc(agent.name)}</div>
            <div class="detail-subtitle">${esc(agent.description)}</div>
            <div class="detail-info">
                <div class="detail-info-item">
                    <span class="detail-info-label">Engine</span>
                    ${esc(agent.engine)} / ${esc(agent.model)}
                </div>
                <div class="detail-info-item">
                    <span class="detail-info-label">Schedule</span>
                    ${esc(agent.trigger)}${agent.intervalMinutes ? ' / ' + agent.intervalMinutes + 'm' : ''}
                </div>
                <div class="detail-info-item">
                    <span class="detail-info-label">Last Run</span>
                    ${agent.lastRun ? formatTime(agent.lastRun) : 'Never'}
                </div>
                <div class="detail-info-item">
                    <span class="detail-info-label">Next Run</span>
                    ${agent.nextRun ? formatTime(agent.nextRun) : 'N/A'}
                </div>
            </div>
            <div class="detail-actions">
                <button class="btn btn-start" onclick="startAgent('${id}')"
                    ${agent.status === 'Running' ? 'disabled' : ''}>Start</button>
                <button class="btn btn-stop" onclick="stopAgent('${id}')"
                    ${agent.status !== 'Running' ? 'disabled' : ''}>Stop</button>
            </div>
        </div>
        <div class="detail-logs">
            <div class="logs-title">Log</div>
            <div id="log-entries">
                ${renderLogs(logs)}
            </div>
        </div>
    `;
}

function renderLogs(logs) {
    if (!logs || logs.length === 0) {
        return '<div style="color: var(--text-muted); font-size: 13px;">No log entries.</div>';
    }
    return logs.map(l => `
        <div class="log-entry">
            <span class="log-time">${formatTime(l.timestamp)}</span>
            <span class="log-level ${l.level}">${l.level}</span>
            <span class="log-message">${esc(l.message)}</span>
        </div>
    `).join("");
}

// ── Actions ───────────────────────────────────────────────────────────

async function startAgent(id) {
    try {
        const res = await fetch(`/api/agents/${id}/start`, { method: "POST" });
        if (res.ok) {
            const data = await res.json();
            const agent = agents.find(a => a.id === id);
            if (agent) {
                agent.status = data.status;
            }
            renderGrid();
            updateStats();
            selectAgent(id);
        }
    } catch (err) {
        console.error("Failed to start agent:", err);
    }
}

async function stopAgent(id) {
    try {
        const res = await fetch(`/api/agents/${id}/stop`, { method: "POST" });
        if (res.ok) {
            const data = await res.json();
            const agent = agents.find(a => a.id === id);
            if (agent) {
                agent.status = data.status;
                agent.nextRun = null;
            }
            renderGrid();
            updateStats();
            selectAgent(id);
        }
    } catch (err) {
        console.error("Failed to stop agent:", err);
    }
}

// ── SignalR ───────────────────────────────────────────────────────────

async function connectSignalR() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/agents")
        .withAutomaticReconnect()
        .build();

    connection.on("StatusUpdate", (agentId, status) => {
        const agent = agents.find(a => a.id === agentId);
        if (agent) {
            agent.status = status;
            renderGrid();
            updateStats();
            if (selectedAgentId === agentId) {
                selectAgent(agentId);
            }
        }
    });

    connection.on("LogEntry", (agentId, timestamp, level, message) => {
        if (selectedAgentId === agentId) {
            const logEntries = document.getElementById("log-entries");
            if (logEntries) {
                const entry = document.createElement("div");
                entry.className = "log-entry";
                entry.innerHTML = `
                    <span class="log-time">${formatTime(timestamp)}</span>
                    <span class="log-level ${level}">${level}</span>
                    <span class="log-message">${esc(message)}</span>
                `;
                logEntries.appendChild(entry);
                logEntries.scrollTop = logEntries.scrollHeight;
            }
        }
    });

    connection.onclose(() => {
        connectionDot.classList.remove("connected");
        connectionLabel.textContent = "Disconnected";
    });

    connection.onreconnecting(() => {
        connectionDot.classList.remove("connected");
        connectionLabel.textContent = "Reconnecting...";
    });

    connection.onreconnected(() => {
        connectionDot.classList.add("connected");
        connectionLabel.textContent = "Connected";
    });

    try {
        await connection.start();
        connectionDot.classList.add("connected");
        connectionLabel.textContent = "Connected";
    } catch (err) {
        console.error("SignalR connection failed:", err);
        connectionLabel.textContent = "Connection failed";
    }
}

// ── Helpers ───────────────────────────────────────────────────────────

function esc(str) {
    if (!str) return "";
    const div = document.createElement("div");
    div.textContent = str;
    return div.innerHTML;
}

function formatTime(iso) {
    if (!iso) return "";
    const d = new Date(iso);
    return d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", second: "2-digit" });
}

// ── Init ──────────────────────────────────────────────────────────────

fetchAgents();
connectSignalR();
