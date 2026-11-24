'use strict';

/** Versión de la página web (la misma que el archivo VERSION del proyecto). */
const VERSION = '1.11';

const Section = Object.freeze({ Songs: 'Songs', Artists: 'Artists', Genres: 'Genres' });

/** Dónde va el vídeo de la canción: a la vista en la vista original, u oculto (solo se oye). */
const VideoHost = Object.freeze({ Single: 'Single', Hidden: 'Hidden' });

const UNCLASSIFIED = 'Sin clasificar';
const MAX_SEARCH_RESULTS = 50;

/** Una portada del mosaico. baseSpan: celdas de lado elegidas por el usuario o automáticas. */
class Tile {
    constructor(key) {
        this.key = key;
        this.baseSpan = 1;
        this.isSpotify = SpotifyLibrary.isSpotify(key);
    }
}

function songs(count) {
    return count === 1 ? '1 canción' : `${count} canciones`;
}

/** Texto sin mayúsculas ni tildes y con la misma longitud, para buscar y resaltar lo encontrado. */
function fold(text) {
    let out = '';
    for (const c of text) out += c.normalize('NFD')[0].toLowerCase();
    return out;
}

function genreName(meta) {
    return meta.genre?.name ?? UNCLASSIFIED;
}

/**
 * Toda la lógica de la página (lo que en Windows hace MainWindow.xaml.cs y en Android
 * LauncheritoViewModel). La interfaz (Ui.js) lee estos campos y se redibuja cuando se llama a changed.
 */
class Launcherito {
    constructor() {
        this.audio = new Audio();
        this.audio.preload = 'auto';
        this.audioUrl = null;
        this.audioVersion = 0;
        this.playlist = new Playlist(key => SongTags.fallbackTitle(key));
        this.library = new SongLibrary();
        this.catalog = new MusicCatalog();
        this.arrangement = new MosaicArrangement();
        this.onChange = null;

        /** Letra grande o pequeña. Se recuerda. */
        this.largeText = Settings.read('letraGrande', false) === true;

        // ─────────────────────────── Estado de la pantalla ───────────────────────────
        this.playerVisible = false;   // false: menú principal. true: reproductor.
        this.section = Section.Songs;
        this.mosaicView = false;
        this.isEditing = false;

        /** Las portadas en el orden del mosaico. */
        this.tiles = [];
        this.tileByKey = new Map();

        this.currentKey = null;
        this.isPlaying = false;
        this.title = '';
        this.artist = '';
        this.shuffle = this.playlist.shuffle;
        this.positionText = '';
        this.position = 0;   // segundos
        this.duration = 0;
        this.isSeeking = false;
        this.seekValue = 0;

        this.statusText = '';        // avisos de Spotify y del vídeo
        this.statusClear = null;
        this.catalogStatus = '';     // progreso de la clasificación con Deezer

        this.libraryCount = this.library.count;
        this.unloadedCount = 0;

        /** Mensaje que se muestra en un cuadro (null: ninguno). */
        this.message = null;
        this.messageTitle = 'Launcherito';

        // Artistas y géneros
        this.meta = new Map();            // clave → { title, artist, genre, classified }
        this.metaVersion = 0;             // cambia cada vez que cambian meta o las fotos (para redibujar)
        this.artistPictures = new Map();
        this.picturesRequested = new Set();
        this.openCategoryName = null;
        this.pending = [];                // canciones por clasificar con Deezer
        this.classifying = false;

        this.localSongs = new Map();      // clave artista|título → .mp3 cargado

        // Lista de Spotify
        this.spotifyDialogOpen = false;
        this.spotifyLink = '';
        this.spotifyDialogStatus = '';
        this.spotifyBusy = false;

        // Borrar canciones
        this.deleteRows = null;
        this.deleteChecked = new Set();
        this.confirmDelete = false;

        // Buscador
        this.searchQuery = '';

        // Vídeo de YouTube de la canción que suena, manejado con los controles de Launcherito: a la
        // vista en la vista original y, para las de Spotify, oculto en las demás. Mientras
        // audioFromVideo es true el sonido sale del vídeo y el .mp3 está cerrado.
        this.songVideo = null;
        this.videoHost = null;
        this.videoShown = false;      // en la vista original se ve el vídeo (no la carátula)
        this.videoStatus = null;
        this.videoFailedFor = null;   // .mp3 sin vídeo: suena el archivo y no se reintenta
        this.skippedInARow = 0;       // canciones de Spotify sin vídeo saltadas seguidas
        this.songVideoPlaying = false;
        this.songVideoStarted = false;
        this.songVideoVersion = 0;    // invalida búsquedas de vídeo de una canción que ya no suena
        this.audioFromVideo = false;
        this.videoTime = 0;
        this.videoDuration = 0;

        this.metadataVersion = 0;     // invalida lecturas de etiquetas de una canción que ya no suena

        this.audio.addEventListener('loadedmetadata', () => {
            if (this.audioFromVideo) return;
            this.skippedInARow = 0;
            this.duration = Math.max(1, this.audio.duration || 0);
            this.changed();
        });
        this.audio.addEventListener('timeupdate', () => {
            if (this.isPlaying && !this.audioFromVideo && !this.isSeeking) {
                this.position = this.audio.currentTime;
                this.changed();
            }
        });
        this.audio.addEventListener('ended', () => {
            if (!this.audioFromVideo) this.songEnded();
        });
        this.audio.addEventListener('error', () => {
            if (!this.audioFromVideo && this.audio.getAttribute('src')) this.mediaFailed(this.audio.error?.message ?? '');
        });
        // Las teclas multimedia, los auriculares o el sistema también pausan y reanudan.
        this.audio.addEventListener('pause', () => this.syncPlaying(false));
        this.audio.addEventListener('play', () => this.syncPlaying(true));

        this.setupMediaSession();
        this.updateLibraryButtons();
    }

