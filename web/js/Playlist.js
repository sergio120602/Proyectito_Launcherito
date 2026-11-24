'use strict';

/** Comparación sin distinguir mayúsculas (sí tildes), como CurrentCultureIgnoreCase en Windows. */
const collator = new Intl.Collator('es', { sensitivity: 'accent' });

/**
 * Lista de reproducción. Guarda las canciones en orden alfabético y mantiene aparte el orden en que
 * se reproducen, que puede ser alfabético o aleatorio.
 */
class Playlist {
    constructor(titleOf) {
        this.titleOf = titleOf;
        this.songs = [];          // claves en orden alfabético
        this.known = new Set();
        this.order = [];          // índices de songs en orden de reproducción
        /** Posición (empezando en 0) de la canción actual en el orden de reproducción. */
        this.position = -1;
        this._shuffle = true;     // el aleatorio empieza activado
    }

    get count() { return this.songs.length; }

    /** Todas las canciones en orden alfabético (el orden del mosaico). */
    get songList() { return this.songs; }

    get current() { return this.position < 0 ? null : this.songs[this.order[this.position]]; }

    /**
     * true: las canciones se reproducen desordenadas. false: en orden alfabético. Al cambiarlo, la
     * canción actual sigue sonando y solo cambia cuál va después.
     */
    get shuffle() { return this._shuffle; }

    set shuffle(value) {
        if (this._shuffle === value) return;
        this._shuffle = value;
        this.rebuildOrder(this.current);
    }

    /** Añade canciones ignorando las repetidas. Devuelve cuántas se han añadido. */
    add(keys) {
        let added = 0;
        for (const key of keys) {
            if (!this.known.has(key)) {
                this.known.add(key);
                this.songs.push(key);
                added++;
            }
        }
        if (added > 0) {
            const current = this.current;
            this.songs.sort((a, b) => this.compareTitles(a, b));
            this.rebuildOrder(current);
        }
        return added;
    }

    /** Quita canciones de la lista, salvo la que está sonando. Devuelve cuántas se han quitado. */
    remove(keys) {
        const current = this.current;
        const gone = new Set();
        for (const key of keys) {
            if (key !== current && this.known.delete(key)) gone.add(key);
        }
        if (gone.size > 0) {
            this.songs = this.songs.filter(key => !gone.has(key));
            this.rebuildOrder(current);
        }
        return gone.size;
    }

    /** Hace que la canción indicada sea la actual sin cambiar el orden de reproducción. */
    jumpTo(key) {
        const index = this.songs.indexOf(key);
        if (index >= 0) this.position = this.order.indexOf(index);
        return this.current;
    }

    next() { return this.move(+1); }

    previous() { return this.move(-1); }

    /** Quita la canción actual (p. ej. si no se puede reproducir) y pasa a la siguiente. */
    removeCurrent() {
        if (this.position < 0) return null;
        const removed = this.order[this.position];
        this.known.delete(this.songs[removed]);
        this.songs.splice(removed, 1);
        this.order.splice(this.position, 1);
        for (let i = 0; i < this.order.length; i++) {
            if (this.order[i] > removed) this.order[i]--;
        }
        if (this.order.length === 0) this.position = -1;
        else if (this.position >= this.order.length) this.position = 0;
        return this.current;
    }

    /** Orden alfabético por título, el mismo que el del mosaico. */
    compareTitles(a, b) {
        return collator.compare(this.titleOf(a), this.titleOf(b));
    }

    move(step) {
        if (this.order.length === 0) return null;
        // Al llegar al final vuelve al principio (y al revés).
        this.position = (this.position + step + this.order.length) % this.order.length;
        return this.current;
    }

    rebuildOrder(current) {
        this.order = this.songs.map((_, i) => i);
        const currentIndex = current == null ? -1 : this.songs.indexOf(current);
        if (this._shuffle) {
            for (let i = this.order.length - 1; i > 0; i--) {   // Fisher-Yates
                const j = Math.floor(Math.random() * (i + 1));
                [this.order[i], this.order[j]] = [this.order[j], this.order[i]];
            }
            // La canción que está sonando pasa a ser la primera del nuevo orden aleatorio.
            if (currentIndex >= 0) {
                this.order.splice(this.order.indexOf(currentIndex), 1);
                this.order.unshift(currentIndex);
            }
        }
        this.position = currentIndex >= 0 ? this.order.indexOf(currentIndex) : this.order.length > 0 ? 0 : -1;
    }
}
