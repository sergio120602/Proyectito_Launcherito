'use strict';

const svg = (path, extra = '') => `<svg viewBox="0 0 24 24" aria-hidden="true"${extra}><path fill="currentColor" d="${path}"/></svg>`;

/** Los mismos iconos que la versión de Android (Material Symbols redondeados). */
const Icons = {
    home: svg('M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z'),
    search: svg('M15.5 14h-.79l-.28-.27A6.47 6.47 0 0 0 16 9.5 6.5 6.5 0 1 0 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z'),
    close: svg('M19 6.41 17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z'),
    more: svg('M12 8c1.1 0 2-.9 2-2s-.9-2-2-2-2 .9-2 2 .9 2 2 2zm0 2c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm0 6c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z'),
    add: svg('M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z'),
    library: svg('M20 2H8c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm-2 5h-3v5.5a2.5 2.5 0 0 1-5 0 2.5 2.5 0 0 1 2.5-2.5c.57 0 1.08.19 1.5.51V5h4v2zM4 6H2v14c0 1.1.9 2 2 2h14v-2H4V6z'),
    delete: svg('M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM8 9h8v10H8V9zm7.5-5-1-1h-5l-1 1H5v2h14V4z'),
    check: svg('M9 16.17 4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z'),
    edit: svg('M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04a1 1 0 0 0 0-1.41l-2.34-2.34a1 1 0 0 0-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z'),
    grid: svg('M3 3v8h8V3H3zm6 6H5V5h4v4zm-6 4v8h8v-8H3zm6 6H5v-4h4v4zm4-16v8h8V3h-8zm6 6h-4V5h4v4zm-6 4v8h8v-8h-8zm6 6h-4v-4h4v4z'),
    square: svg('M18 4H6c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 14H6V6h12v12z'),
    play: svg('M8 5v14l11-7z'),
    pause: svg('M6 19h4V5H6v14zm8-14v14h4V5h-4z'),
    previous: svg('M6 6h2v12H6zm3.5 6 8.5 6V6z'),
    next: svg('M6 18l8.5-6L6 6v12zM16 6v12h2V6h-2z'),
    shuffle: svg('M10.59 9.17 5.41 4 4 5.41l5.17 5.17 1.42-1.41zM14.5 4l2.04 2.04L4 18.59 5.41 20 17.96 7.46 20 9.5V4h-5.5zm.33 9.41-1.41 1.41 3.13 3.13L14.5 20H20v-5.5l-2.04 2.04-3.13-3.13z'),
    note: svg('M12 3v10.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h4V3h-6z'),
    person: svg('M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z'),
    back: svg('M20 11H7.83l5.59-5.59L12 4l-8 8 8 8 1.41-1.41L7.83 13H20v-2z'),
    volume: svg('M3 9v6h4l5 5V4L7 9H3zm13.5 3A4.5 4.5 0 0 0 14 7.97v8.05c1.48-.73 2.5-2.25 2.5-4.02zM14 3.23v2.06c2.89.86 5 3.54 5 6.71s-2.11 5.85-5 6.71v2.06c4.01-.91 7-4.49 7-8.77s-2.99-7.86-7-8.77z'),
    video: svg('M20 4H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zM9.5 16.5v-9l7 4.5-7 4.5z'),
    textSize: svg('M9 4v3h5v12h3V7h5V4H9zm-6 8h3v7h3v-7h3V9H3v3z'),
    playCircle: svg('M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 14.5v-9l6 4.5-6 4.5z'),
    /** Dos rayas en diagonal: arrastrándolo cambia el tamaño. */
    grip: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M17 7 7 17M17 12l-5 5" stroke="#fff" stroke-width="2" stroke-linecap="round" fill="none"/></svg>',
    /** Tu música: un .mp3 tuyo, que suena entero. */
    ownBadge: '<svg viewBox="0 0 24 24" class="badge" aria-label="Tu .mp3"><circle cx="12" cy="12" r="12" fill="#8B5CF6"/><path fill="#fff" transform="translate(4.8 4.8) scale(.6)" d="M12 3v10.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h4V3h-6z"/></svg>',
    /** De Spotify: el círculo verde con las tres ondas negras del logo. */
    spotifyBadge: '<svg viewBox="0 0 24 24" class="badge" aria-label="De Spotify"><circle cx="12" cy="12" r="12" fill="#1DB954"/><path d="M6.5 9.6Q12 7.6 17.5 10.6M7.3 12.7Q12 11.1 16.6 13.5M8.1 15.6Q12 14.4 15.6 16.3" stroke="#000" stroke-width="1.8" stroke-linecap="round" fill="none"/></svg>',
};

