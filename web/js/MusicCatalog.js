'use strict';

/** Deezer no responde (sin conexión, caído o bloqueado). Lo ya consultado no se pierde. */
class CatalogUnavailableError extends Error {
    constructor() { super('No se puede conectar con Deezer.'); }
}

/** Quita añadidos como "- Remastered 2005" o "(feat. X)" que estorban en la búsqueda. */
function cleanTitle(title) {
    let text = title;
    const dash = text.indexOf(' - ');
    if (dash > 0) text = text.slice(0, dash);
    const paren = text.search(/[([]/);
    if (paren > 0) text = text.slice(0, paren);
    return text.trim();
}

/**
 * Clasifica canciones con la API pública de Deezer (no necesita clave): busca la canción, mira el
 * género de su álbum y obtiene fotos de artistas y géneros. Todo se guarda en una caché en el
 * navegador (catalogo.json en Windows), así cada canción solo se consulta una vez.
 */
class MusicCatalog {
    static API = 'https://api.deezer.com';
    // Deezer admite 50 peticiones cada 5 s; con 120 ms entre peticiones se queda por debajo.
    static MIN_INTERVAL_MS = 120;
    static POP_GENRE_ID = 132;

    constructor() {
        this.gate = Promise.resolve();
        this.lastRequest = 0;
        this.dirty = false;
        const json = Settings.read('catalogo', {});
        this.tracks = new Map(Object.entries(json.Tracks ?? {}));                 // "artista|título" → id de álbum
        this.albums = new Map(Object.entries(json.Albums ?? {}));                 // id de álbum → id de género
        this.genres = new Map(Object.entries(json.Genres ?? {}).map(([k, g]) => [k, { name: g.Name ?? 'Desconocido', picture: g.Picture ?? null }]));
        this.artistPictures = new Map(Object.entries(json.ArtistPictures ?? {}));
    }

    /** Busca la canción en Deezer y devuelve el género de su álbum, o null si no se encuentra. */
    async getGenre(artist, title) {
        const key = `${this.normalize(artist)}|${this.normalize(title)}`;
        let albumId = this.tracks.get(key);
        if (albumId === undefined) {
            albumId = await this.searchAlbumId(artist, cleanTitle(title));
            this.tracks.set(key, albumId);
            this.dirty = true;
        }
        if (albumId == null) return null;

        let genreId = this.albums.get(String(albumId));
        if (genreId === undefined) {
            genreId = await this.getAlbumGenreId(albumId);
            this.albums.set(String(albumId), genreId);
            this.dirty = true;
        }
        return genreId == null ? null : this.getGenreInfo(genreId);
    }

    /** URL de la foto de un artista, o null si Deezer no la conoce. */
    async getArtistPicture(artist) {
        const key = this.normalize(artist);
        if (this.artistPictures.has(key)) return this.artistPictures.get(key);
        const json = await this.getJson(`${MusicCatalog.API}/search/artist?limit=1&q=${encodeURIComponent(artist)}`);
        if (!json) return null;
        const picture = this.firstData(json)?.picture_big ?? null;
        this.artistPictures.set(key, picture);
        this.dirty = true;
        return picture;
    }

    /** Guarda la caché si ha cambiado. */
    save() {
        if (!this.dirty) return;
        const json = {
            Tracks: Object.fromEntries(this.tracks),
            Albums: Object.fromEntries(this.albums),
            Genres: Object.fromEntries([...this.genres].map(([k, g]) => [k, { Name: g.name, Picture: g.picture }])),
            ArtistPictures: Object.fromEntries(this.artistPictures),
        };
        if (Settings.write('catalogo', json)) this.dirty = false;
    }

    async searchAlbumId(artist, title) {
        // Primero una búsqueda exacta por artista y título; si no da nada, una búsqueda libre.
        for (const query of [`artist:"${artist}" track:"${title}"`, `${artist} ${title}`]) {
            const json = await this.getJson(`${MusicCatalog.API}/search?limit=1&q=${encodeURIComponent(query)}`);
            if (!json) throw new CatalogUnavailableError();
            const album = this.firstData(json)?.album;
            if (album?.id != null) return album.id;
        }
        return null;
    }

    async getAlbumGenreId(albumId) {
        const json = await this.getJson(`${MusicCatalog.API}/album/${albumId}`);
        if (!json) throw new CatalogUnavailableError();
        // Un álbum puede tener varios géneros ("Pop", "Pop latino", "Latino"); "Pop" es el que Deezer
        // pone a casi todo, así que se prefiere el primero que sea más concreto.
        let chosen = null;
        for (const genre of json.genres?.data ?? []) {
            if (chosen == null) chosen = genre.id;
            if (genre.id !== MusicCatalog.POP_GENRE_ID) {
                chosen = genre.id;
                break;
            }
        }
        if (chosen == null && json.genre_id > 0) chosen = json.genre_id;
        return chosen;
    }

    async getGenreInfo(genreId) {
        const known = this.genres.get(String(genreId));
        if (known) return known;
        const json = await this.getJson(`${MusicCatalog.API}/genre/${genreId}`);
        if (!json) throw new CatalogUnavailableError();
        if (typeof json.name !== 'string') return null;
        const info = { name: json.name, picture: json.picture_big ?? null };
        this.genres.set(String(genreId), info);
        this.dirty = true;
        return info;
    }

    /**
     * Hace una petición respetando el límite de Deezer, de una en una. Con el servidor de Launcherito
     * va a través de él (así los géneros llegan en español); sin él, con JSONP. Devuelve null si falla.
     */
    getJson(url) {
        const run = async () => {
            for (let attempt = 0; attempt < 3; attempt++) {
                const wait = this.lastRequest + MusicCatalog.MIN_INTERVAL_MS - Date.now();
                if (wait > 0) await sleep(wait);
                this.lastRequest = Date.now();

                let json;
                try {
                    json = Http.server ? JSON.parse(await Http.viaServer(url, 15000)) : await Http.jsonp(url);
                } catch {
                    json = null;
                }
                if (!json) return null;
                // Si se supera el límite, Deezer responde {"error":{"code":4,...}}: se espera y se reintenta.
                if (json.error?.code === 4) await sleep(5000);
                else return json;
            }
            return null;
        };
        const result = this.gate.then(run);
        this.gate = result.catch(() => null);
        return result;
    }

    firstData(root) {
        return Array.isArray(root.data) && root.data.length > 0 ? root.data[0] : null;
    }

    normalize(text) {
        return text.trim().toLowerCase();
    }
}
