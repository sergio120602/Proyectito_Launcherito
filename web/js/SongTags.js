'use strict';

/**
 * Lector de etiquetas ID3 de los .mp3 (lo que en Windows hace TagLibSharp): título, artista y
 * carátula. Admite ID3v2.2, 2.3 y 2.4 al principio del archivo e ID3v1 al final. Solo se lee la
 * parte del archivo con las etiquetas, no el audio.
 */
const Id3 = {
    async read(blob, wantPicture) {
        const result = { title: '', artist: '', picture: null };
        const head = new Uint8Array(await blob.slice(0, 10).arrayBuffer());
        if (head.length === 10 && head[0] === 0x49 && head[1] === 0x44 && head[2] === 0x33 && head[3] >= 2 && head[3] <= 4) {
            this.readV2(head, new Uint8Array(await blob.slice(10, 10 + this.synchsafe(head, 6)).arrayBuffer()), wantPicture, result);
        }
        if ((!result.title || !result.artist) && blob.size >= 128) {
            // ID3v1: 128 bytes al final, con título y artista de 30 caracteres.
            const tail = new Uint8Array(await blob.slice(blob.size - 128).arrayBuffer());
            if (tail[0] === 0x54 && tail[1] === 0x41 && tail[2] === 0x47) {
                result.title ||= this.decode(tail.subarray(3, 33), 0);
                result.artist ||= this.decode(tail.subarray(33, 63), 0);
            }
        }
        return result;
    },

    readV2(head, data, wantPicture, result) {
        const version = head[3];
        const flags = head[5];
        // Desincronización de todo el tag (en la 2.4 va por marco).
        if (version < 4 && (flags & 0x80)) data = this.resync(data);
        let pos = 0;
        if (version >= 3 && (flags & 0x40)) {   // cabecera extendida: se salta
            pos = version === 3 ? 4 + this.int32(data, 0) : this.synchsafe(data, 0);
        }
        const idLength = version === 2 ? 3 : 4;
        const headerLength = version === 2 ? 6 : 10;
        let picture = null;

        while (pos + headerLength <= data.length) {
            const id = String.fromCharCode(...data.subarray(pos, pos + idLength));
            if (!/^[A-Z0-9]+$/.test(id)) break;   // relleno de ceros al final del tag
            const size = version === 2 ? (data[pos + 3] << 16 | data[pos + 4] << 8 | data[pos + 5])
                : version === 4 ? this.synchsafe(data, pos + 4) : this.int32(data, pos + 4);
            const format = version >= 3 ? data[pos + 9] : 0;
            let body = data.subarray(pos + headerLength, pos + headerLength + size);
            pos += headerLength + size;

            if (version === 3 && (format & 0xC0)) continue;   // comprimido o cifrado
            if (version === 4) {
                if (format & 0x0C) continue;
                if (format & 0x01) body = body.subarray(4);   // indicador de longitud
                if (format & 0x02) body = this.resync(body);
            }
            if (body.length < 2) continue;

            if (id === 'TIT2' || id === 'TT2') result.title ||= this.text(body);
            else if (id === 'TPE1' || id === 'TP1') result.artist ||= this.text(body);
            else if (wantPicture && (id === 'APIC' || id === 'PIC')) {
                const found = this.picture(body, id === 'PIC');
                // La portada delantera (tipo 3) gana a cualquier otra imagen.
                if (found && (!picture || (found.type === 3 && picture.type !== 3))) picture = found;
            }
        }
        if (picture) result.picture = new Blob([picture.data], { type: picture.mime });
    },

    text(body) {
        return this.decode(body.subarray(1), body[0]).split('\0')[0].trim();
    },

    picture(body, v22) {
        const encoding = body[0];
        let i = 1;
        let mime;
        if (v22) {
            mime = String.fromCharCode(...body.subarray(1, 4)).toLowerCase() === 'png' ? 'image/png' : 'image/jpeg';
            i = 4;
        } else {
            const start = i;
            while (i < body.length && body[i] !== 0) i++;
            mime = String.fromCharCode(...body.subarray(start, i)).toLowerCase();
            i++;
            if (mime === '-->') return null;   // enlace a una imagen externa
            if (!mime.includes('/')) mime = mime === 'png' ? 'image/png' : 'image/jpeg';
        }
        const type = body[i++];
        // Descripción, terminada en un cero (dos en UTF-16).
        if (encoding === 1 || encoding === 2) {
            while (i + 1 < body.length && !(body[i] === 0 && body[i + 1] === 0)) i += 2;
            i += 2;
        } else {
            while (i < body.length && body[i] !== 0) i++;
            i++;
        }
        return i < body.length ? { type, mime, data: body.slice(i) } : null;
    },

    decode(bytes, encoding) {
        let label = 'latin1';
        if (encoding === 3) label = 'utf-8';
        else if (encoding === 2) label = 'utf-16be';
        else if (encoding === 1) label = bytes[0] === 0xFE && bytes[1] === 0xFF ? 'utf-16be' : 'utf-16le';
        return new TextDecoder(label).decode(bytes).replace(/^﻿/, '').replace(/\0+$/, '').trim();
    },

    /** Quita los ceros que la desincronización mete detrás de cada 0xFF. */
    resync(bytes) {
        const out = new Uint8Array(bytes.length);
        let n = 0;
        for (let i = 0; i < bytes.length; i++) {
            out[n++] = bytes[i];
            if (bytes[i] === 0xFF && bytes[i + 1] === 0x00) i++;
        }
        return out.subarray(0, n);
    },

    synchsafe(b, at) {
        return (b[at] & 0x7F) << 21 | (b[at + 1] & 0x7F) << 14 | (b[at + 2] & 0x7F) << 7 | (b[at + 3] & 0x7F);
    },

    int32(b, at) {
        return ((b[at] << 24) | (b[at + 1] << 16) | (b[at + 2] << 8) | b[at + 3]) >>> 0;
    },
};