    changed() {
        this.onChange?.();
    }

    chooseLargeText(large) {
        this.largeText = large;
        Settings.write('letraGrande', large);
        this.changed();
    }

    syncPlaying(playing) {
        // Los avisos llegan un instante después: si el reproductor ya no está así (otra canción), se ignoran.
        if (this.audioFromVideo || !this.audio.getAttribute('src') || this.audio.ended || this.audio.paused === playing) return;
        if (this.isPlaying !== playing) {
            this.isPlaying = playing;
            this.changed();
        }
    }

    // ─────────────────────────────── Cargar canciones ───────────────────────────────

    /** Añade los .mp3 elegidos en el selector o soltados sobre la página. */
    async addFiles(files) {
        const valid = [];
        let rejected = 0;
        for (const file of files) {
            if (file.type !== 'audio/mpeg' && file.type !== 'audio/mp3' && !/\.mp3$/i.test(file.name)) {
                rejected++;
                continue;
            }
            valid.push(file);
        }
        if (rejected > 0) this.showMessage(`Solo se admiten archivos .mp3. Se han ignorado ${rejected} archivo(s).`);
        if (valid.length === 0) return;

        const keys = valid.map(file => SongTags.keyOf(file));
        valid.forEach((file, i) => FileStore.session.set(keys[i], file));

        // Se guardan en «Tus canciones»; el título y el artista se apuntan al leer sus etiquetas.
        this.library.add(keys.map(key => new SavedSong(key, SongTags.fallbackTitle(key))));
        this.addToPlaylist(keys);

        // Copia en el navegador, para volver a tenerlas al abrir otra vez la página.
        let failed = 0;
        for (let i = 0; i < valid.length; i++) {
            if (!(await FileStore.put(keys[i], valid[i]))) failed++;
        }
        if (failed > 0) {
            this.showMessage(`${failed === 1 ? '1 canción no se ha podido' : `${failed} canciones no se han podido`} guardar ` +
                'en el navegador (¿sin espacio o en una ventana privada?). Suenan ahora, pero no estarán al volver a abrir la página.');
        }
        this.updateLibraryButtons();
        this.changed();
    }

    /** Añade canciones (.mp3 o de Spotify) a la lista y al mosaico. Devuelve cuántas son nuevas. */
    addToPlaylist(keys) {
        const before = this.playlist.count;
        const wasEmpty = before === 0;
        const added = this.playlist.add(keys);
        this.syncMosaic();
        this.classify(keys);

        // Al pasar de una canción a varias se cambia sola a la vista mosaico.
        if (before <= 1 && this.playlist.count > 1 && !this.mosaicView) this.showMosaic(true);

        // Si ya estaba sonando algo, las nuevas canciones se añaden a la lista sin interrumpirla.
        const first = this.playlist.current;
        if (wasEmpty && first != null) this.playSong(first);
        else this.updatePosition();
        this.showPlayer();   // también si se han añadido desde el menú principal
        this.changed();
        return added;
    }

    // ─────────────────────────────── Listas de Spotify ───────────────────────────────

    /** Abre el cuadro del enlace. Si ya hay un enlace de Spotify copiado, aparece pegado. */
    async openSpotifyDialog() {
        this.spotifyDialogStatus = '';
        this.spotifyDialogOpen = true;
        this.changed();
        if (!this.spotifyLink && navigator.clipboard?.readText) {
            try {
                const text = await navigator.clipboard.readText();
                if (SpotifyLibrary.parseLink(text) && !this.spotifyLink) {
                    this.spotifyLink = text.trim();
                    this.changed();
                }
            } catch {
                // sin permiso para leer el portapapeles: se pega a mano
            }
        }
    }

    closeSpotifyDialog() {
        if (!this.spotifyBusy) this.spotifyDialogOpen = false;   // mientras se lee la lista no se cierra
        this.changed();
    }

    /**
     * Lee la lista de Spotify y añade sus canciones al mosaico. Las que ya están en un .mp3 cargado no
     * se repiten (suena el .mp3 entero); las demás suenan con su vídeo de YouTube.
     */
    async addSpotifyList() {
        if (this.spotifyBusy) return;
        const link = SpotifyLibrary.parseLink(this.spotifyLink);
        if (!link) {
            this.spotifyDialogStatus = 'Ese enlace no es de una lista ni de un álbum de Spotify.';
            this.changed();
            return;
        }
        this.spotifyBusy = true;
        this.spotifyDialogStatus = 'Leyendo la lista en Spotify…';
        this.changed();
        let list;
        try {
            list = await SpotifyLibrary.load(link.type, link.id);
        } catch (e) {
            this.spotifyDialogStatus = e.message;
            return;
        } finally {
            this.spotifyBusy = false;
            this.changed();
        }

        const keys = [];
        const saved = [];
        let owned = 0;
        for (const track of list.tracks) {
            if (this.localSongs.has(SpotifyLibrary.matchKey(track.artists, track.title))) owned++;
            else {
                keys.push(track.key);
                saved.push(SavedSong.fromTrack(track));
            }
        }
        this.library.add(saved);
        const added = this.addToPlaylist(keys);

        const summary = [`«${list.name}»: ${songs(added)} añadida${added === 1 ? '' : 's'}`];
        if (owned > 0) summary.push(`${owned} ya la${owned === 1 ? '' : 's'} tenías en .mp3`);
        if (keys.length > added) summary.push(`${keys.length - added} ya estaba${keys.length - added === 1 ? '' : 'n'}`);
        const text = list.tracks.length === 0 ? `«${list.name}» no tiene canciones.` : summary.join('  ·  ');

        // Si no se ha añadido nada el cuadro sigue abierto con la explicación; si no, se cierra.
        if (added === 0) {
            this.spotifyDialogStatus = text;
        } else {
            this.spotifyDialogOpen = false;
            this.spotifyLink = '';
            this.showStatus(text);
        }
        this.changed();
    }

