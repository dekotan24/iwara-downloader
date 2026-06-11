const App = {
    currentView: 'all',
    currentChannel: null,
    viewMode: 'list',
    currentPage: 1,
    searchQuery: '',
    sortField: 'added',
    sortOrder: 'desc',
    channels: [],
    lastVideos: null,

    async init() {
        Player.init();
        this.bindEvents();

        try {
            const status = await API.authStatus();
            if (status.needsAuth && !status.authenticated) {
                this.showLogin();
            } else {
                this.showApp();
            }
        } catch {
            this.showApp();
        }
    },

    showLogin() {
        document.getElementById('loginScreen').classList.remove('hidden');
        document.getElementById('app').classList.add('hidden');
    },

    showApp() {
        document.getElementById('loginScreen').classList.add('hidden');
        document.getElementById('app').classList.remove('hidden');
        this.loadChannels();
        this.loadVideos();
    },

    bindEvents() {
        document.getElementById('loginForm').addEventListener('submit', async (e) => {
            e.preventDefault();
            const user = document.getElementById('loginUser').value;
            const pass = document.getElementById('loginPass').value;
            const errEl = document.getElementById('loginError');
            errEl.classList.add('hidden');
            try {
                await API.login(user, pass);
                this.showApp();
            } catch (err) {
                errEl.textContent = err.message || 'ログインに失敗しました';
                errEl.classList.remove('hidden');
            }
        });

        document.getElementById('btnLogout').addEventListener('click', async () => {
            await API.logout();
            this.showLogin();
        });

        // Navigation
        document.querySelectorAll('.nav-item[data-view]').forEach(el => {
            el.addEventListener('click', () => {
                this.currentChannel = null;
                this.currentPage = 1;
                this.setView(el.dataset.view);
            });
        });

        // Search
        let searchTimer;
        document.getElementById('searchInput').addEventListener('input', (e) => {
            clearTimeout(searchTimer);
            searchTimer = setTimeout(() => {
                this.searchQuery = e.target.value.trim();
                this.currentPage = 1;
                this.loadVideos();
            }, 400);
        });

        // Sort
        document.getElementById('sortSelect').addEventListener('change', (e) => {
            const [field, order] = e.target.value.split('-');
            this.sortField = field;
            this.sortOrder = order;
            this.currentPage = 1;
            this.loadVideos();
        });

        // View mode
        document.getElementById('btnViewGrid').addEventListener('click', () => this.setViewMode('grid'));
        document.getElementById('btnViewList').addEventListener('click', () => this.setViewMode('list'));

        // Sidebar toggle
        document.getElementById('btnToggleSidebar').addEventListener('click', () => this.toggleSidebar());
        document.getElementById('btnMobileMenu').addEventListener('click', () => {
            const sidebar = document.getElementById('sidebar');
            if (window.innerWidth > 768 && sidebar.classList.contains('collapsed')) {
                sidebar.classList.remove('collapsed');
            } else {
                this.toggleSidebar();
            }
        });
        document.getElementById('sidebarOverlay').addEventListener('click', () => this.toggleSidebar(false));
    },

    setView(view) {
        this.currentView = view;
        document.querySelectorAll('.nav-item').forEach(el => {
            el.classList.toggle('active', el.dataset.view === view && !this.currentChannel);
        });

        const container = document.getElementById('videoContainer');
        const statsView = document.getElementById('statsView');
        const pagination = document.getElementById('pagination');
        const filterBar = document.getElementById('filterBar');

        if (view === 'stats') {
            container.classList.add('hidden');
            pagination.classList.add('hidden');
            filterBar.classList.add('hidden');
            statsView.classList.remove('hidden');
            this.loadStats();
        } else if (view === 'downloads') {
            container.classList.remove('hidden');
            pagination.classList.add('hidden');
            filterBar.classList.remove('hidden');
            statsView.classList.add('hidden');
            this.loadActiveDownloads();
        } else {
            container.classList.remove('hidden');
            pagination.classList.remove('hidden');
            filterBar.classList.remove('hidden');
            statsView.classList.add('hidden');
            this.loadVideos();
        }

        this.closeSidebar();
    },

    setViewMode(mode) {
        this.viewMode = mode;
        document.getElementById('btnViewGrid').classList.toggle('active', mode === 'grid');
        document.getElementById('btnViewList').classList.toggle('active', mode === 'list');
        this.renderVideos(this.lastVideos);
    },

    toggleSidebar(force) {
        const sidebar = document.getElementById('sidebar');
        const overlay = document.getElementById('sidebarOverlay');
        const isMobile = window.innerWidth <= 768;

        if (isMobile) {
            const isOpen = force !== undefined ? force : !sidebar.classList.contains('open');
            sidebar.classList.toggle('open', isOpen);
            overlay.classList.toggle('hidden', !isOpen);
        } else {
            const shouldCollapse = force !== undefined ? !force : !sidebar.classList.contains('collapsed');
            sidebar.classList.toggle('collapsed', shouldCollapse);
        }
    },

    closeSidebar() {
        const isMobile = window.innerWidth <= 768;
        if (isMobile) {
            const sidebar = document.getElementById('sidebar');
            const overlay = document.getElementById('sidebarOverlay');
            sidebar.classList.remove('open');
            overlay.classList.add('hidden');
        }
    },

    async loadChannels() {
        try {
            const data = await API.getChannels();
            this.channels = data.channels;
            const list = document.getElementById('channelList');
            list.innerHTML = data.channels.map(ch => `
                <div class="nav-item channel-item" data-channel-id="${ch.id}">
                    <span class="nav-icon">&#128100;</span>
                    <span style="flex:1;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">${this.esc(ch.username)}</span>
                    <span style="font-size:11px;color:var(--text-muted)">${ch.totalVideos}</span>
                </div>
            `).join('');

            list.querySelectorAll('.channel-item').forEach(el => {
                el.addEventListener('click', () => {
                    this.currentChannel = parseInt(el.dataset.channelId);
                    this.currentPage = 1;
                    const ch = this.channels.find(c => c.id === this.currentChannel);
                    document.getElementById('viewTitle').textContent = ch ? ch.username : 'チャンネル';
                    // setView 経由で統計/DLビューの表示状態をリセットする
                    // (直接 loadVideos すると統計ビューの残骸が画面に残る)
                    this.setView('channel');
                    document.querySelectorAll('.nav-item').forEach(n => n.classList.remove('active'));
                    el.classList.add('active');
                });
            });
        } catch (err) {
            console.error('Failed to load channels:', err);
        }
    },

    async loadVideos() {
        const container = document.getElementById('videoContainer');
        container.innerHTML = '<div class="loading">読み込み中</div>';

        const params = {
            page: this.currentPage,
            limit: 50,
            sort: this.sortField,
            order: this.sortOrder
        };

        if (this.searchQuery) params.search = this.searchQuery;
        if (this.currentChannel) params.channel = this.currentChannel;

        let viewTitle = '全ての動画';
        if (this.currentView === 'downloaded') {
            params.status = 'downloaded';
            viewTitle = 'DL済み';
        } else if (this.currentView === 'favorites') {
            params.favorite = 1;
            viewTitle = 'お気に入り';
        } else if (this.currentView === 'errors') {
            params.status = 3; // Failed
            viewTitle = 'エラー';
        } else if (this.currentChannel) {
            const ch = this.channels.find(c => c.id === this.currentChannel);
            viewTitle = ch ? ch.username : 'チャンネル';
        }

        if (this.currentView !== 'channel') {
            document.getElementById('viewTitle').textContent = viewTitle;
        }

        try {
            const data = await API.getVideos(params);
            this.lastVideos = data;
            document.getElementById('videoCount').textContent = `${data.total} 件`;
            this.renderVideos(data);
            this.renderPagination(data);
            this.renderFilterActions();
        } catch (err) {
            container.innerHTML = `<div class="loading" style="color:var(--danger)">${this.esc(err.message)}</div>`;
        }
    },

    renderVideos(data) {
        if (!data) return;
        const container = document.getElementById('videoContainer');
        container.className = this.viewMode === 'grid' ? 'video-grid' : 'video-list';

        if (data.items.length === 0) {
            container.innerHTML = '<div class="loading" style="animation:none">動画が見つかりません</div>';
            return;
        }

        container.innerHTML = data.items.map(v => this.renderVideoItem(v)).join('');

        container.querySelectorAll('.video-item').forEach(el => {
            el.addEventListener('click', (e) => {
                if (e.target.closest('.video-actions') || e.target.closest('.fav-star')) return;
                const id = parseInt(el.dataset.id);
                const video = data.items.find(v => v.id === id);
                if (video && video.hasFile) {
                    const playable = data.items.filter(v => v.hasFile);
                    Player.open(video, playable);
                }
            });
        });

        container.querySelectorAll('.fav-star').forEach(btn => {
            btn.addEventListener('click', async (e) => {
                e.stopPropagation();
                const id = parseInt(btn.dataset.id);
                const item = data.items.find(v => v.id === id);
                try {
                    const r = await API.setFavorite(id, !(item && item.isFavorite));
                    if (item) item.isFavorite = r.favorite;
                    btn.classList.toggle('active', r.favorite);
                    btn.innerHTML = r.favorite ? '&#9733;' : '&#9734;';
                    btn.title = r.favorite ? 'お気に入り解除' : 'お気に入りに追加';
                    // お気に入りビューで解除したらリストから消す
                    if (this.currentView === 'favorites' && !r.favorite) this.loadVideos();
                } catch {}
            });
        });

        container.querySelectorAll('.btn-retry').forEach(btn => {
            btn.addEventListener('click', async (e) => {
                e.stopPropagation();
                const id = parseInt(btn.dataset.id);
                try {
                    await API.retryError(id);
                    btn.textContent = '追加済み';
                    btn.disabled = true;
                } catch {}
            });
        });

        container.querySelectorAll('.btn-queue').forEach(btn => {
            btn.addEventListener('click', async (e) => {
                e.stopPropagation();
                const id = parseInt(btn.dataset.id);
                try {
                    await API.queueDownload(id);
                    btn.textContent = '追加済み';
                    btn.disabled = true;
                } catch {}
            });
        });
    },

    renderVideoItem(v) {
        const statusClass = {
            'Completed': 'status-completed',
            'Failed': 'status-failed',
            'Pending': 'status-pending',
            'Downloading': 'status-downloading',
            'Skipped': 'status-skipped',
            'WritingTags': 'status-downloading',
            'Paused': 'status-pending'
        }[v.statusText] || '';

        const statusLabel = {
            'Completed': '完了',
            'Failed': '失敗',
            'Pending': '待機',
            'Downloading': 'DL中',
            'Skipped': 'スキップ',
            'WritingTags': 'タグ書込',
            'Paused': '一時停止'
        }[v.statusText] || v.statusText;

        const thumb = `<img src="${API.thumbnailUrl(v.id)}" loading="lazy" alt="" onerror="this.parentElement.innerHTML='<div class=\\'no-thumb\\'>&#127909;</div>'">`;

        const actions = [];
        if (v.status === 3) actions.push(`<button class="btn-primary btn-retry" data-id="${v.id}">再試行</button>`);
        if (v.status === 3 || v.status === 4) actions.push(`<button class="btn-primary btn-queue" data-id="${v.id}">ダウンロード</button>`);

        const errorLine = v.lastErrorMessage
            ? `<div class="error-message" title="${this.esc(v.lastErrorMessage)}">${this.esc(v.lastErrorMessage)}</div>`
            : '';

        return `
            <div class="video-item" data-id="${v.id}" ${v.hasFile ? '' : 'style="opacity:0.7"'}>
                <div class="video-thumb">
                    ${thumb}
                    ${v.durationFormatted ? `<span class="thumb-duration">${v.durationFormatted}</span>` : ''}
                    <button class="fav-star ${v.isFavorite ? 'active' : ''}" data-id="${v.id}"
                        title="${v.isFavorite ? 'お気に入り解除' : 'お気に入りに追加'}">${v.isFavorite ? '&#9733;' : '&#9734;'}</button>
                </div>
                <div class="video-info">
                    <div class="video-title" title="${this.esc(v.title)}">${this.esc(v.title || '無題')}</div>
                    <div class="video-meta">
                        ${v.authorUsername ? `<span>${this.esc(v.authorUsername)}</span>` : ''}
                        ${v.fileSizeFormatted && v.fileSize > 0 ? `<span>${v.fileSizeFormatted}</span>` : ''}
                        ${v.postedAt ? `<span>${new Date(v.postedAt).toLocaleDateString()}</span>` : ''}
                        <span class="video-status ${statusClass}">${statusLabel}</span>
                    </div>
                    ${errorLine}
                </div>
                <div class="video-actions">
                    ${actions.join('')}
                </div>
            </div>
        `;
    },

    renderPagination(data) {
        const container = document.getElementById('pagination');
        if (data.totalPages <= 1) { container.innerHTML = ''; return; }

        let html = '';
        html += `<button ${data.page <= 1 ? 'disabled' : ''} data-page="${data.page - 1}">&laquo;</button>`;

        const start = Math.max(1, data.page - 3);
        const end = Math.min(data.totalPages, data.page + 3);

        if (start > 1) html += `<button data-page="1">1</button>`;
        if (start > 2) html += `<span style="color:var(--text-muted)">...</span>`;

        for (let i = start; i <= end; i++) {
            html += `<button class="${i === data.page ? 'active' : ''}" data-page="${i}">${i}</button>`;
        }

        if (end < data.totalPages - 1) html += `<span style="color:var(--text-muted)">...</span>`;
        if (end < data.totalPages) html += `<button data-page="${data.totalPages}">${data.totalPages}</button>`;

        html += `<button ${data.page >= data.totalPages ? 'disabled' : ''} data-page="${data.page + 1}">&raquo;</button>`;

        container.innerHTML = html;
        container.querySelectorAll('button[data-page]').forEach(btn => {
            btn.addEventListener('click', () => {
                this.currentPage = parseInt(btn.dataset.page);
                this.loadVideos();
                document.getElementById('videoContainer').scrollTop = 0;
            });
        });
    },

    renderFilterActions() {
        const actions = document.getElementById('filterActions');
        if (this.currentView === 'errors') {
            actions.innerHTML = `
                <button class="btn-primary" id="btnRetryAll">全て再試行</button>
                <button class="btn-danger" id="btnDeleteNotFound">Not Found を削除</button>
            `;
            document.getElementById('btnRetryAll').addEventListener('click', async () => {
                if (!confirm('失敗した動画を全て再試行しますか?')) return;
                try {
                    const r = await API.retryAllErrors();
                    alert(`${r.retriedCount} 件を再試行キューに追加しました`);
                    this.loadVideos();
                } catch (err) { alert(err.message); }
            });
            document.getElementById('btnDeleteNotFound').addEventListener('click', async () => {
                if (!confirm('Not Found (削除済み) の動画を全てデータベースから削除しますか?')) return;
                try {
                    const r = await API.deleteNotFound();
                    alert(`${r.deletedCount} 件を削除しました`);
                    this.loadVideos();
                } catch (err) { alert(err.message); }
            });
        } else {
            actions.innerHTML = '';
        }
    },

    async loadStats() {
        const view = document.getElementById('statsView');
        view.innerHTML = '<div class="loading">統計を読み込み中</div>';
        try {
            const s = await API.getStats();
            view.innerHTML = `
                <div class="stats-grid">
                    <div class="stat-card">
                        <div class="stat-value">${s.totalVideos}</div>
                        <div class="stat-label">総動画数</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-value">${s.downloadedVideos}</div>
                        <div class="stat-label">DL済み</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-value">${s.failedVideos}</div>
                        <div class="stat-label">失敗</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-value">${s.pendingVideos}</div>
                        <div class="stat-label">待機</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-value">${s.skippedVideos}</div>
                        <div class="stat-label">スキップ</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-value">${s.totalChannels}</div>
                        <div class="stat-label">チャンネル (有効 ${s.enabledChannels})</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-value">${s.totalSizeFormatted}</div>
                        <div class="stat-label">合計サイズ</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-value">${s.favoriteCount}</div>
                        <div class="stat-label">お気に入り</div>
                    </div>
                </div>
                <h3 style="margin-bottom:12px">最近のダウンロード</h3>
                <div class="video-list" id="recentDownloadsList">
                    ${s.recentDownloads.map(v => this.renderVideoItem(v)).join('')}
                </div>
            `;
            const list = document.getElementById('recentDownloadsList');
            list.querySelectorAll('.video-item').forEach(el => {
                el.addEventListener('click', (e) => {
                    if (e.target.closest('.video-actions') || e.target.closest('.fav-star')) return;
                    const id = parseInt(el.dataset.id);
                    const video = s.recentDownloads.find(v => v.id === id);
                    if (video && video.hasFile) {
                        const playable = s.recentDownloads.filter(v => v.hasFile);
                        Player.open(video, playable);
                    }
                });
            });
        } catch (err) {
            view.innerHTML = `<div class="loading" style="color:var(--danger)">${this.esc(err.message)}</div>`;
        }
    },

    async loadActiveDownloads() {
        const container = document.getElementById('videoContainer');
        container.innerHTML = '<div class="loading">読み込み中</div>';
        try {
            const data = await API.getActiveDownloads();
            document.getElementById('viewTitle').textContent = 'ダウンロード状況';
            document.getElementById('videoCount').textContent =
                `DL中 ${data.downloading || 0} / 待機 ${data.pending || 0}`;

            container.innerHTML = `
                <div class="stats-grid" style="margin-bottom:16px">
                    <div class="stat-card">
                        <div class="stat-value">${data.downloading || 0}</div>
                        <div class="stat-label">DL中</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-value">${data.writingTags || 0}</div>
                        <div class="stat-label">タグ書込中</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-value">${data.pending || 0}</div>
                        <div class="stat-label">待機</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-value">${data.isRunning ? '稼働中' : '停止中'}</div>
                        <div class="stat-label">マネージャー状態</div>
                    </div>
                </div>
                <p style="color:var(--text-secondary);font-size:13px">
                    ダウンロード進捗の詳細はデスクトップアプリで確認できます。
                    このページではキュー全体の状況を確認できます。
                </p>
            `;
        } catch (err) {
            container.innerHTML = `<div class="loading" style="color:var(--danger)">${this.esc(err.message)}</div>`;
        }
    },

    esc(s) {
        if (!s) return '';
        const d = document.createElement('div');
        d.textContent = s;
        return d.innerHTML;
    }
};

document.addEventListener('DOMContentLoaded', () => App.init());
