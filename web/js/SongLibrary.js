'use strict';

/**
 * Canción guardada. key es la clave del .mp3 ("mp3:…") o la de Spotify ("spotify:track:…"). De las
 * de Spotify se guardan todos sus datos, porque sin ellos no se pueden volver a mostrar.
 */
class SavedSong {
    constructor(key, title = '', artist = '', durationMs = 0) {
        this.key = key;
        this.title = title;
        this.artist = artist;
        this.durationMs = durationMs;
    }

    get isSpotify() { return SpotifyLibrary.isSpotify(this.key); }

    toTrack() {
        return new SpotifyTrack(this.key.slice(SpotifyLibrary.KEY_PREFIX.length), this.title, this.artist, this.durationMs);
    }

    static fromTrack(track) {
        return new SavedSong(track.key, track.title, track.artists, track.durationMs);
    }
}

/**
 * Las canciones que se han cargado alguna vez («Tus canciones»), guardadas en el navegador
 * (canciones.json en Windows) para que sigan ahí al volver a abrir la página. Borrar una solo la quita
 * de aquí y de la copia del navegador: el archivo .mp3 del ordenador no se toca.
 */
class SongLibrary {
    constructor() {
        this.songs = new Map();
        this.dirty = false;
        for (const item of Settings.read('canciones', [])) {
            if (item && typeof item.Key === 'string' && !this.songs.has(item.Key)) {
                this.songs.set(item.Key, new SavedSong(item.Key, item.Title ?? '', item.Artist ?? '', item.DurationMs ?? 0));
            }
        }
    }

    get count() { return this.songs.size; }

    /** Todas, por orden alfabético de título. */
    get all() { return [...this.songs.values()].sort((a, b) => collator.compare(a.title, b.title)); }

    has(key) { return this.songs.has(key); }

    get(key) { return this.songs.get(key); }

    /** Guarda las canciones nuevas (las que ya estaban se dejan como están). */
    add(newSongs) {
        for (const song of newSongs) {
            if (!this.songs.has(song.key)) {
                this.songs.set(song.key, song);
                this.dirty = true;
            }
        }
        this.save();
    }

    /** Apunta el título y el artista de un .mp3 en cuanto se leen sus etiquetas. Se escribe con save. */
    describe(key, title, artist) {
        const song = this.songs.get(key);
        if (song && (song.title !== title || song.artist !== artist)) {
            song.title = title;
            song.artist = artist;
            this.dirty = true;
        }
    }

    remove(keys) {
        for (const key of keys) {
            if (this.songs.delete(key)) this.dirty = true;
        }
        this.save();
    }

    /** Escribe si algo ha cambiado. */
    save() {
        if (!this.dirty) return;
        const json = [...this.songs.values()].map(s => ({ Key: s.key, Title: s.title, Artist: s.artist, DurationMs: s.durationMs }));
        if (Settings.write('canciones', json)) this.dirty = false;
    }
}

/**
 * Orden y tamaño de las portadas que el usuario ha elegido en el mosaico (mosaico.json en Windows).
 * Mientras no se toque nada, el mosaico va en orden alfabético con los tamaños automáticos.
 */
class MosaicArrangement {
    constructor() {
        const json = Settings.read('mosaico', {});
        this.order = Array.isArray(json.Order) ? json.Order : [];
        this.spans = new Map(Object.entries(json.Spans ?? {}));
    }

    /**
     * Ordena las canciones, que llegan en orden alfabético: primero las ya colocadas, en el orden
     * elegido; las nuevas, al final y entre ellas por orden alfabético.
     */
    arrange(songs) {
        if (this.order.length === 0) return songs;
        const position = new Map();
        this.order.forEach((key, i) => { if (!position.has(key)) position.set(key, i); });
        const far = Number.MAX_SAFE_INTEGER;
        return [...songs].sort((a, b) => (position.get(a) ?? far) - (position.get(b) ?? far));   // sort es estable
    }

    /** Tamaño elegido para la canción (en celdas de lado), o undefined si no se ha guardado ninguno. */
    spanOf(key) { return this.spans.get(key); }

    /** Guarda el orden y el tamaño de todas las portadas tal como están ahora. */
    remember(tiles) {
        const shown = new Set(tiles.map(t => t.key));
        // Las que ahora no están (una lista de Spotify quitada, p. ej.) conservan su orden detrás.
        this.order = [...tiles.map(t => t.key), ...this.order.filter(key => !shown.has(key))];
        for (const tile of tiles) this.spans.set(tile.key, tile.baseSpan);
        Settings.write('mosaico', { Order: this.order, Spans: Object.fromEntries(this.spans) });
    }
}
