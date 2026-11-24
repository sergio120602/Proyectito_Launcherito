// Servidor de Launcherito para la versión web. Sirve la página y pide en su nombre la página de las
// listas de Spotify, la búsqueda de YouTube y la API de Deezer: el navegador no deja que una página
// las lea por su cuenta (CORS). Solo atiende a este ordenador y solo a esas tres webs.
//
// Uso:  node servidor.mjs          → http://localhost:8080
//       PORT=9000 node servidor.mjs
// Necesita Node 18 o posterior; no hace falta instalar nada más.

import { createServer } from 'node:http';
import { readFile, stat } from 'node:fs/promises';
import { extname, join, normalize, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = fileURLToPath(new URL('.', import.meta.url));
const PORT = Number(process.env.PORT) || 8080;
const HOST = '127.0.0.1';

const TYPES = {
    '.html': 'text/html; charset=utf-8',
    '.css': 'text/css; charset=utf-8',
    '.js': 'text/javascript; charset=utf-8',
    '.mjs': 'text/javascript; charset=utf-8',
    '.json': 'application/json; charset=utf-8',
    '.png': 'image/png',
    '.jpg': 'image/jpeg',
    '.svg': 'image/svg+xml',
    '.ico': 'image/x-icon',
};

// Las únicas direcciones que se piden en nombre de la página, con las cabeceras que hacen falta.
const ALLOWED = [
    { host: 'open.spotify.com', path: /^\/embed\/(playlist|album)\/[A-Za-z0-9]{22}$/, headers: { 'User-Agent': 'Launcherito' } },
    {
        host: 'www.youtube.com', path: /^\/results$/, headers: {
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)',
            // Sin esta cookie, en Europa YouTube responde con su página de aceptar cookies.
            'Cookie': 'SOCS=CAI',
        },
    },
    // Accept-Language: nombres de géneros en español.
    { host: 'api.deezer.com', path: /^\/(search|search\/artist|album\/\d+|genre\/\d+)$/, headers: { 'User-Agent': 'Launcherito', 'Accept-Language': 'es' } },
];

async function proxy(request, response, query) {
    let target;
    try {
        target = new URL(query.get('url') ?? '');
    } catch {
        return send(response, 400, 'Dirección no válida');
    }
    const rule = ALLOWED.find(r => target.protocol === 'https:' && target.hostname === r.host && r.path.test(target.pathname));
    if (!rule) return send(response, 403, 'Launcherito no pide esa dirección');

    try {
        const upstream = await fetch(target, { headers: rule.headers, signal: AbortSignal.timeout(20000) });
        const body = Buffer.from(await upstream.arrayBuffer());
        response.writeHead(upstream.status, {
            'Content-Type': upstream.headers.get('content-type') ?? 'text/plain',
            'Cache-Control': 'no-store',
        });
        response.end(body);
    } catch (e) {
        send(response, 502, `No se puede conectar con ${rule.host}: ${e.message}`);
    }
}

async function serveFile(response, pathname) {
    // Sin salir de la carpeta web (nada de "../").
    const relative = normalize(decodeURIComponent(pathname)).replace(/^([/\\])+/, '');
    const file = join(ROOT, relative || 'index.html');
    if (!file.startsWith(ROOT) || relative.split(sep).some(part => part.startsWith('.'))) return send(response, 403, 'Prohibido');
    try {
        const info = await stat(file);
        const path = info.isDirectory() ? join(file, 'index.html') : file;
        const body = await readFile(path);
        response.writeHead(200, { 'Content-Type': TYPES[extname(path).toLowerCase()] ?? 'application/octet-stream', 'Cache-Control': 'no-cache' });
        response.end(body);
    } catch {
        send(response, 404, 'No existe');
    }
}

function send(response, status, text) {
    response.writeHead(status, { 'Content-Type': 'text/plain; charset=utf-8' });
    response.end(text);
}

const server = createServer((request, response) => {
    const url = new URL(request.url, `http://${request.headers.host ?? 'localhost'}`);
    if (request.method !== 'GET' && request.method !== 'HEAD') return send(response, 405, 'Solo GET');
    if (url.pathname === '/api/estado') {
        response.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
        return response.end(JSON.stringify({ launcherito: true }));
    }
    if (url.pathname === '/api/proxy') return proxy(request, response, url.searchParams);
    return serveFile(response, url.pathname);
});

server.on('error', e => {
    console.error(e.code === 'EADDRINUSE'
        ? `El puerto ${PORT} ya está en uso. Prueba con otro: PORT=9000 node servidor.mjs`
        : e.message);
    process.exit(1);
});

server.listen(PORT, HOST, () => {
    console.log(`🎵 Launcherito está en http://localhost:${PORT}  (Ctrl+C para pararlo)`);
});