function formatTime(seconds) {
    const total = Math.max(0, Math.floor(seconds || 0));
    const h = Math.floor(total / 3600);
    const m = Math.floor(total % 3600 / 60);
    const s = String(total % 60).padStart(2, '0');
    return h > 0 ? `${h}:${String(m).padStart(2, '0')}:${s}` : `${m}:${s}`;
}

function element(tag, className, html) {
    const el = document.createElement(tag);
    if (className) el.className = className;
    if (html != null) el.innerHTML = html;
    return el;
}

/**
 * La barra, los tiempos y los botones. Hay un solo juego de controles en cada vista: dentro de la
 * portada que suena (mosaico), debajo de la carátula (vista original) o abajo (artistas y géneros).
 */
class Controls {
    static all = new Set();

    constructor(vm) {
        this.vm = vm;
        this.el = element('div', 'controls', `
            <input type="range" class="seek" min="0" max="1" step="0.1" value="0" aria-label="Posición">
            <div class="times"><span class="now">0:00</span><span class="total">0:00</span></div>
            <div class="buttons">
                <button class="icon-button shuffle" type="button">${Icons.shuffle}</button>
                <button class="icon-button skip" type="button" title="Anterior" aria-label="Anterior">${Icons.previous}</button>
                <button class="play-button" type="button"></button>
                <button class="icon-button skip" type="button" title="Siguiente" aria-label="Siguiente">${Icons.next}</button>
                <span class="queue-position"></span>
            </div>`);
        this.seek = this.el.querySelector('.seek');
        this.now = this.el.querySelector('.now');
        this.total = this.el.querySelector('.total');
        this.shuffle = this.el.querySelector('.shuffle');
        this.playButton = this.el.querySelector('.play-button');
        this.queue = this.el.querySelector('.queue-position');
        const [previous, next] = this.el.querySelectorAll('.skip');

        // La canción cambia de posición una sola vez, al soltar la barra.
        this.seek.addEventListener('input', () => vm.seekDrag(+this.seek.value));
        this.seek.addEventListener('change', () => vm.seekEnd());
        this.shuffle.addEventListener('click', () => vm.toggleShuffle());
        previous.addEventListener('click', () => vm.previous());
        next.addEventListener('click', () => vm.next());
        this.playButton.addEventListener('click', () => vm.togglePlay());
        Controls.all.add(this);
        this.update();
    }

    update() {
        const vm = this.vm;
        const shown = vm.isSeeking ? vm.seekValue : vm.position;
        const max = Math.max(1, vm.duration);
        if (+this.seek.max !== max) this.seek.max = max;
        if (!vm.isSeeking || document.activeElement !== this.seek) this.seek.value = Math.min(shown, max);
        this.seek.style.setProperty('--progress', `${Math.min(100, shown / max * 100)}%`);
        setText(this.now, formatTime(shown));
        setText(this.total, formatTime(vm.duration));
        const shuffleLabel = vm.shuffle ? 'Aleatorio: activado' : 'Aleatorio: desactivado (orden alfabético)';
        if (this.shuffle.title !== shuffleLabel) {
            this.shuffle.title = shuffleLabel;
            this.shuffle.setAttribute('aria-label', shuffleLabel);
            this.shuffle.classList.toggle('on', vm.shuffle);
        }
        const playLabel = vm.isPlaying ? 'Pausar' : 'Reproducir';
        if (this.playButton.title !== playLabel) {
            this.playButton.title = playLabel;
            this.playButton.setAttribute('aria-label', playLabel);
            this.playButton.innerHTML = vm.isPlaying ? Icons.pause : Icons.play;
        }
        setText(this.queue, vm.positionText);
    }

    dispose() {
        Controls.all.delete(this);
        this.el.remove();
    }
}

