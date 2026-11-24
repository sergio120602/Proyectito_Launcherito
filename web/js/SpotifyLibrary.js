'use strict';

/** Canción de una lista de Spotify. Dentro de Launcherito se identifica por key. */
class SpotifyTrack {
    constructor(id, title, artists, durationMs) {
        this.id = id;
        this.title = title;
        this.artists = artists;
        this.durationMs = durationMs;
    }

    get key() { return SpotifyLibrary.KEY_PREFIX + this.id; }

    /** Primer artista ("Drake, Future" → "Drake"), el que se usa para agrupar y para Deezer. */
    get mainArtist() { return this.artists.split(',')[0].trim(); }
}

/** No se ha podido leer una lista de Spotify; el mensaje se muestra tal cual. */
class SpotifyImportError extends Error {}

/**
 * Lee listas públicas y álbumes de Spotify sin cuenta ni clave. La API oficial exige desde febrero
 * de 2026 que el dueño de la app tenga Premium, así que se usa la página que Spotify ofrece para
 * insertar listas en otras webs (trae título y artistas de cada canción, hasta 100 canciones) y su
 * servicio oEmbed para las portadas. Spotify no da el audio completo: si no se tiene el .mp3, la
 * canción suena con su vídeo de YouTube (YouTubeLinks).
 */
const SpotifyLibrary = {
    KEY_PREFIX: 'spotify:track:',

    tracks: new Map(),
    covers: new Map(Object.entries(Settings.read('portadas-spotify', {}))),   // id → dirección de la portada

    // open.spotify.com/playlist/ID, open.spotify.com/intl-es/album/ID?si=…, spotify:playlist:ID
    linkPattern: /(?:open\.spotify\.com\/(?:intl-[a-z-]+\/)?|spotify:)(?<type>playlist|album)[/:](?<id>[A-Za-z0-9]{22})/i,
    dataPattern: /<script id="__NEXT_DATA__" type="application\/json">(?<json>.*?)<\/script>/s,

    isSpotify(key) { return key.startsWith(this.KEY_PREFIX); },

    tryGet(key) { return this.tracks.get(key); },

    /** Vuelve a dar a conocer una canción guardada («Tus canciones») sin pedirla a Spotify. */
    register(track) { this.tracks.set(track.key, track); },

    /** Saca el tipo ("playlist" o "album") y el id de un enlace de Spotify, o null si no lo es. */
    parseLink(text) {
        const match = text ? this.linkPattern.exec(text) : null;
        return match ? { type: match.groups.type.toLowerCase(), id: match.groups.id } : null;
    },

    /** Descarga la lista o el álbum y registra sus canciones. */
    async load(type, id) {
        if (!Http.server) {
            throw new SpotifyImportError(
                'El navegador no deja que una página lea Spotify por su cuenta. Abre Launcherito con su ' +
                'servidor (node servidor.mjs, en la carpeta web) para añadir listas.');
        }
        let html;
        try {
            html = await Http.viaServer(`https://open.spotify.com/embed/${type}/${id}`);
        } catch {
            throw new SpotifyImportError('No se puede conectar con Spotify. Comprueba la conexión a Internet.');
        }

        const data = this.dataPattern.exec(html);
        if (!data) throw new SpotifyImportError('Spotify ha cambiado su página y no se puede leer la lista.');

        const notFound = new SpotifyImportError(type === 'album'
            ? 'No se encuentra ese álbum en Spotify.'
            : 'No se encuentra la lista. Comprueba el enlace y que la lista sea pública.');
        let entity;
        try {
            entity = JSON.parse(data.groups.json)?.props?.pageProps?.state?.data?.entity;
        } catch {
            throw new SpotifyImportError('Spotify ha cambiado su página y no se puede leer la lista.');
        }
        if (!entity || !Array.isArray(entity.trackList)) throw notFound;

        const result = [];
        for (const item of entity.trackList) {
            const uri = item?.uri;
            if (typeof uri !== 'string' || !uri.startsWith(this.KEY_PREFIX)) continue;   // episodios de pódcast u otros
            const track = new SpotifyTrack(
                uri.slice(this.KEY_PREFIX.length),
                typeof item.title === 'string' ? item.title : 'Sin título',
                typeof item.subtitle === 'string' && item.subtitle ? item.subtitle : SongTags.UNKNOWN_ARTIST,
                typeof item.duration === 'number' ? item.duration : 0,
            );
            this.tracks.set(track.key, track);
            result.push(track);
        }
        return {
            name: typeof entity.name === 'string' ? entity.name : 'Lista de Spotify',
            owner: typeof entity.subtitle === 'string' ? entity.subtitle : '',
            tracks: result,
        };
    },

    /**
     * Dirección de la portada (640 px) de una canción, o null si no se puede saber. oEmbed es el
     * servicio público de Spotify para mostrar una canción en otra web (sí deja leerlo desde una
     * página) y da la portada a 300 px; cambiando el código de tamaño se obtiene la de 640 px. La
     * dirección se recuerda, así solo se pregunta una vez.
     */
    async coverUrl(track) {
        const known = this.covers.get(track.id);
        if (known) return known;
        try {
            const oembed = await Http.fetchJson('https://open.spotify.com/oembed?url=' +
                encodeURIComponent(`https://open.spotify.com/track/${track.id}`), 10000);
            const url = typeof oembed.thumbnail_url === 'string'
                ? oembed.thumbnail_url.replace('ab67616d00001e02', 'ab67616d0000b273') : null;
            if (url) {
                this.covers.set(track.id, url);
                Settings.write('portadas-spotify', Object.fromEntries(this.covers));
            }
            return url;
        } catch {
            return null;   // sin portada esta vez; se reintentará la próxima vez que se necesite
        }
    },

    /**
     * Clave para reconocer la misma canción en Spotify y en un .mp3 aunque se escriba distinto: sin
     * mayúsculas, tildes, signos ni añadidos como "(feat. X)" o "- Remastered".
     */
    matchKey(artist, title) {
        let main = artist;
        for (const separator of [',', '&', ';', ' feat', ' ft.', ' x ']) {
            const cut = main.toLowerCase().indexOf(separator);
            if (cut > 0) main = main.slice(0, cut);
        }
        return this.simplify(main) + '|' + this.simplify(cleanTitle(title));
    },

    simplify(text) {
        return text.normalize('NFD').replace(/[^\p{L}\p{N}]/gu, '').toLowerCase();
    },
};
