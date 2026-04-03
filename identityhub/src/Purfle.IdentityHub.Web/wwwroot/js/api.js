// ============================================================
// Purfle IdentityHub — API client
// ============================================================

const API = {
    adminToken: sessionStorage.getItem('purfle_admin_token') || '',

    async get(url) {
        const resp = await fetch(url);
        if (!resp.ok) throw new Error(`GET ${url} failed: ${resp.status}`);
        return resp.json();
    },

    async adminGet(url) {
        const resp = await fetch(url, {
            headers: { 'Authorization': `Bearer ${this.adminToken}` }
        });
        if (resp.status === 401) { this.adminToken = ''; sessionStorage.removeItem('purfle_admin_token'); throw new Error('Unauthorized'); }
        if (!resp.ok) throw new Error(`GET ${url} failed: ${resp.status}`);
        return resp.json();
    },

    async adminPost(url, body) {
        const resp = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${this.adminToken}` },
            body: JSON.stringify(body)
        });
        if (resp.status === 401) { this.adminToken = ''; sessionStorage.removeItem('purfle_admin_token'); throw new Error('Unauthorized'); }
        if (!resp.ok) throw new Error(`POST ${url} failed: ${resp.status}`);
        return resp.json().catch(() => ({}));
    },

    async adminDelete(url) {
        const resp = await fetch(url, {
            method: 'DELETE',
            headers: { 'Authorization': `Bearer ${this.adminToken}` }
        });
        if (resp.status === 401) { this.adminToken = ''; sessionStorage.removeItem('purfle_admin_token'); throw new Error('Unauthorized'); }
        return resp;
    },

    setAdminToken(token) {
        this.adminToken = token;
        sessionStorage.setItem('purfle_admin_token', token);
    },

    isAdmin() {
        return !!this.adminToken;
    }
};