    /**
     * Quita las canciones de Spotify que ya están en un .mp3 cargado: suena el archivo, que está
     * entero. La que está sonando se deja hasta que se cambie de canción.
     */
    removeSpotifyDuplicates() {
        const current = this.playlist.current;
        const duplicates = this.playlist.songList.filter(key => {
            const track = SpotifyLibrary.tryGet(key);
            return key !== current && track && this.localSongs.has(SpotifyLibrary.matchKey(track.artists, track.title));
        });
        if (this.playlist.remove(duplicates) === 0) return;
        duplicates.forEach(key => this.meta.delete(key));
        this.metaVersion++;
        this.library.remove(duplicates);   // tampoco hace falta guardarla: ya está guardado el .mp3
        this.syncMosaic();
        this.updatePosition();
    }

    showStatus(text) {
        this.statusText = text;
        clearTimeout(this.statusClear);
        this.statusClear = setTimeout(() => {
            this.statusText = '';
            this.changed();
        }, 12000);
        this.changed();
    }

    showMessage(text, title = 'Launcherito') {
        this.messageTitle = title;
        this.message = text;
        this.changed();
    }

    // ─────────────────────── Tus canciones y Borrar canciones ───────────────────────

    updateLibraryButtons() {
        this.libraryCount = this.library.count;
        this.unloadedCount = this.unloadedSongs().length;
    }

    /** Se puede poner: es de Spotify, ya está cargada o el navegador guarda su copia. */
    isAvailable(key) {
        return SpotifyLibrary.isSpotify(key) || this.tileByKey.has(key) || FileStore.has(key);
    }

    /** Guardadas que no están en el mosaico y se pueden cargar. */
    unloadedSongs() {
        return this.library.all.filter(song => !this.tileByKey.has(song.key) && this.isAvailable(song.key));
    }

    /** Carga todas las canciones guardadas que aún no están en el mosaico. */
    loadLibrary() {
        const keys = this.unloadedSongs().map(song => {
            if (song.isSpotify) SpotifyLibrary.register(song.toTrack());
            return song.key;
        });
        if (keys.length) this.addToPlaylist(keys);
        this.showPlayer();   // aunque ya estuvieran todas cargadas

        const missing = this.library.all.filter(song => !this.isAvailable(song.key)).length;
        if (missing > 0) {
            this.showMessage((missing === 1 ? '1 canción guardada ya no se puede abrir' : `${missing} canciones guardadas ya no se pueden abrir`) +
                ' (el navegador no conserva su copia). Vuelve a cargar el .mp3 o quítala con «Borrar canciones».');
        }
        this.updateLibraryButtons();
        this.changed();
    }

    openDeleteDialog() {
        this.deleteChecked.clear();
        this.deleteRows = this.library.all.map(song => ({
            key: song.key, title: song.title, artist: song.artist, isSpotify: song.isSpotify,
            missing: !song.isSpotify && !this.isAvailable(song.key),
        }));
        this.changed();
    }

    closeDeleteDialog() {
        this.deleteRows = null;
        this.deleteChecked.clear();
        this.confirmDelete = false;
        this.changed();
    }

    toggleChecked(key) {
        if (this.deleteChecked.has(key)) this.deleteChecked.delete(key);
        else this.deleteChecked.add(key);
        this.changed();
    }

    toggleSelectAll() {
        const rows = this.deleteRows;
        if (!rows) return;
        const select = rows.some(row => !this.deleteChecked.has(row.key));
        for (const row of rows) {
            if (select) this.deleteChecked.add(row.key);
            else this.deleteChecked.delete(row.key);
        }
        this.changed();
    }

    get markedForDelete() { return [...this.deleteChecked]; }

    get deleteQuestion() {
        const count = this.deleteChecked.size;
        return (count === this.library.count ? '¿Borrar todas tus canciones guardadas?' : `¿Borrar ${songs(count)} de «Tus canciones»?`) +
            '\nLos archivos .mp3 de tu ordenador no se borran; solo la copia que guarda el navegador.';
    }

    deleteMarked() {
        const keys = this.markedForDelete;
        if (keys.length === 0) return;
        this.library.remove(keys);
        FileStore.remove(keys.filter(key => !SpotifyLibrary.isSpotify(key)));
        this.closeDeleteDialog();
        this.removeFromSession(keys);
        this.updateLibraryButtons();
        this.changed();
    }

