const Player = {
    video: null,
    playlist: [],
    currentIndex: -1,
    isOpen: false,

    init() {
        this.video = document.getElementById('videoPlayer');
        this.video.addEventListener('ended', () => this.playNext());
        document.getElementById('btnClosePlayer').addEventListener('click', () => this.close());
        document.getElementById('btnShuffle').addEventListener('click', () => this.shuffle());

        document.addEventListener('keydown', (e) => {
            if (!this.isOpen) return;
            switch (e.key) {
                case 'Escape': this.close(); break;
                case ' ':
                    e.preventDefault();
                    this.video.paused ? this.video.play() : this.video.pause();
                    break;
                case 'ArrowLeft':
                    e.preventDefault();
                    this.video.currentTime = Math.max(0, this.video.currentTime - 10);
                    break;
                case 'ArrowRight':
                    e.preventDefault();
                    this.video.currentTime = Math.min(this.video.duration, this.video.currentTime + 10);
                    break;
                case 'ArrowUp':
                    e.preventDefault();
                    this.video.volume = Math.min(1, this.video.volume + 0.1);
                    break;
                case 'ArrowDown':
                    e.preventDefault();
                    this.video.volume = Math.max(0, this.video.volume - 0.1);
                    break;
                case 'f':
                case 'F':
                    if (!e.ctrlKey && !e.metaKey) {
                        e.preventDefault();
                        document.fullscreenElement ? document.exitFullscreen() : this.video.requestFullscreen();
                    }
                    break;
                case 'n':
                case 'N':
                    if (!e.ctrlKey && !e.metaKey) { e.preventDefault(); this.playNext(); }
                    break;
                case 'p':
                case 'P':
                    if (!e.ctrlKey && !e.metaKey) { e.preventDefault(); this.playPrev(); }
                    break;
            }
        });
    },

    open(videoData, playlist = []) {
        this.playlist = playlist.length > 0 ? playlist : [videoData];
        this.currentIndex = playlist.length > 0
            ? playlist.findIndex(v => v.id === videoData.id)
            : 0;
        if (this.currentIndex < 0) this.currentIndex = 0;

        this.loadCurrent();
        document.getElementById('playerModal').classList.remove('hidden');
        this.isOpen = true;
        this.renderPlaylist();
    },

    close() {
        this.video.pause();
        this.video.src = '';
        document.getElementById('playerModal').classList.add('hidden');
        this.isOpen = false;
    },

    loadCurrent() {
        const item = this.playlist[this.currentIndex];
        if (!item) return;

        this.video.src = API.streamUrl(item.id);
        document.getElementById('playerTitle').textContent = item.title || '無題';

        const meta = document.getElementById('playerMeta');
        meta.innerHTML = [
            item.authorUsername && `<span>${this.esc(item.authorUsername)}</span>`,
            item.durationFormatted && `<span>${item.durationFormatted}</span>`,
            item.fileSizeFormatted && `<span>${item.fileSizeFormatted}</span>`,
            item.postedAt && `<span>${new Date(item.postedAt).toLocaleDateString()}</span>`,
            item.tags && `<span>${this.esc(item.tags)}</span>`
        ].filter(Boolean).join('');

        this.video.play().catch(() => {});
        this.highlightPlaylist();
    },

    playNext() {
        if (this.currentIndex < this.playlist.length - 1) {
            this.currentIndex++;
            this.loadCurrent();
        }
    },

    playPrev() {
        if (this.video.currentTime > 3) {
            this.video.currentTime = 0;
        } else if (this.currentIndex > 0) {
            this.currentIndex--;
            this.loadCurrent();
        }
    },

    shuffle() {
        for (let i = this.playlist.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [this.playlist[i], this.playlist[j]] = [this.playlist[j], this.playlist[i]];
        }
        this.currentIndex = 0;
        this.loadCurrent();
        this.renderPlaylist();
    },

    renderPlaylist() {
        const container = document.getElementById('playlistItems');
        container.innerHTML = this.playlist.map((item, i) => `
            <div class="playlist-item ${i === this.currentIndex ? 'active' : ''}" data-index="${i}">
                <span class="pl-idx">${i + 1}</span>
                <span class="pl-title">${this.esc(item.title || '無題')}</span>
                <span class="pl-duration">${item.durationFormatted || ''}</span>
            </div>
        `).join('');

        container.querySelectorAll('.playlist-item').forEach(el => {
            el.addEventListener('click', () => {
                this.currentIndex = parseInt(el.dataset.index);
                this.loadCurrent();
            });
        });
    },

    highlightPlaylist() {
        document.querySelectorAll('.playlist-item').forEach((el, i) => {
            el.classList.toggle('active', i === this.currentIndex);
        });
        const active = document.querySelector('.playlist-item.active');
        if (active) active.scrollIntoView({ block: 'nearest' });
    },

    esc(s) {
        const d = document.createElement('div');
        d.textContent = s;
        return d.innerHTML;
    }
};
