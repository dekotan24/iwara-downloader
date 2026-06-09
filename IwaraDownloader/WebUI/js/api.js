const API = {
    token: localStorage.getItem('iwdl_token') || null,

    async request(path, opts = {}) {
        const headers = { ...opts.headers };
        if (this.token) headers['Authorization'] = `Bearer ${this.token}`;
        if (opts.body && typeof opts.body === 'object') {
            headers['Content-Type'] = 'application/json';
            opts.body = JSON.stringify(opts.body);
        }
        const res = await fetch(`/api${path}`, { ...opts, headers });
        if (res.status === 401) {
            this.token = null;
            localStorage.removeItem('iwdl_token');
            if (typeof App !== 'undefined') App.showLogin();
            throw new Error('Unauthorized');
        }
        if (!res.ok) {
            const err = await res.json().catch(() => ({ error: res.statusText }));
            throw new Error(err.error || res.statusText);
        }
        return res.json();
    },

    async login(username, password) {
        const data = await this.request('/auth/login', {
            method: 'POST',
            body: { username, password }
        });
        this.token = data.token;
        localStorage.setItem('iwdl_token', data.token);
        return data;
    },

    async logout() {
        try { await this.request('/auth/logout', { method: 'POST' }); } catch {}
        this.token = null;
        localStorage.removeItem('iwdl_token');
    },

    authStatus() { return this.request('/auth/status'); },

    getVideos(params = {}) {
        const q = new URLSearchParams();
        for (const [k, v] of Object.entries(params)) {
            if (v !== undefined && v !== null && v !== '') q.set(k, v);
        }
        return this.request(`/videos?${q}`);
    },

    getVideo(id) { return this.request(`/videos/${id}`); },

    streamUrl(id) { return `/api/videos/${id}/stream`; },

    thumbnailUrl(id) { return `/api/videos/${id}/thumbnail`; },

    getChannels() { return this.request('/channels'); },

    getErrors() { return this.request('/errors'); },

    retryError(id) { return this.request(`/errors/${id}/retry`, { method: 'POST' }); },

    retryAllErrors() { return this.request('/errors/retry-all', { method: 'POST' }); },

    deleteNotFound() { return this.request('/errors/delete-not-found', { method: 'POST' }); },

    queueDownload(id) { return this.request(`/downloads/${id}/queue`, { method: 'POST' }); },

    getActiveDownloads() { return this.request('/downloads/active'); },

    getStats() { return this.request('/stats'); },

    deleteBatch(ids) { return this.request('/videos/delete-batch', { method: 'POST', body: { ids } }); }
};