    /**
     * Quita del mosaico y de la lista las canciones borradas. Si sonaba una de ellas, pasa a la
     * siguiente que quede; si no queda ninguna, se vuelve al menú principal.
     */
    removeFromSession(keys) {
        const gone = new Set(keys);
        const currentGone = gone.has(this.playlist.current);
        const wasPlaying = this.isPlaying;
        if (currentGone) this.stop();

        gone.forEach(key => this.meta.delete(key));
        this.metaVersion++;
        for (const [match, key] of this.localSongs) if (gone.has(key)) this.localSongs.delete(match);
        this.playlist.remove(gone);                       // todas menos la que suena…
        if (currentGone) this.playlist.removeCurrent();   // …que se quita aparte
        this.syncMosaic();
        this.updatePosition();

        if (this.playlist.count === 0) {
            this.disposeSongVideo();
            this.setSongTexts('', '');
            this.currentKey = null;
            this.playerVisible = false;
        } else if (currentGone) {
            const next = this.playlist.current;
            if (next) {
                this.playSong(next);
                if (!wasPlaying) this.pause();
            }
        }
    }

    // ─────────────────────────────── Reproducción ───────────────────────────────

    /** Pulsar una canción: suena (las de Spotify, con su vídeo de YouTube). */
    selectSong(key) {
        const song = this.playlist.jumpTo(key);
        if (song) this.playSong(song);
    }

    playSong(key) {
        this.stop();
        this.currentKey = key;
        this.showMetadata(key);
        this.updatePosition();
        this.position = 0;
        this.duration = 0;
        this.playerVisible = true;

        // Suena el vídeo de YouTube en la vista original y siempre en las de Spotify; si no, el .mp3.
        this.videoFailedFor = null;
        const host = this.wantedVideoHost(key);
        if (host) this.startSongVideo(key, 0, host);
        else this.openMp3(key, 0);
        this.play();
        this.changed();
    }

    /** Pone el .mp3 en el reproductor, empezando en el segundo start. */
    async openMp3(key, start) {
        const version = ++this.audioVersion;
        const blob = await FileStore.get(key);
        if (version !== this.audioVersion) return;   // mientras se abría se ha cambiado de canción
        if (!blob) {
            this.mediaFailed('El navegador ya no tiene una copia del archivo.');
            return;
        }
        this.releaseAudio();
        this.audioUrl = URL.createObjectURL(blob);
        this.audio.src = this.audioUrl;
        if (start > 0) {
            this.audio.addEventListener('loadedmetadata', () => { this.audio.currentTime = start; }, { once: true });
        }
        if (this.isPlaying) this.startAudio();
        this.updateMediaSession();
    }

    /** El navegador puede negarse a sonar sin un clic antes (al abrir la página, p. ej.). */
    startAudio() {
        this.audio.play().catch(e => {
            if (e.name === 'NotAllowedError' && !this.audioFromVideo) {
                this.isPlaying = false;
                this.changed();
            }
        });
    }

    releaseAudio() {
        this.audio.pause();
        this.audio.removeAttribute('src');
        this.audio.load();
        if (this.audioUrl) URL.revokeObjectURL(this.audioUrl);
        this.audioUrl = null;
    }

    previous() {
        const song = this.playlist.previous();
        if (song) this.playSong(song);
    }

    next() {
        const song = this.playlist.next();
        if (song) this.playSong(song);
    }

    toggleShuffle() {
        this.playlist.shuffle = !this.playlist.shuffle;
        this.shuffle = this.playlist.shuffle;
        this.updatePosition();
        this.changed();
    }

    updatePosition() {
        this.positionText = this.playlist.count === 0 ? '' : `${this.playlist.position + 1} / ${this.playlist.count}`;
    }

    togglePlay() {
        if (this.isPlaying) this.pause();
        else this.play();
    }

    play() {
        this.isPlaying = true;
        if (this.audioFromVideo) this.songVideo?.play();
        else if (this.audio.getAttribute('src')) this.startAudio();
        this.updateMediaSession();
        this.changed();
    }

    pause() {
        this.isPlaying = false;
        if (this.audioFromVideo) this.songVideo?.pause();
        else this.audio.pause();
        this.updateMediaSession();
        this.changed();
    }

    stop() {
        this.audioVersion++;
        this.releaseAudio();
        // El vídeo se queda (se reutiliza para la siguiente canción), pero callado.
        this.songVideoVersion++;
        this.audioFromVideo = false;
        this.songVideo?.pause();
        this.isPlaying = false;
    }

    mediaFailed(detail) {
        // Libera el archivo; si no, el reproductor se quedaría con el error puesto.
        const title = this.title;
        this.stop();
        this.showMessage(`No se pudo reproducir «${title}» y se ha quitado de la lista.${detail ? '\n' + detail : ''}`);

        const failed = this.playlist.current;
        if (failed) {
            this.meta.delete(failed);
            this.metaVersion++;
            for (const [match, key] of this.localSongs) if (key === failed) this.localSongs.delete(match);
        }
        const next = this.playlist.removeCurrent();
        this.syncMosaic();
        if (next) this.playSong(next);
        else {
            this.currentKey = null;
            this.playerVisible = false;
        }
        this.changed();
    }

    /** Ha terminado la canción (el .mp3 o su vídeo). */
    songEnded() {
        // Con varias canciones pasa a la siguiente (al acabar la lista vuelve a empezar).
        if (this.playlist.count > 1) {
            const next = this.playlist.next();
            if (next) {
                this.playSong(next);
                return;
            }
        }
        this.pause();
        if (this.audioFromVideo) {
            this.songVideo?.seek(0);
            this.videoTime = 0;
        } else {
            this.audio.currentTime = 0;
        }
        this.position = 0;
        this.changed();
    }

    /** Se mueve la barra: el reproductor solo salta al soltarla. */
    seekDrag(seconds) {
        this.isSeeking = true;
        this.seekValue = seconds;
        this.changed();
    }

