// ============================================================
// Purfle IdentityHub — App bootstrap
// ============================================================

Router
    .add(/^\/$/, () => renderHomePage())
    .add(/^\/agents$/, () => renderAgentsPage())
    .add(/^\/agents\/(.+)$/, m => renderAgentDetailPage(m[1]))
    .add(/^\/publishers\/(.+)$/, m => renderPublisherPage(m[1]))
    .add(/^\/keys\/(.+)$/, m => renderKeyLookupPage(m[1]))
    .add(/^\/admin$/, () => renderAdminDashboard())
    .add(/^\/admin\/agents$/, () => renderAdminAgents())
    .add(/^\/admin\/publishers$/, () => renderAdminPublishers())
    .add(/^\/admin\/keys$/, () => renderAdminKeys())
    .add(/^\/admin\/attestations$/, () => renderAdminAttestations())
    .start();
