'use strict';

/**
 * Datos pequeños (canciones guardadas, caché de Deezer, orden del mosaico, ajustes) en el
 * localStorage del navegador: son los .json que en Windows van a %LOCALAPPDATA%\Launcherito.
 */
const Settings = {
    read(name, fallback) {
        try {
            const text = localStorage.getItem('launcherito.' + name);
            return text == null ? fallback : JSON.parse(text);
        } catch {
            return fallback;   // dañado o el navegador no deja usar localStorage: se empieza de cero
        }
    },

    /** Devuelve false si no se ha podido guardar (sin espacio o localStorage bloqueado). */
    write(name, value) {
        try {
            localStorage.setItem('launcherito.' + name, JSON.stringify(value));
            return true;
        } catch {
            return false;
        }
    },
};

/**
 * Los .mp3 cargados. Una página web no puede volver a abrir un archivo del disco por su ruta, así que
 * se guarda una copia en IndexedDB (la base de datos del navegador) para que «Tus canciones» siga
 * funcionando al volver a abrir la página. Mientras la página está abierta se usan los propios archivos.
 */
const FileStore = {
    db: null,
    keys: new Set(),        // claves guardadas en IndexedDB
    session: new Map(),     // clave → File/Blob ya a mano

    async open() {
        try {
            this.db = await new Promise((resolve, reject) => {
                const request = indexedDB.open('launcherito', 1);
                request.onupgradeneeded = () => request.result.createObjectStore('mp3');
                request.onsuccess = () => resolve(request.result);
                request.onerror = () => reject(request.error);
            });
            for (const key of await this.request(store => store.getAllKeys())) this.keys.add(key);
            // Pide al navegador que no borre los datos aunque le falte espacio (puede decir que no).
            navigator.storage?.persist?.().catch(() => {});
        } catch {
            this.db = null;   // sin IndexedDB (p. ej. navegación privada): las canciones solo duran esta sesión
        }
    },

    request(action, mode = 'readonly') {
        return new Promise((resolve, reject) => {
            const transaction = this.db.transaction('mp3', mode);
            const request = action(transaction.objectStore('mp3'));
            transaction.oncomplete = () => resolve(request.result);
            transaction.onerror = transaction.onabort = () => reject(transaction.error);
        });
    },

    has(key) {
        return this.session.has(key) || this.keys.has(key);
    },

    /** Guarda el archivo. Devuelve false si solo se ha podido dejar para esta sesión. */
    async put(key, file) {
        this.session.set(key, file);
        if (!this.db || this.keys.has(key)) return this.keys.has(key);
        try {
            await this.request(store => store.put(file, key), 'readwrite');
            this.keys.add(key);
            return true;
        } catch {
            return false;   // sin espacio
        }
    },

    async get(key) {
        const file = this.session.get(key);
        if (file) return file;
        if (!this.db || !this.keys.has(key)) return null;
        try {
            const blob = await this.request(store => store.get(key));
            if (blob) this.session.set(key, blob);   // un Blob de IndexedDB no se carga entero en memoria
            return blob ?? null;
        } catch {
            return null;
        }
    },

    async remove(keys) {
        for (const key of keys) this.session.delete(key);
        if (!this.db) return;
        try {
            await this.request(store => { let last; for (const key of keys) last = store.delete(key); return last ?? store.count(); }, 'readwrite');
            for (const key of keys) this.keys.delete(key);
        } catch {
            // se reintentará la próxima vez que se borren
        }
    },
};