    seekEnd() {
        if (!this.isSeeking) return;
        this.isSeeking = false;
        if (this.audioFromVideo) {
            this.songVideo?.seek(this.seekValue);
            this.videoTime = this.seekValue;
        } else if (this.audio.getAttribute('src')) {
            this.audio.currentTime = this.seekValue;
        }
        this.position = this.seekValue;
        this.changed();
    }

    /**
     * Pone al momento el título y el artista que ya se conocen y luego lee las etiquetas, para que la
     * página no espere al cambiar de canción.
     */
    async showMetadata(key) {
        const version = ++this.metadataVersion;
        const known = this.meta.get(key);
        if (known) this.setSongTexts(known.title, known.artist);
        else this.setSongTexts(SongTags.fallbackTitle(key), '');
        const info = await SongTags.read(key);
        if (version !== this.metadataVersion) return;   // mientras se leía ya se ha pasado a otra canción
        this.setSongTexts(info.title, info.artist);
        this.changed();
    }

    setSongTexts(title, artist) {
        this.title = title;
        this.artist = artist;
        this.updateMediaSession();
    }

    // ───────────── Controles del sistema (teclas multimedia, auriculares, pantalla de bloqueo) ─────────────

    setupMediaSession() {
        const session = navigator.mediaSession;
        if (!session) return;
        const handlers = {
            play: () => this.play(),
            pause: () => this.pause(),
            previoustrack: () => this.previous(),
            nexttrack: () => this.next(),
            seekto: details => {
                this.seekDrag(details.seekTime);
                this.seekEnd();
            },
        };
        for (const [action, handler] of Object.entries(handlers)) {
            try {
                session.setActionHandler(action, handler);
            } catch {
                // acción que este navegador no admite
            }
        }
    }

    updateMediaSession() {
        const session = navigator.mediaSession;
        if (!session || !this.currentKey) return;
        const key = this.currentKey;
        const cover = Covers.peek(key);
        session.metadata = new MediaMetadata({
            title: this.title,
            artist: this.artist,
            album: 'Launcherito',
            artwork: cover ? [{ src: cover, sizes: '512x512' }] : [],
        });
        session.playbackState = this.isPlaying ? 'playing' : 'paused';
        if (cover === undefined) {
            Covers.get(key).then(url => { if (url && this.currentKey === key) this.updateMediaSession(); });
        }
    }

    // ─────────────────────── Vídeo de YouTube de la canción que suena ───────────────────────

    /**
     * Dónde va el vídeo de la canción: a la vista en la vista original; oculto si es de Spotify; en
     * ningún sitio si es un .mp3 fuera de la vista original (suena el archivo). Sin el servidor de
     * Launcherito no se puede buscar en YouTube: los .mp3 suenan siempre con el archivo.
     */
    wantedVideoHost(key) {
        if (SpotifyLibrary.isSpotify(key)) return this.singleViewVisible ? VideoHost.Single : VideoHost.Hidden;
        if (this.singleViewVisible && Http.server) return VideoHost.Single;
        return null;
    }

    /**
     * Pone el vídeo de YouTube de la canción en host, empezando en el segundo start. El sonido sale
     * del vídeo y lo manejan los controles de Launcherito. Mientras se busca se ve la carátula.
     */
    startSongVideo(key, start, host) {
        // El reproductor se reutiliza de una canción a otra (y se cambia de sitio si hace falta).
        this.moveSongVideo(host);
        const version = ++this.songVideoVersion;
        this.audioFromVideo = true;
        this.songVideoPlaying = false;
        this.videoTime = start;
        this.videoDuration = 0;
        this.showSongVideo(false);
        this.videoStatus = host === VideoHost.Single ? 'Buscando el vídeo en YouTube…' : null;

        let video = this.songVideo;
        if (!video) {
            video = this.createSongVideo();
            this.songVideo = video;
            this.videoHost = host;
            this.songVideoStarted = false;
        }
        if (this.songVideoStarted) video.hold();   // el vídeo anterior no vuelve a sonar mientras se busca el nuevo
        this.loadSongVideo(key, start, version);
    }

    createSongVideo() {
        const video = new YouTubeVideo();
        video.onPlaying = () => {
            if (this.songVideo === video && this.audioFromVideo) {
                this.skippedInARow = 0;
                this.songVideoPlaying = true;
                if (this.videoHost === VideoHost.Single) this.showSongVideo(true);
                this.changed();
            }
        };
        video.onEnded = () => {
            if (this.songVideo === video && this.audioFromVideo) this.songEnded();
        };
        video.onProgress = (time, total) => {
            if (this.songVideo === video && this.audioFromVideo) {
                this.videoTime = time;
                if (!this.isSeeking) this.position = time;
                if (total > 0 && Math.abs(total - this.videoDuration) > 0.5) {
                    this.videoDuration = total;
                    this.duration = Math.max(1, total);
                }
                this.changed();
            }
        };
        video.onUnavailable = () => {
            if (this.songVideo === video && this.audioFromVideo) this.videoFailed('NotEmbeddable');
        };
        return video;
    }

