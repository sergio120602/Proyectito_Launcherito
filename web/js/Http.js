'use strict';

/** No se ha podido descargar (sin conexión, sin el servidor de Launcherito o respuesta de error). */
class HttpError extends Error {}

/**
 * Peticiones a Deezer, Spotify y YouTube. Un navegador no deja que una página lea otras webs salvo que
 * ellas lo permitan (CORS): Deezer lo permite con JSONP y las portadas de Spotify (oEmbed) directamente,
 * pero la página de las listas de Spotify y la búsqueda de YouTube no. Para esas hace falta abrir
 * Launcherito con su servidor (servidor.mjs), que las pide en nombre de la página.
 */
const Http = {
    /** true si la página la sirve servidor.mjs (y se puede leer Spotify y YouTube). */
    server: false,
    callbacks: 0,

    async detectServer() {
        if (location.protocol === 'file:') return (this.server = false);
        try {
            const response = await fetch('api/estado', { cache: 'no-store' });
            this.server = response.ok && (await response.json()).launcherito === true;
        } catch {
            this.server = false;
        }
        return this.server;
    },

    /** Texto de una dirección pedida a través del servidor de Launcherito. */
    async viaServer(url, timeoutMs = 20000) {
        if (!this.server) throw new HttpError('Sin el servidor de Launcherito');
        return this.fetchText('api/proxy?url=' + encodeURIComponent(url), timeoutMs);
    },

    async fetchText(url, timeoutMs = 20000) {
        const abort = new AbortController();
        const timer = setTimeout(() => abort.abort(), timeoutMs);
        try {
            const response = await fetch(url, { signal: abort.signal });
            if (!response.ok) throw new HttpError('HTTP ' + response.status);
            return await response.text();
        } catch (e) {
            throw e instanceof HttpError ? e : new HttpError(e.message);
        } finally {
            clearTimeout(timer);
        }
    },

    async fetchJson(url, timeoutMs) {
        try {
            return JSON.parse(await this.fetchText(url, timeoutMs));
        } catch (e) {
            throw e instanceof HttpError ? e : new HttpError('Respuesta no válida');
        }
    },

    /**
     * JSONP: la respuesta llega como un script que llama a una función nuestra. Es lo que permite
     * usar la API de Deezer desde una página. Devuelve null si falla.
     */
    jsonp(url, timeoutMs = 15000) {
        return new Promise(resolve => {
            const name = '__launcherito' + (++this.callbacks);
            const script = document.createElement('script');
            const finish = value => {
                clearTimeout(timer);
                window[name] = () => {};   // por si responde tarde
                script.remove();
                resolve(value);
            };
            const timer = setTimeout(() => finish(null), timeoutMs);
            window[name] = data => finish(data);
            script.onerror = () => finish(null);
            script.src = url + (url.includes('?') ? '&' : '?') + 'output=jsonp&callback=' + name;
            document.head.appendChild(script);
        });
    },
};

const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
