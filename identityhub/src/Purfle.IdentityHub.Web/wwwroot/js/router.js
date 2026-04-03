// ============================================================
// Purfle IdentityHub — Client-side router
// ============================================================

const Router = {
    routes: [],
    root: document.getElementById('app'),

    add(pattern, handler) {
        this.routes.push({ pattern, handler });
        return this;
    },

    navigate(path) {
        if (path !== location.pathname) {
            history.pushState(null, '', path);
        }
        this.resolve();
    },

    resolve() {
        const path = location.pathname;
        for (const route of this.routes) {
            const match = path.match(route.pattern);
            if (match) {
                route.handler(match);
                window.scrollTo(0, 0);
                this.updateNav();
                return;
            }
        }
        this.root.innerHTML = '<div class="container"><div class="empty-state"><h3>Page not found</h3><p><a href="/">Back to home</a></p></div></div>';
    },

    updateNav() {
        document.querySelectorAll('.site-header nav a').forEach(a => {
            const href = a.getAttribute('href');
            if (href === location.pathname || (href !== '/' && location.pathname.startsWith(href))) {
                a.classList.add('active');
            } else {
                a.classList.remove('active');
            }
        });
    },

    start() {
        window.addEventListener('popstate', () => this.resolve());
        document.addEventListener('click', e => {
            const a = e.target.closest('a[href]');
            if (a && a.getAttribute('href').startsWith('/') && !a.getAttribute('href').startsWith('//') && !a.hasAttribute('data-external')) {
                e.preventDefault();
                this.navigate(a.getAttribute('href'));
            }
        });
        this.resolve();
    }
};
