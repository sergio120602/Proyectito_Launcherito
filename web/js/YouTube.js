'use strict';

/**
 * Vídeos de YouTube de las canciones: las de Spotify (que no se pueden escuchar enteras dentro de
 * Launcherito) y las de los .mp3 en la vista original. Se busca "artista título" en YouTube (sin
 * clave) y se toman los primeros vídeos. Necesita el servidor de Launcherito: el navegador no deja
 * que una página lea la búsqueda de YouTube.
 */
const YouTubeLinks = {
    MAX_VIDEOS: 5,   // si el primero no se deja ver dentro de otra web, se prueban los siguientes
    videos: new Map(),   // clave de la canción → ids
    videoPattern: /"videoId":"([A-Za-z0-9_-]{11})"/g,

    /**
     * Ids de los primeros vídeos que da YouTube para la canción, en orden; vacío si no hay conexión
     * o no hay servidor. Se recuerdan mientras la página está abierta.
     */
    async findVideos(key, artist, title) {
        const known = this.videos.get(key);
        if (known) return known;
        if (!Http.server) return [];
        // Sin artista conocido se busca solo por el título.
        const query = (artist === SongTags.UNKNOWN_ARTIST ? '' : artist + ' ') + cleanTitle(title);
        try {
            const html = await Http.viaServer('https://www.youtube.com/results?search_query=' + encodeURIComponent(query), 8000);
            const ids = [...new Set([...html.matchAll(this.videoPattern)].map(m => m[1]))].slice(0, this.MAX_VIDEOS);
            if (ids.length) this.videos.set(key, ids);
            return ids;
        } catch {
            return [];   // sin conexión o YouTube no responde
        }
    },
};

/** Carga una sola vez la API de reproductor de YouTube (https://www.youtube.com/iframe_api). */
const YouTubeApi = {
    promise: null,

    load() {
        if (window.YT?.Player) return Promise.resolve();
        this.promise ??= new Promise((resolve, reject) => {
            const previous = window.onYouTubeIframeAPIReady;
            window.onYouTubeIframeAPIReady = () => {
                previous?.();
                resolve();
            };
            const script = document.createElement('script');
            script.src = 'https://www.youtube.com/iframe_api';
            script.onerror = () => {
                this.promise = null;   // se reintentará con la siguiente canción
                script.remove();
                reject(new Error('Sin conexión con YouTube'));
            };
            document.head.appendChild(script);
        });
        return this.promise;
    },
};

/**
 * Reproductor de YouTube sin controles de YouTube: lo manejan los controles de Launcherito con play,
 * pause, seek y load, y avisa del tiempo (onProgress) y de cuándo termina (onEnded). Es la misma
 * página que en Windows y Android, pero aquí ya estamos en una web. Su elemento no se cambia nunca
 * de sitio en la página (un iframe que se mueve se recarga): la interfaz lo coloca encima de la
 * vista original o lo deja oculto.
 */
class YouTubeVideo {
    constructor() {
        this.element = document.createElement('div');
        this.element.className = 'song-video';
        this.slot = document.createElement('div');
        this.element.appendChild(this.slot);

        this.player = null;
        this.ready = false;
        this.wantPlay = true;
        this.waiting = false;
        this.ids = [];
        this.index = 0;
        this.start = 0;
        this.shown = null;
        this.disposed = false;
        this.timer = null;

        /** No se puede ver ninguno de los vídeos dentro de la página (o no hay conexión). */
        this.onUnavailable = null;
        /** Empieza a sonar el vídeo (también después de cada pausa o salto). */
        this.onPlaying = null;
        /** Ha llegado al final. */
        this.onEnded = null;
        /** Cada 250 ms: segundo actual y duración (0 mientras no se conoce). */
        this.onProgress = null;
    }

    /** Carga los vídeos (prueba el siguiente si uno no se deja insertar), empezando en el segundo start. */
    begin(ids, start) {
        this.ids = ids;
        this.index = 0;
        this.start = start;
        this.shown = ids[0];
        YouTubeApi.load().then(() => this.create(), () => this.onUnavailable?.());
    }

    create() {
        if (this.disposed) return;
        this.player = new YT.Player(this.slot, {
            videoId: this.ids[0],
            width: '100%',
            height: '100%',
            playerVars: {
                autoplay: 0, playsinline: 1, rel: 0, controls: 0, disablekb: 1, fs: 0, iv_load_policy: 3,
                start: Math.floor(this.start), origin: location.origin.startsWith('http') ? location.origin : undefined,
            },
            events: {
                onReady: () => {
                    this.ready = true;
                    // No arranca solo: suena si Launcherito no lo ha pausado mientras cargaba. Si
                    // mientras cargaba se ha pedido otra canción, se pone esa.
                    if (this.shown !== this.ids[this.index]) this.openVideo(this.ids[this.index]);
                    else if (this.wantPlay && !this.waiting) this.player.playVideo();
                },
                onStateChange: e => {
                    if (e.data === 1) this.onPlaying?.();
                    else if (e.data === 0) this.onEnded?.();
                },
                // 101/150: el dueño no deja verlo fuera de YouTube; se prueba el siguiente resultado.
                onError: () => {
                    if (++this.index < this.ids.length) this.openVideo(this.ids[this.index]);
                    else this.onUnavailable?.();
                },
            },
        });
        this.timer = setInterval(() => {
            if (this.ready && !this.waiting && this.player.getCurrentTime) {
                this.onProgress?.(this.player.getCurrentTime() || 0, this.player.getDuration() || 0);
            }
        }, 250);
    }

    openVideo(id) {
        this.shown = id;
        const video = { videoId: id, startSeconds: this.start };
        if (this.wantPlay) this.player.loadVideoById(video);
        else this.player.cueVideoById(video);
    }

    /** Cambia a otros vídeos (otra canción) sin crear otro reproductor. */
    load(ids, start) {
        this.waiting = false;
        this.ids = ids;
        this.index = 0;
        this.start = start;
        if (this.ready) this.openVideo(ids[0]);
    }

    /** Para el vídeo actual hasta el próximo load (mientras se busca el de otra canción). */
    hold() {
        this.waiting = true;
        if (this.ready) this.player.stopVideo();
    }

    play() {
        this.wantPlay = true;
        if (this.ready && !this.waiting) this.player.playVideo();
    }

    pause() {
        this.wantPlay = false;
        if (this.ready) this.player.pauseVideo();
    }

    seek(seconds) {
        if (this.ready) this.player.seekTo(seconds, true);
    }

    /** Libera el reproductor (ocupa bastante memoria). */
    dispose() {
        this.disposed = true;
        this.onUnavailable = this.onPlaying = this.onEnded = this.onProgress = null;
        clearInterval(this.timer);
        try {
            this.player?.destroy();
        } catch {
            // ya estaba roto
        }
        this.element.remove();
    }
}