    async loadSongVideo(key, start, version) {
        const track = SpotifyLibrary.tryGet(key);
        const known = this.meta.get(key);
        let title, artist;
        if (track) [title, artist] = [track.title, track.mainArtist];
        else if (known) [title, artist] = [known.title, known.artist];
        else ({ title, artist } = await SongTags.read(key));

        const ids = await YouTubeLinks.findVideos(key, artist, title);
        const video = this.songVideo;
        if (version !== this.songVideoVersion || !video) return;   // mientras se buscaba se ha cambiado de canción o de vista
        if (ids.length === 0) {
            this.videoFailed(Http.server ? 'Offline' : 'NoServer');
            return;
        }
        if (this.songVideoStarted) video.load(ids, start);
        else {
            this.songVideoStarted = true;
            video.begin(ids, start);
        }
        if (this.isPlaying) video.play();
        else video.pause();
    }

    /**
     * La canción no tiene vídeo que se pueda ver. Si es un .mp3, suena el archivo. Si es de Spotify
     * no hay nada más que poner: sin conexión se para; si es solo esa, se pasa a la siguiente.
     */
    videoFailed(problem) {
        const key = this.playlist.current;
        if (!key) return;
        if (!SpotifyLibrary.isSpotify(key)) {
            this.videoFailedFor = key;   // no se vuelve a intentar al cambiar de vista
            this.switchToMp3(problem === 'NotEmbeddable'
                ? 'Esta canción no tiene vídeo que se pueda ver aquí: suena el .mp3'
                : 'Sin conexión con YouTube: suena el .mp3');
            return;
        }

        this.videoStatus = null;
        const title = SongTags.fallbackTitle(key);
        if (problem !== 'NotEmbeddable') {
            this.showStatus(problem === 'NoServer'
                ? 'Las canciones de Spotify necesitan abrir Launcherito con su servidor (node servidor.mjs) para sonar'
                : 'Sin conexión con YouTube: las canciones de Spotify no pueden sonar');
            this.pause();
            return;
        }
        // Se salta, pero sin dar vueltas a la lista si ninguna tiene vídeo.
        const next = ++this.skippedInARow < this.playlist.count ? this.playlist.next() : null;
        if (next) {
            this.showStatus(`«${title}» no tiene vídeo que se pueda ver aquí: pasa a la siguiente`);
            this.playSong(next);
        } else {
            this.skippedInARow = 0;
            this.showStatus(`«${title}» no tiene vídeo que se pueda ver aquí`);
            this.pause();
        }
    }

    /**
     * Al cambiar de vista el sonido pasa a donde toca (el vídeo a la vista, el vídeo oculto o el .mp3)
     * y sigue por el mismo segundo.
     */
    updateSongSource() {
        const key = this.playlist.current;
        if (!key) return;
        const wanted = this.wantedVideoHost(key);
        if (this.audioFromVideo ? this.videoHost === wanted : wanted == null || this.videoFailedFor === key) {
            if (wanted == null && !this.audioFromVideo) this.disposeSongVideo();   // suena el .mp3: el reproductor no hace falta
            return;
        }

        // De la vista original al hueco oculto o al revés: el mismo vídeo cambia de sitio sin cortarse.
        if (this.audioFromVideo && wanted) {
            this.moveSongVideo(wanted);
            this.showSongVideo(wanted === VideoHost.Single && this.songVideoPlaying);
            if (wanted === VideoHost.Single && !this.songVideoPlaying) this.videoStatus = 'Buscando el vídeo en YouTube…';
            return;
        }

        const at = this.audioFromVideo ? this.videoTime : this.audio.currentTime;
        const playing = this.isPlaying;
        if (this.audioFromVideo) {
            this.audioFromVideo = false;
            this.disposeSongVideo();
        } else {
            this.audioVersion++;
            this.releaseAudio();
        }

        if (wanted == null) {
            this.openMp3(key, at);   // suena solo si isPlaying
        } else {
            this.startSongVideo(key, at, wanted);
            if (playing) this.songVideo?.play();
            else this.songVideo?.pause();
        }
    }

    /** El sonido de un .mp3 vuelve al archivo, por donde iba el vídeo (el vídeo se queda parado). */
    switchToMp3(reason) {
        const key = this.playlist.current;
        if (!this.audioFromVideo || !key) return;
        if (reason) this.showStatus(reason);
        this.songVideoVersion++;
        this.audioFromVideo = false;
        this.songVideo?.pause();
        this.showSongVideo(false);
        this.videoStatus = null;
        this.openMp3(key, this.videoTime);
        this.changed();
    }

    /** Muestra el vídeo (marco 16:9) o la carátula (marco cuadrado) en la vista original. */
    showSongVideo(visible) {
        this.videoShown = visible;
        if (visible) this.videoStatus = null;
    }

    /** Pasa el vídeo (si lo hay) a otro hueco sin cerrarlo. */
    moveSongVideo(host) {
        if (!this.songVideo || this.videoHost === host) return;
        this.videoHost = host;
        if (host !== VideoHost.Single) {
            this.showSongVideo(false);
            this.videoStatus = null;
        }
    }

    /** Libera el reproductor del vídeo. Quien lo llama decide antes de dónde sale el sonido. */
    disposeSongVideo() {
        const video = this.songVideo;
        if (!video) return;
        this.songVideoVersion++;
        this.songVideo = null;
        this.songVideoStarted = false;
        this.videoHost = null;
        video.dispose();
        this.showSongVideo(false);
        this.videoStatus = null;
    }

    // ─────────────────────────── Menú principal y buscador ───────────────────────────

    /** Vuelve al menú principal sin parar la música; desde allí se vuelve con «Volver al reproductor». */
    goHome() {
        this.editMosaic(false);
        this.searchQuery = '';
        this.playerVisible = false;
        this.updateLibraryButtons();
        // La vista original deja de verse: el vídeo pasa al hueco oculto o suena el .mp3.
        this.updateSongSource();
        this.changed();
    }