/** Toda la página: lee el estado de Launcherito y lo dibuja. */
class Ui {
    constructor(vm) {
        this.vm = vm;
        this.$ = id => document.getElementById(id);
        this.frame = 0;
        this.categorySignature = null;
        this.searchSignature = null;
        this.deleteRowsShown = null;
        this.singleCoverKey = undefined;
        this.touch = matchMedia('(pointer: coarse)').matches;

        vm.onChange = () => this.schedule();
        this.mosaic = new MosaicView(vm, this.$('mosaic'));
        this.$('single-controls').appendChild(new Controls(vm).el);
        this.$('bottom-controls').appendChild(new Controls(vm).el);
        this.pictureObserver = new IntersectionObserver(entries => {
            for (const entry of entries) {
                if (entry.isIntersecting) {
                    // La foto se pide a Deezer la primera vez que se ve la tarjeta.
                    vm.requestArtistPicture(entry.target.dataset.pictureArtist);
                    this.pictureObserver.unobserve(entry.target);
                }
            }
        }, { rootMargin: '200px' });

        this.fillIcons();
        this.wire();
        this.render();
    }

    schedule() {
        if (this.frame) return;
        this.frame = requestAnimationFrame(() => {
            this.frame = 0;
            this.render();
        });
    }

    fillIcons() {
        for (const el of document.querySelectorAll('[data-icon]')) {
            el.insertAdjacentHTML('afterbegin', Icons[el.dataset.icon]);
        }
    }

    // ───────────────────────────── Eventos ─────────────────────────────

    wire() {
        const vm = this.vm;
        const $ = this.$;
        const fileInput = $('file-input');
        const loadFiles = () => fileInput.click();
        fileInput.addEventListener('change', () => {
            vm.addFiles([...fileInput.files]);
            fileInput.value = '';
        });

        // Menú principal
        $('start-back').addEventListener('click', () => vm.showPlayer());
        $('start-load').addEventListener('click', loadFiles);
        $('start-library').addEventListener('click', () => vm.loadLibrary());
        $('start-delete').addEventListener('click', () => vm.openDeleteDialog());
        $('start-spotify').addEventListener('click', () => vm.openSpotifyDialog());
        for (const button of document.querySelectorAll('[data-large-text]')) {
            button.addEventListener('click', () => vm.chooseLargeText(button.dataset.largeText === 'true'));
        }

        // Barra de arriba
        $('home').addEventListener('click', () => vm.goHome());
        const search = $('search');
        search.addEventListener('input', () => vm.setSearch(search.value));
        search.addEventListener('keydown', e => {
            if (e.key === 'Enter') {
                // Enter elige la primera.
                const first = vm.searchResults()[0];
                if (first) {
                    vm.pickSearchResult(first.key);
                    search.blur();
                }
            } else if (e.key === 'Escape') {
                e.stopPropagation();
                if (vm.searchQuery) vm.setSearch('');
                else search.blur();
            }
        });
        $('search-clear').addEventListener('click', () => {
            vm.setSearch('');
            search.focus();
        });

        const menu = $('menu');
        $('menu-button').addEventListener('click', e => {
            e.stopPropagation();
            menu.hidden = !menu.hidden;
        });
        document.addEventListener('click', e => { if (!menu.contains(e.target)) menu.hidden = true; });
        const menuAction = (id, action) => $(id).addEventListener('click', () => {
            menu.hidden = true;
            action();
        });
        menuAction('menu-load', loadFiles);
        menuAction('menu-spotify', () => vm.openSpotifyDialog());
        menuAction('menu-library', () => vm.loadLibrary());
        menuAction('menu-delete', () => vm.openDeleteDialog());
        menuAction('menu-large-text', () => vm.chooseLargeText(!vm.largeText));

        for (const tab of document.querySelectorAll('[data-section]')) {
            tab.addEventListener('click', () => vm.showSection(tab.dataset.section));
        }
        $('edit').addEventListener('click', () => vm.editMosaic(true));
        $('stop-edit').addEventListener('click', () => vm.editMosaic(false));
        $('toggle-view').addEventListener('click', () => vm.toggleMosaicView());

        // Cuadros de diálogo
        const spotifyInput = $('spotify-link');
        spotifyInput.addEventListener('input', () => { vm.spotifyLink = spotifyInput.value; });
        $('spotify-form').addEventListener('submit', e => {
            e.preventDefault();
            vm.addSpotifyList();
        });
        $('spotify-cancel').addEventListener('click', () => vm.closeSpotifyDialog());
        $('spotify-dialog').addEventListener('cancel', e => {
            e.preventDefault();
            vm.closeSpotifyDialog();
        });

        $('delete-select-all').addEventListener('click', () => vm.toggleSelectAll());
        $('delete-cancel').addEventListener('click', () => vm.closeDeleteDialog());
        $('delete-confirm').addEventListener('click', () => {
            vm.confirmDelete = true;
            vm.changed();
        });
        $('delete-dialog').addEventListener('cancel', e => {
            e.preventDefault();
            vm.closeDeleteDialog();
        });
        $('delete-list').addEventListener('change', e => {
            const row = e.target.closest('[data-key]');
            if (row) vm.toggleChecked(row.dataset.key);
        });
        $('confirm-cancel').addEventListener('click', () => {
            vm.confirmDelete = false;
            vm.changed();
        });
        $('confirm-ok').addEventListener('click', () => vm.deleteMarked());
        $('confirm-dialog').addEventListener('cancel', e => {
            e.preventDefault();
            vm.confirmDelete = false;
            vm.changed();
        });
        $('message-ok').addEventListener('click', () => {
            vm.message = null;
            vm.changed();
        });
        $('message-dialog').addEventListener('cancel', e => {
            e.preventDefault();
            vm.message = null;
            vm.changed();
        });

        // Teclado: Ctrl+F busca, Esc va deshaciendo (como el botón Atrás de Android).
        document.addEventListener('keydown', e => {
            if (document.querySelector('dialog[open]')) return;
            if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'f' && vm.playerVisible) {
                e.preventDefault();
                search.focus();
                search.select();
            } else if (e.key === 'Escape') {
                menu.hidden = true;
                vm.back();
            }
        });