/**
 * Título, artista y carátula de cada canción. Un .mp3 se identifica por "mp3:nombre|tamaño|fecha"
 * (una página no conoce la ruta del archivo) y una canción de Spotify por "spotify:track:…".
 */
const SongTags = {
    /** Artista que se muestra cuando la canción no lo trae en sus etiquetas. */
    UNKNOWN_ARTIST: 'Artista desconocido',

    keyOf(file) {
        return `mp3:${file.name}|${file.size}|${file.lastModified}`;
    },

    /** Título que se puede mostrar antes de leer las etiquetas: el de Spotify o el nombre del archivo. */
    fallbackTitle(key) {
        const track = SpotifyLibrary.tryGet(key);
        if (track) return track.title;
        return key.replace(/^mp3:/, '').split('|')[0].replace(/\.mp3$/i, '');
    },

    /** Lee título y artista. */
    async read(key) {
        const track = SpotifyLibrary.tryGet(key);
        // El artista lleva la marca de YouTube porque suena con su vídeo (no hay .mp3).
        if (track) return { title: track.title, artist: `${track.artists}  ·  vídeo de YouTube` };
        let title = this.fallbackTitle(key);
        let artist = this.UNKNOWN_ARTIST;
        const blob = await FileStore.get(key);
        if (blob) {
            try {
                const tags = await Id3.read(blob, false);
                if (tags.title) title = tags.title;
                if (tags.artist) artist = tags.artist;
            } catch {
                // etiquetas ilegibles: se usa el nombre del archivo
            }
        }
        return { title, artist };
    },

    /** Dirección de la carátula (la incrustada en el .mp3 o la de Spotify), o null si no tiene. */
    async coverUrl(key) {
        const track = SpotifyLibrary.tryGet(key);
        if (track) return SpotifyLibrary.coverUrl(track);
        const blob = await FileStore.get(key);
        if (!blob) return null;
        try {
            const picture = (await Id3.read(blob, true)).picture;
            return picture ? URL.createObjectURL(picture) : null;
        } catch {
            return null;
        }
    },
};

/**
 * Carátulas ya leídas. Como mucho se leen 4 a la vez y las que dejan de hacer falta (portadas lejos de
 * la pantalla) se sueltan con trim, así una biblioteca grande no dispara la memoria.
 */
const Covers = {
    cache: new Map(),   // clave → { promise, url }
    queue: [],
    running: 0,
    MAX_PARALLEL: 4,

    get(key) {
        let entry = this.cache.get(key);
        if (!entry) {
            entry = { url: undefined };
            entry.promise = new Promise(resolve => this.queue.push(async () => {
                try {
                    entry.url = await SongTags.coverUrl(key);
                } catch {
                    entry.url = null;
                }
                resolve(entry.url);
            }));
            this.cache.set(key, entry);
            this.pump();
        }
        return entry.promise;
    },

    /** Dirección si ya está leída (undefined si aún no). */
    peek(key) {
        return this.cache.get(key)?.url;
    },

    pump() {
        while (this.running < this.MAX_PARALLEL && this.queue.length) {
            const job = this.queue.shift();
            this.running++;
            job().finally(() => {
                this.running--;
                this.pump();
            });
        }
    },

    /** Suelta las carátulas que no estén en [keep]. */
    trim(keep) {
        for (const [key, entry] of this.cache) {
            if (keep.has(key) || entry.url === undefined) continue;
            if (entry.url?.startsWith('blob:')) URL.revokeObjectURL(entry.url);
            this.cache.delete(key);
        }
    },
};