    /** Muestra el reproductor si hay algo cargado. */
    showPlayer() {
        if (this.playlist.count === 0 || this.playerVisible) return;
        this.playerVisible = true;
        this.updateSongSource();
        this.changed();
    }

    get hasSongs() { return this.tiles.length > 0; }

    setSearch(text) {
        this.searchQuery = text;
        this.changed();
    }

    /**
     * Busca por el nombre de la canción (y por el artista) sin distinguir mayúsculas ni tildes, entre
     * las cargadas y las guardadas en «Tus canciones». Primero las que empiezan por lo escrito.
     */
    searchResults() {
        const query = fold(this.searchQuery.trim());
        if (!query) return [];
        const found = [];
        for (const [key, title, artist] of this.searchCandidates()) {
            const at = fold(title).indexOf(query);
            const inArtist = fold(artist).includes(query);
            if (at < 0 && !inArtist) continue;
            if (!this.isAvailable(key)) continue;   // un .mp3 guardado que ya no se puede abrir no se puede poner
            found.push({ key, title, artist, highlight: at >= 0 ? [at, at + query.length] : null, rank: at === 0 ? 0 : at > 0 ? 1 : 2 });
        }
        found.sort((a, b) => a.rank - b.rank || collator.compare(a.title, b.title));
        return found.slice(0, MAX_SEARCH_RESULTS);
    }

    /** Las canciones del mosaico y, además, las guardadas que aún no están cargadas. */
    searchCandidates() {
        const seen = new Set();
        const result = [];
        for (const tile of this.tiles) {
            seen.add(tile.key);
            const known = this.meta.get(tile.key);
            const track = SpotifyLibrary.tryGet(tile.key);
            if (known) result.push([tile.key, known.title, known.artist]);
            else if (track) result.push([tile.key, track.title, track.artists]);
            else result.push([tile.key, SongTags.fallbackTitle(tile.key), '']);
        }
        for (const song of this.library.all) {
            if (!seen.has(song.key)) {
                seen.add(song.key);
                result.push([song.key, song.title, song.artist]);
            }
        }
        return result;
    }

    /** Pone la canción elegida en el buscador; si era una guardada sin cargar, la carga antes. */
    pickSearchResult(key) {
        this.searchQuery = '';
        if (!this.tileByKey.has(key)) {
            const saved = this.library.get(key);
            if (saved?.isSpotify) SpotifyLibrary.register(saved.toTrack());
            this.addToPlaylist([key]);
            if (this.playlist.current === key && this.isPlaying) return;   // era la primera y ya suena
        }
        this.selectSong(key);
    }

    // ─────────────────────── Pestañas: artistas y géneros ───────────────────────

    showSection(section) {
        this.section = section;
        this.openCategoryName = null;
        this.searchQuery = '';
        this.applyView();
        this.changed();
    }

    openCategory(name) {
        this.openCategoryName = name;
        this.changed();
    }

    /** Lee título y artista de las canciones nuevas y las clasifica por género con Deezer. */
    async classify(keys) {
        const fresh = [...new Set(keys)].filter(key => !this.meta.has(key));
        if (fresh.length === 0) return;
        const tags = await Promise.all(fresh.map(async key => [key, await SongTags.read(key)]));
        let newLocal = false;
        for (const [key, info] of tags) {
            if (!this.playlist.known.has(key)) continue;   // borrada mientras se leía
            const track = SpotifyLibrary.tryGet(key);
            if (track) {
                // Sin la marca de YouTube y solo el primer artista, para agrupar y buscar en Deezer.
                this.meta.set(key, { title: track.title, artist: track.mainArtist, genre: null, classified: false });
            } else {
                this.meta.set(key, { title: info.title, artist: info.artist, genre: null, classified: false });
                this.localSongs.set(SpotifyLibrary.matchKey(info.artist, info.title), key);
                this.library.describe(key, info.title, info.artist);
                newLocal = true;
            }
            this.pending.push(key);
        }
        this.metaVersion++;
        this.library.save();
        if (newLocal) this.removeSpotifyDuplicates();
        this.changed();
        this.runClassification();
    }

    async runClassification() {
        if (this.classifying) return;   // el bucle que ya está en marcha se encargará también de estas
        this.classifying = true;
        let done = 0;
        try {
            while (this.pending.length) {
                this.catalogStatus = `Clasificando con Deezer… ${done}/${done + this.pending.length}`;
                this.changed();
                const key = this.pending[0];
                const info = this.meta.get(key);
                if (info) {
                    const genre = await this.catalog.getGenre(info.artist, info.title);
                    // Puede haberse borrado mientras se consultaba.
                    const now = this.meta.get(key);
                    if (now) {
                        this.meta.set(key, { ...now, genre, classified: true });
                        this.metaVersion++;
                    }
                }
                this.pending.shift();
                if (++done % 25 === 0) this.catalog.save();
            }
            this.catalogStatus = '';
        } catch (e) {
            if (!(e instanceof CatalogUnavailableError)) throw e;
            // Las que faltan se quedan en la cola y se reintentan al añadir más canciones.
            this.catalogStatus = `Sin conexión con Deezer: faltan ${this.pending.length} por clasificar`;
        } finally {
            this.classifying = false;
            this.catalog.save();
            this.changed();
        }
    }