        // Soltar .mp3 sobre la página los añade (como «Abrir con → Launcherito»).
        let dragDepth = 0;
        const hasFiles = e => [...(e.dataTransfer?.types ?? [])].includes('Files');
        document.addEventListener('dragenter', e => {
            if (!hasFiles(e)) return;
            dragDepth++;
            $('drop-hint').hidden = false;
        });
        document.addEventListener('dragleave', e => {
            if (hasFiles(e) && --dragDepth <= 0) {
                dragDepth = 0;
                $('drop-hint').hidden = true;
            }
        });
        document.addEventListener('dragover', e => { if (hasFiles(e)) e.preventDefault(); });
        document.addEventListener('drop', e => {
            if (!hasFiles(e)) return;
            e.preventDefault();
            dragDepth = 0;
            $('drop-hint').hidden = true;
            vm.addFiles([...e.dataTransfer.files]);
        });

        // El vídeo de la vista original sigue a su marco.
        $('single').addEventListener('scroll', () => this.schedule(), { passive: true });
        window.addEventListener('resize', () => this.schedule());
        window.addEventListener('pagehide', () => vm.saveAll());
        document.addEventListener('visibilitychange', () => { if (document.hidden) vm.saveAll(); });
    }

    // ───────────────────────────── Dibujo ─────────────────────────────

    render() {
        const vm = this.vm;
        const $ = this.$;
        document.documentElement.classList.toggle('large-text', vm.largeText);

        $('start').hidden = vm.playerVisible;
        $('player').hidden = !vm.playerVisible;
        if (!vm.playerVisible) this.renderStart();
        else this.renderPlayer();

        for (const controls of Controls.all) controls.update();
        this.renderVideoLayer();
        this.renderDialogs();
        if (vm.playerVisible) this.mosaic.render();
        else Covers.trim(new Set([vm.currentKey]));
    }

    renderStart() {
        const vm = this.vm;
        const $ = this.$;
        // Si hay algo cargado, se puede volver al reproductor.
        $('start-back').hidden = !vm.hasSongs;
        setText($('start-back-text'), vm.isPlaying ? `Volver al reproductor  ·  ${vm.title}` : 'Volver al reproductor');
        setText($('start-library-text'), vm.libraryCount > 0 ? `Tus canciones  · ${vm.libraryCount}` : 'Tus canciones');
        $('start-library').disabled = vm.libraryCount === 0;
        $('start-delete').disabled = vm.libraryCount === 0;
        $('start-hint').hidden = vm.libraryCount > 0;
        $('start-server-note').hidden = Http.server;
        for (const button of document.querySelectorAll('[data-large-text]')) {
            button.classList.toggle('selected', (button.dataset.largeText === 'true') === vm.largeText);
        }
    }

    renderPlayer() {
        const vm = this.vm;
        const $ = this.$;
        const songsSection = vm.section === Section.Songs;

        // Barra de arriba y menú
        const search = $('search');
        if (search.value !== vm.searchQuery) search.value = vm.searchQuery;
        $('search-clear').hidden = !vm.searchQuery;
        $('menu-library').hidden = vm.unloadedCount === 0;
        setText($('menu-library-text'), `Tus canciones: cargar ${vm.unloadedCount === 1 ? 'la que falta' : `las ${vm.unloadedCount} que faltan`}`);
        $('menu-delete').hidden = vm.libraryCount === 0;
        $('menu-large-text').classList.toggle('checked', vm.largeText);

        for (const tab of document.querySelectorAll('[data-section]')) {
            tab.classList.toggle('active', tab.dataset.section === vm.section);
        }

        // Avisos y botones de la vista
        setText($('status'), vm.statusText || vm.catalogStatus || songs(vm.tiles.length));
        $('edit').hidden = !vm.mosaicVisible;
        $('edit').classList.toggle('editing', vm.isEditing);
        $('stop-edit').hidden = !(vm.mosaicVisible && vm.isEditing);
        const toggle = $('toggle-view');
        toggle.hidden = !songsSection;
        const toggleText = vm.mosaicView ? 'Vista original' : 'Vista mosaico';
        if (toggle.dataset.text !== toggleText) {
            toggle.dataset.text = toggleText;
            toggle.innerHTML = (vm.mosaicView ? Icons.square : Icons.grid) + `<span>${toggleText}</span>`;
        }
        $('edit-help').hidden = !vm.isEditing;
        setText($('edit-help'), this.touch
            ? 'Mantén pulsada una portada para moverla; arrastra su esquina para cambiar el tamaño'
            : 'Arrastra una portada para moverla; arrastra su esquina para cambiar el tamaño');

        // Contenido
        $('mosaic').hidden = !vm.mosaicVisible;
        $('mosaic').classList.toggle('editing', vm.isEditing);
        $('single').hidden = !(songsSection && !vm.mosaicView);
        $('categories').hidden = songsSection;
        $('bottombar').hidden = songsSection;
        if (!$('single').hidden) this.renderSingle();
        if (!songsSection) this.renderCategories();
        else this.categorySignature = null;
        setText($('bottom-title'), vm.title);
        setText($('bottom-artist'), vm.artist);
        this.renderSearch();
    }

    /** Vista original: la carátula grande (o el vídeo de YouTube de la canción), el título y los controles. */
    renderSingle() {
        const vm = this.vm;
        const $ = this.$;
        // Marco 16:9 para el vídeo y cuadrado para la carátula.
        $('single-frame').classList.toggle('video', vm.videoShown);
        const cover = $('single-cover');
        if (this.singleCoverKey !== vm.currentKey) {
            const key = this.singleCoverKey = vm.currentKey;
            cover.hidden = true;
            cover.removeAttribute('src');
            if (key) {
                Covers.get(key).then(url => {
                    if (url && this.singleCoverKey === key) {
                        cover.src = url;
                        cover.hidden = false;
                    }
                });
            }
        }
        const status = $('video-status');
        status.hidden = !vm.videoStatus;
        setText(status, vm.videoStatus ?? '');
        setText($('single-title'), vm.title);
        setText($('single-artist'), vm.artist);
    }

    /**
     * El reproductor de YouTube no se mueve nunca de sitio en la página (se recargaría): se coloca
     * encima del marco de la vista original o se queda oculto, sonando.
     */
    renderVideoLayer() {
        const vm = this.vm;
        const layer = this.$('video-layer');
        const video = vm.songVideo;
        if (video && video.element.parentNode !== layer) layer.appendChild(video.element);
        const visible = video && vm.videoHost === VideoHost.Single && vm.videoShown && vm.singleViewVisible;
        layer.classList.toggle('shown', !!visible);
        if (visible) {
            const box = this.$('single-frame').getBoundingClientRect();
            Object.assign(layer.style, { left: `${box.left}px`, top: `${box.top}px`, width: `${box.width}px`, height: `${box.height}px` });
        } else {
            layer.removeAttribute('style');
        }
    }

    /** Pestañas Artistas y Géneros: las tarjetas o, si se ha pulsado una, sus canciones. */
    renderCategories() {
        const vm = this.vm;
        const signature = [vm.section, vm.openCategoryName, vm.metaVersion, vm.currentKey].join('\u0001');
        if (signature === this.categorySignature) return;
        this.categorySignature = signature;
        const container = this.$('categories');
        const artists = vm.section === Section.Artists;
        const scrollTop = container.scrollTop;
        container.replaceChildren();

        const open = vm.openCategoryName;
        if (open != null) {
            const songsOf = vm.categorySongs(open);
            const category = vm.categories().find(c => c.name.toLowerCase() === open.toLowerCase());
            const header = element('div', 'category-header');
            const back = element('button', 'icon-button', Icons.back);
            back.title = 'Volver';
            back.setAttribute('aria-label', 'Volver');
            back.addEventListener('click', () => vm.openCategory(null));
            const text = element('div', 'category-header-text');
            text.append(element('h2'), element('p', 'muted'));
            text.firstChild.textContent = open;
            text.lastChild.textContent = (artists ? 'Artista · ' : 'Género · ') + songs(songsOf.length);
            header.append(back, this.picture(category?.picture, artists, 'small'), text);
            const list = element('div', 'song-list');
            for (const [key, meta] of songsOf) {
                list.appendChild(this.songRow(key, meta.title, artists ? genreName(meta) : meta.artist));
            }
            container.append(header, list);
            return;
        }

        const categories = vm.categories();
        if (categories.length === 0) {
            const empty = element('p', 'empty muted');
            empty.textContent = artists ? 'Aún no hay artistas.' : 'Las canciones aparecerán aquí cuando Deezer las clasifique.';
            container.appendChild(empty);
            return;
        }
        const grid = element('div', artists ? 'category-grid artists' : 'category-grid genres');
        for (const category of categories) {
            const card = element('button', 'category-card');
            card.type = 'button';
            card.addEventListener('click', () => vm.openCategory(category.name));
            const name = element('span', 'category-name');
            name.textContent = category.name;
            const count = element('span', 'category-count muted');
            count.textContent = songs(category.count);
            card.append(this.picture(category.picture, artists), name, count);
            if (category.pictureArtist && !category.picture) {
                card.dataset.pictureArtist = category.pictureArtist;
                this.pictureObserver.observe(card);
            }
            grid.appendChild(card);
        }
        container.appendChild(grid);
        container.scrollTop = scrollTop;
    }

    /** Foto de un artista (redonda) o de un género (rectangular), con un icono mientras no hay. */
    picture(url, round, size = '') {
        const box = element('span', `picture ${round ? 'round' : 'wide'} ${size}`, round ? Icons.person : Icons.note);
        if (url) {
            const img = element('img');
            img.alt = '';
            img.loading = 'lazy';
            img.src = url;
            img.onerror = () => img.remove();
            box.appendChild(img);
        }
        return box;
    }

    /**
     * Fila de una canción (detalle de artista o género, y resultados del buscador): icono, título y un
     * dato a la derecha. La que suena sale resaltada; lo que coincide con la búsqueda, en morado.
     */
    songRow(key, title, detail, highlight = null, onClick = () => this.vm.selectSong(key)) {
        const playing = key === this.vm.currentKey;
        const row = element('button', 'song-row' + (playing ? ' playing' : ''),
            playing ? Icons.volume : SpotifyLibrary.isSpotify(key) ? Icons.video : Icons.play);
        row.type = 'button';
        const name = element('span', 'song-row-title');
        if (highlight && highlight[1] <= title.length) {
            const mark = element('mark');
            mark.textContent = title.slice(highlight[0], highlight[1]);
            name.append(title.slice(0, highlight[0]), mark, title.slice(highlight[1]));
        } else {
            name.textContent = title;
        }
        row.appendChild(name);
        if (detail) {
            const extra = element('span', 'song-row-detail muted');
            extra.textContent = detail;
            row.appendChild(extra);
        }
        row.addEventListener('click', onClick);
        return row;
    }

    /** Resultados del buscador, encima de lo que se esté viendo. */
    renderSearch() {
        const vm = this.vm;
        const panel = this.$('search-results');
        const query = vm.searchQuery.trim();
        panel.hidden = !query;
        if (!query) {
            this.searchSignature = null;
            return;
        }
        const signature = [query, vm.metaVersion, vm.tiles.length, vm.libraryCount, vm.currentKey].join('\u0001');
        if (signature === this.searchSignature) return;
        this.searchSignature = signature;
        panel.replaceChildren();
        const results = vm.searchResults();
        if (results.length === 0) {
            const none = element('p', 'muted search-none');
            none.textContent = `Ninguna canción se llama «${query}»`;
            panel.appendChild(none);
            return;
        }
        for (const result of results) {
            panel.appendChild(this.songRow(result.key, result.title, result.artist, result.highlight, () => {
                this.$('search').blur();
                vm.pickSearchResult(result.key);
            }));
        }
    }

    renderDialogs() {
        const vm = this.vm;
        const $ = this.$;

        // Lista de Spotify
        const spotify = $('spotify-dialog');
        this.toggleDialog(spotify, vm.spotifyDialogOpen);
        if (vm.spotifyDialogOpen) {
            const input = $('spotify-link');
            if (input.value !== vm.spotifyLink) input.value = vm.spotifyLink;
            input.disabled = vm.spotifyBusy;
            $('spotify-add').disabled = vm.spotifyBusy;
            $('spotify-cancel').disabled = vm.spotifyBusy;
            $('spotify-progress').hidden = !vm.spotifyBusy;
            $('spotify-status').hidden = !vm.spotifyDialogStatus;
            setText($('spotify-status'), vm.spotifyDialogStatus);
            $('spotify-server-note').hidden = Http.server;
        }

        // Borrar canciones
        const rows = vm.deleteRows;
        this.toggleDialog($('delete-dialog'), rows != null && !vm.confirmDelete);
        if (rows) {
            const list = $('delete-list');
            if (this.deleteRowsShown !== rows) {
                this.deleteRowsShown = rows;
                list.replaceChildren();
                for (const row of rows) {
                    const label = element('label', 'delete-row');
                    label.dataset.key = row.key;
                    const box = element('input');
                    box.type = 'checkbox';
                    const text = element('span', 'delete-row-text');
                    text.textContent = row.title;
                    if (row.artist) {
                        const artist = element('span', 'muted');
                        artist.textContent = '  ·  ' + row.artist;
                        text.appendChild(artist);
                    }
                    // Marca a la derecha: de dónde viene, o que el archivo ya no está.
                    const origin = element('span', 'delete-origin ' + (row.missing ? 'missing' : row.isSpotify ? 'spotify' : ''));
                    origin.textContent = row.missing ? 'no se encuentra' : row.isSpotify ? 'Spotify' : '.mp3';
                    label.append(box, text, origin);
                    list.appendChild(label);
                }
            }
            for (const label of list.children) label.firstChild.checked = vm.deleteChecked.has(label.dataset.key);
            const marked = vm.deleteChecked.size;
            setText($('delete-select-all'), marked > 0 && marked === rows.length ? 'Quitar la selección' : 'Seleccionar todas');
            $('delete-confirm').disabled = marked === 0;
            setText($('delete-confirm'), marked === 0 ? 'Borrar' : `Borrar ${songs(marked)}`);
        } else {
            this.deleteRowsShown = null;
        }

        this.toggleDialog($('confirm-dialog'), vm.confirmDelete);
        if (vm.confirmDelete) setText($('confirm-text'), vm.deleteQuestion);

        this.toggleDialog($('message-dialog'), vm.message != null);
        if (vm.message != null) {
            setText($('message-title'), vm.messageTitle);
            setText($('message-text'), vm.message);
        }
    }

    toggleDialog(dialog, open) {
        if (open && !dialog.open) dialog.showModal();
        else if (!open && dialog.open) dialog.close();
    }
}

async function main() {
    await FileStore.open();
    await Http.detectServer();
    document.title = `Launcherito ${VERSION}`;
    const vm = new Launcherito();
    window.launcherito = new Ui(vm);
}

main();