    /** Las tarjetas de la pestaña abierta: artistas por orden alfabético, géneros de más a menos canciones. */
    categories() {
        const groups = new Map();
        const artists = this.section === Section.Artists;
        for (const meta of this.meta.values()) {
            if (!artists && !meta.classified) continue;
            const name = (artists ? meta.artist : genreName(meta)).toLowerCase();
            if (!groups.has(name)) groups.set(name, []);
            groups.get(name).push(meta);
        }
        const list = [...groups.values()];
        if (artists) {
            return list.map(group => ({ name: group[0].artist, count: group.length, picture: this.artistPictures.get(group[0].artist) ?? null, pictureArtist: group[0].artist }))
                .sort((a, b) => collator.compare(a.name, b.name));
        }
        return list.map(group => {
            // Deezer no tiene foto para los subgéneros ("Pop latino", "Flamenco"): se usa la del artista
            // con más canciones de ese género.
            const picture = group[0].genre?.picture ?? null;
            let topArtist = null;
            if (!picture && genreName(group[0]) !== UNCLASSIFIED) {
                const counts = new Map();
                for (const m of group) counts.set(m.artist, (counts.get(m.artist) ?? 0) + 1);
                topArtist = [...counts].reduce((best, entry) => entry[1] > best[1] ? entry : best)[0];
            }
            return { name: genreName(group[0]), count: group.length, picture: picture ?? (topArtist ? this.artistPictures.get(topArtist) ?? null : null), pictureArtist: topArtist };
        }).sort((a, b) => (a.name === UNCLASSIFIED) - (b.name === UNCLASSIFIED) || b.count - a.count || collator.compare(a.name, b.name));
    }

    /** Pide a Deezer la foto de un artista la primera vez que se va a ver su tarjeta. */
    async requestArtistPicture(artist) {
        if (!artist || artist === SongTags.UNKNOWN_ARTIST || this.picturesRequested.has(artist)) return;
        this.picturesRequested.add(artist);
        const picture = await this.catalog.getArtistPicture(artist);
        this.artistPictures.set(artist, picture);
        this.catalog.save();
        this.metaVersion++;
        this.changed();
    }

    /** Canciones de un artista o de un género, por título. */
    categorySongs(name) {
        const artists = this.section === Section.Artists;
        const wanted = name.toLowerCase();
        return [...this.meta].filter(([, m]) => (artists ? m.artist : genreName(m)).toLowerCase() === wanted && (artists || m.classified))
            .sort((a, b) => collator.compare(a[1].title, b[1].title));
    }

    // ───────────────────────────── Vista mosaico ─────────────────────────────

    toggleMosaicView() {
        this.showMosaic(!this.mosaicView);
        this.changed();
    }

    showMosaic(mosaic) {
        this.mosaicView = mosaic;
        this.applyView();
    }

    /**
     * Entra o sale del modo edición: con él activo las portadas se mueven (arrastrándolas) y cambian de
     * tamaño (con el tirador de su esquina).
     */
    editMosaic(editing) {
        this.isEditing = editing;
        this.changed();
    }

    /** El mosaico solo se ve en la pestaña Canciones con la vista mosaico elegida. */
    get mosaicVisible() { return this.section === Section.Songs && this.mosaicView; }

    /** La vista original (carátula grande) solo se ve en Canciones sin la vista mosaico. */
    get singleViewVisible() { return this.playerVisible && this.section === Section.Songs && !this.mosaicView; }

    applyView() {
        // Solo se edita el mosaico: al salir de él se deja la edición.
        if (!this.mosaicVisible && this.isEditing) this.editMosaic(false);
        // En la vista original suena el vídeo de la canción; fuera de ella, el .mp3 por donde iba (las
        // de Spotify, su vídeo oculto).
        this.updateSongSource();
    }

    /**
     * Crea o quita portadas para que el mosaico coincida con la lista de reproducción: en orden
     * alfabético o en el que haya elegido el usuario arrastrándolas.
     */
    syncMosaic() {
        const keys = this.arrangement.arrange([...this.playlist.songList]);
        const present = new Set(keys);
        for (const key of [...this.tileByKey.keys()]) if (!present.has(key)) this.tileByKey.delete(key);
        this.tiles = keys.map((key, i) => {
            let tile = this.tileByKey.get(key);
            if (!tile) this.tileByKey.set(key, tile = new Tile(key));
            // Sin tamaño elegido, una de cada seis es grande.
            tile.baseSpan = this.arrangement.spanOf(key) ?? (i % 6 === 0 ? 2 : 1);
            return tile;
        });
        this.updateLibraryButtons();
    }

    /** Se ha movido o cambiado de tamaño una portada: se guarda cómo ha quedado el mosaico. */
    rememberArrangement() {
        this.arrangement.remember(this.tiles);
    }

    /** Pone la portada tile en el sitio de target; las de en medio corren un puesto. */
    moveTileBefore(tile, target) {
        const to = this.tiles.indexOf(target);
        const from = this.tiles.indexOf(tile);
        if (to < 0 || from < 0) return;
        this.tiles.splice(from, 1);
        this.tiles.splice(to, 0, tile);
        this.changed();
    }

    /** Atrás (Esc) va deshaciendo: el buscador, la edición, el artista abierto, la pestaña y, por último, al menú. */
    back() {
        if (this.searchQuery) this.setSearch('');
        else if (this.isEditing) this.editMosaic(false);
        else if (this.openCategoryName != null) this.openCategory(null);
        else if (this.section !== Section.Songs) this.showSection(Section.Songs);
        else if (this.playerVisible) this.goHome();
    }

    /** Guarda lo pendiente al cerrar la página. */
    saveAll() {
        this.catalog.save();
        this.library.save();
    }
}
