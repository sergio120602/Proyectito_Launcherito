'use strict';

const GAP = 8;
// Ancho que necesitan los controles dentro de la portada que suena (más con la letra grande).
const CONTROLS_MIN_WIDTH = 300;
const CONTROLS_MIN_WIDTH_LARGE = 340;
const GRIP_SIZE = 34;          // zona del tirador (algo mayor que el dibujo)
const ANIMATED_INTROS = 20;    // solo las 20 primeras entran con su animación; las demás, con un fundido rápido
const INTROS = ['spin', 'grow', 'crash', 'drop', 'skid', 'card', 'flicker'];

const rect = (left, top, width, height) => ({ left, top, right: left + width, bottom: top + height, width, height });
const contains = (r, p) => p.x >= r.left && p.x < r.right && p.y >= r.top && p.y < r.bottom;
const overlaps = (a, b) => a.left < b.right && b.left < a.right && a.top < b.bottom && b.top < a.bottom;
const clamp = (v, min, max) => Math.min(max, Math.max(min, v));

/** Columnas y tamaño de celda del mosaico (en píxeles), según el ancho de la ventana. */
class MosaicMetrics {
    constructor(columns, cell, gap, controlsMin) {
        this.columns = columns;
        this.cell = cell;
        this.gap = gap;
        /** La que suena ocupa lo necesario para que quepan los controles (en un móvil, todo el ancho). */
        this.activeMinSpan = clamp(Math.ceil((controlsMin + gap) / (cell + gap)), 2, columns);
    }

    /** Cuántas celdas de lado ocupa una pieza que mide size (como mínimo 1, como mucho todas las columnas). */
    spanForSize(size) {
        return clamp(Math.round((size + this.gap) / (this.cell + this.gap)), 1, this.columns);
    }

    /** Hueco que ocuparía una pieza de span celdas puesta donde empieza slot. */
    areaFrom(slot, span) {
        const step = this.cell + this.gap;
        const column = Math.max(0, Math.min(Math.round(slot.left / step), this.columns - span));
        const size = span * this.cell + (span - 1) * this.gap;
        return rect(column * step, Math.round(slot.top / step) * step, size, size);
    }

    slot(row, column, span) {
        const size = span * this.cell + (span - 1) * this.gap;
        return rect(column * (this.cell + this.gap), row * (this.cell + this.gap), size, size);
    }
}

/**
 * Reparte las piezas en una cuadrícula de celdas cuadradas: cada una ocupa span x span celdas y los
 * huecos que dejan las grandes se rellenan con las pequeñas siguientes (como "grid-auto-flow: dense"
 * en CSS). Devuelve la fila y la columna de cada una, y cuántas filas ocupa el mosaico.
 */
function packMosaic(spans, columns) {
    const occupied = [];
    let firstFreeRow = 0;
    let rows = 0;
    const spots = [];
    const fits = (row, column, span) => {
        for (let r = row; r < row + span; r++) for (let c = column; c < column + span; c++) if (occupied[r][c]) return false;
        return true;
    };
    for (const span of spans) {
        // Las filas ya completas no se vuelven a revisar.
        while (firstFreeRow < occupied.length && occupied[firstFreeRow].every(Boolean)) firstFreeRow++;
        let spot = null;
        for (let row = firstFreeRow; !spot; row++) {
            while (occupied.length < row + span) occupied.push(new Array(columns).fill(false));
            for (let column = 0; column <= columns - span; column++) {
                if (fits(row, column, span)) {
                    spot = [row, column];
                    break;
                }
            }
        }
        const [r0, c0] = spot;
        for (let r = r0; r < r0 + span; r++) for (let c = c0; c < c0 + span; c++) occupied[r][c] = true;
        rows = Math.max(rows, r0 + span);
        spots.push(spot);
    }
    return { spots, rows };
}

/**
 * Modo edición del mosaico: una portada se coge y se arrastra a otro sitio (las demás se apartan), y
 * con el tirador de su esquina se agranda o se encoge de celda en celda. Al agrandarla, las grandes que
 * quedan debajo se encogen para dejarle sitio; al encogerla, la siguiente crece y ocupa el hueco. Con
 * ratón se coge arrastrando; en una pantalla táctil, manteniéndola pulsada (arrastrar sin más desplaza
 * el mosaico).
 */
class MosaicEditor {
    constructor(vm) {
        this.vm = vm;
        this.metrics = null;
        this.slots = new Map();   // dónde está cada portada ahora mismo

        this.dragged = null;
        this.dragPoint = { x: 0, y: 0 };   // puntero, en coordenadas del mosaico
        this.grab = { x: 0, y: 0 };        // dónde se ha cogido, desde la esquina de la portada
        this.lastTarget = null;

        this.resized = null;
        this.resizeOrigin = null;
        this.startSpan = 1;
        this.before = new Map();   // cómo estaba todo al coger el tirador
    }

    tileAt(point, except = null) {
        for (const [tile, slot] of this.slots) if (tile !== except && contains(slot, point)) return tile;
        return null;
    }

    onGrip(tile, point) {
        const slot = this.slots.get(tile);
        return !!slot && point.x >= slot.right - GRIP_SIZE && point.y >= slot.bottom - GRIP_SIZE;
    }

    effectiveSpan(tile) {
        const m = this.metrics;
        if (!m) return tile.baseSpan;
        const min = tile.key === this.vm.currentKey ? m.activeMinSpan : 1;
        return clamp(Math.max(min, tile.baseSpan), 1, m.columns);
    }

    // ───────────────────────────── Mover ─────────────────────────────

    beginDrag(tile, point) {
        const slot = this.slots.get(tile);
        if (!slot) return;
        this.dragged = tile;
        this.grab = { x: point.x - slot.left, y: point.y - slot.top };
        this.dragPoint = point;
    }

    drag(point) {
        const tile = this.dragged;
        if (!tile) return;
        this.dragPoint = point;
        const target = this.tileAt(point, tile);
        if (target !== this.lastTarget) {
            this.lastTarget = target;
            if (target) this.vm.moveTileBefore(tile, target);
        }
    }

    // ───────────────────────────── Tamaño ─────────────────────────────

    beginResize(tile) {
        const slot = this.slots.get(tile);
        if (!slot) return;
        this.resizeOrigin = slot;
        this.resized = tile;
        this.startSpan = this.effectiveSpan(tile);
        this.before = new Map([...this.slots].map(([t, s]) => [t, { span: t.baseSpan, slot: s }]));
    }

    resize(point) {
        const tile = this.resized;
        const m = this.metrics;
        if (!tile || !m) return;
        // El tamaño lo marca el puntero respecto a la esquina de arriba a la izquierda, por la diagonal.
        const size = Math.max(point.x - this.resizeOrigin.left, point.y - this.resizeOrigin.top);
        const min = tile.key === this.vm.currentKey ? m.activeMinSpan : 1;
        const span = Math.max(min, m.spanForSize(size));
        if (span === this.effectiveSpan(tile)) return;

        // Siempre desde como estaba al coger el tirador, así ir y volver no deja cambios sueltos.
        for (const [other, was] of this.before) other.baseSpan = was.span;
        tile.baseSpan = span;
        if (span > this.startSpan) this.shrinkCovered(tile, span, m);
        else if (span < this.startSpan) this.growNext(tile, this.startSpan - span, m);
        this.vm.changed();
    }

    /** Las grandes que quedarían debajo de la portada agrandada pasan a ocupar una celda. */
    shrinkCovered(tile, span, m) {
        const full = m.areaFrom(this.resizeOrigin, span);
        const area = rect(full.left + 1, full.top + 1, full.width - 2, full.height - 2);   // las que solo tocan el borde no cuentan
        for (const [other, was] of this.before) {
            if (other !== tile && other.baseSpan > 1 && overlaps(was.slot, area)) other.baseSpan = 1;
        }
    }

    /** La siguiente portada (o la anterior, si es la última) crece lo que ha encogido esta. */
    growNext(tile, freed, m) {
        const tiles = this.vm.tiles;
        const at = tiles.indexOf(tile);
        const neighbour = [...tiles.slice(at + 1), ...tiles.slice(0, at).reverse()].find(t => t.key !== this.vm.currentKey);
        if (neighbour) neighbour.baseSpan = Math.min(m.columns, neighbour.baseSpan + freed);
    }

    // ───────────────────────────── Común ─────────────────────────────

    /** Suelta lo que se lleve cogido: la portada vuela hasta su sitio y se guarda cómo ha quedado. */
    end() {
        if (!this.dragged && !this.resized) return;
        this.dragged = null;
        this.lastTarget = null;
        this.resized = null;
        this.before = new Map();
        this.vm.rememberArrangement();
        this.vm.changed();
    }
}

/**
 * Vista mosaico: todas las canciones como portadas de distintos tamaños. La que suena se ve en grande,
 * con la barra y los controles dentro de la propia portada. Solo se cargan las carátulas cercanas a la
 * zona visible.
 */
class MosaicView {
    constructor(vm, scroll) {
        this.vm = vm;
        this.scroll = scroll;
        this.canvas = scroll.querySelector('.mosaic-canvas');
        this.editor = new MosaicEditor(vm);
        this.items = new Map();        // clave → elementos de la portada
        this.lastActive = null;
        this.introsLeft = ANIMATED_INTROS;
        this.press = null;             // portada pulsada que aún no se ha cogido
        this.pointer = null;           // último sitio del puntero (en la ventana)

        scroll.addEventListener('scroll', () => this.schedule(), { passive: true });
        new ResizeObserver(() => this.schedule()).observe(scroll);

        this.canvas.addEventListener('click', e => {
            const element = e.target.closest('.tile');
            if (!element || this.vm.isEditing || e.target.closest('.controls')) return;
            const key = element.dataset.key;
            // En modo edición pulsar no hace nada; la que suena tiene sus controles.
            if (key !== this.vm.currentKey) this.vm.selectSong(key);
        });
        this.canvas.addEventListener('pointerdown', e => this.pointerDown(e));
        window.addEventListener('pointermove', e => this.pointerMove(e));
        window.addEventListener('pointerup', () => this.pointerUp());
        window.addEventListener('pointercancel', () => this.pointerUp());
        // Con el dedo, mientras se lleva una portada el mosaico no se desplaza.
        this.canvas.addEventListener('touchmove', e => {
            if (this.editor.dragged || this.editor.resized) e.preventDefault();
        }, { passive: false });
    }

    schedule() {
        if (this.frame) return;
        this.frame = requestAnimationFrame(() => {
            this.frame = 0;
            this.render();
        });
    }

    // ───────────────────────────── Puntero ─────────────────────────────

    point(clientX, clientY) {
        const box = this.canvas.getBoundingClientRect();
        return { x: clientX - box.left, y: clientY - box.top };
    }

    pointerDown(e) {
        if (!this.vm.isEditing || e.button > 0) return;
        const p = this.point(e.clientX, e.clientY);
        const tile = this.editor.tileAt(p);
        if (!tile) return;
        this.pointer = { x: e.clientX, y: e.clientY };
        if (this.editor.onGrip(tile, p)) {
            e.preventDefault();
            this.editor.beginResize(tile);
            this.render();
            return;
        }
        const touch = e.pointerType !== 'mouse';
        if (!touch) e.preventDefault();   // sin seleccionar texto al arrastrar
        this.press = {
            tile, start: p, touch,
            // En una pantalla táctil se coge manteniéndola pulsada.
            timer: touch ? setTimeout(() => this.grabPressed(), 350) : 0,
        };
    }

    grabPressed() {
        if (!this.press) return;
        const { tile } = this.press;
        this.press = null;
        navigator.vibrate?.(15);
        this.editor.beginDrag(tile, this.point(this.pointer.x, this.pointer.y));
        this.autoScroll();
        this.render();
    }

    pointerMove(e) {
        if (!this.press && !this.editor.dragged && !this.editor.resized) return;
        this.pointer = { x: e.clientX, y: e.clientY };
        const p = this.point(e.clientX, e.clientY);
        if (this.editor.resized) {
            this.editor.resize(p);
        } else if (this.editor.dragged) {
            this.editor.drag(p);
            this.render();
        } else if (this.press) {
            const moved = Math.hypot(p.x - this.press.start.x, p.y - this.press.start.y);
            if (this.press.touch) {
                if (moved > 10) this.cancelPress();   // el dedo se mueve antes: se desplaza el mosaico
            } else if (moved > 5) {
                this.grabPressed();
                this.editor.drag(p);
            }
        }
    }

    pointerUp() {
        this.cancelPress();
        this.editor.end();
    }

    cancelPress() {
        if (this.press) clearTimeout(this.press.timer);
        this.press = null;
    }

    /** Al llevar una portada cerca del borde de arriba o de abajo, el mosaico se desplaza solo. */
    autoScroll() {
        const step = () => {
            if (!this.editor.dragged) return;
            const box = this.scroll.getBoundingClientRect();
            const y = this.pointer.y - box.top;
            const wanted = y < 56 ? -10 : y > box.height - 56 ? 10 : 0;
            if (wanted) {
                const before = this.scroll.scrollTop;
                this.scroll.scrollTop += wanted;
                if (this.scroll.scrollTop !== before) {
                    this.editor.drag(this.point(this.pointer.x, this.pointer.y));
                    this.render();
                }
            }
            requestAnimationFrame(step);
        };
        requestAnimationFrame(step);
    }

    // ───────────────────────────── Dibujo ─────────────────────────────

    render() {
        const vm = this.vm;
        const width = this.canvas.clientWidth;
        if (!vm.mosaicVisible || !vm.playerVisible || width === 0) return;

        const large = vm.largeText;
        // En un móvil las portadas son más pequeñas: caben 3 por fila (2 con la letra grande).
        const target = width < 600 ? (large ? 150 : 112) : (large ? 210 : 170);
        const columns = Math.max(2, Math.floor((width + GAP) / (target + GAP)));
        const cell = Math.max(1, (width - GAP * (columns - 1)) / columns);
        const metrics = new MosaicMetrics(columns, cell, GAP, large ? CONTROLS_MIN_WIDTH_LARGE : CONTROLS_MIN_WIDTH);
        this.editor.metrics = metrics;

        const tiles = vm.tiles;
        const spans = tiles.map(tile => this.editor.effectiveSpan(tile));
        const { spots, rows } = packMosaic(spans, columns);
        const slots = new Map(tiles.map((tile, i) => [tile, metrics.slot(spots[i][0], spots[i][1], spans[i])]));
        this.editor.slots = slots;
        const height = rows === 0 ? 0 : rows * cell + (rows - 1) * GAP;
        this.canvas.style.height = `${height + 96}px`;   // sitio al final para no tapar la última
        const activeIndex = tiles.findIndex(tile => tile.key === vm.currentKey);

        const top = this.scroll.scrollTop;
        const viewport = this.scroll.clientHeight;
        const keep = new Set([vm.currentKey]);
        const seen = new Set();

        tiles.forEach((tile, i) => {
            const slot = slots.get(tile);
            const active = i === activeIndex;
            let item = this.items.get(tile.key);
            const fresh = !item;
            if (fresh) {
                item = this.createItem(tile);
                this.items.set(tile.key, item);
            }
            seen.add(tile.key);

            const dragging = this.editor.dragged === tile;
            const x = dragging ? this.editor.dragPoint.x - this.editor.grab.x : slot.left;
            const y = dragging ? this.editor.dragPoint.y - this.editor.grab.y : slot.top;
            const style = item.el.style;
            style.transform = `translate(${x}px, ${y}px)`;
            style.width = style.height = `${slot.width}px`;
            item.el.classList.toggle('dragging', dragging);
            item.el.classList.toggle('active', active);

            if (fresh) {
                // El mosaico se forma desde la canción que suena hacia fuera.
                if (this.introsLeft > 0) {
                    this.introsLeft--;
                    item.body.classList.add('intro-' + INTROS[Math.floor(Math.random() * INTROS.length)]);
                    item.body.style.animationDelay = `${Math.min(700, activeIndex < 0 ? i * 25 : Math.abs(i - activeIndex) * 30)}ms`;
                } else {
                    item.body.classList.add('intro-quick');
                }
                item.body.addEventListener('animationend', () => {
                    item.body.className = 'tile-body';
                    item.body.style.animationDelay = '';
                }, { once: true });
                this.canvas.appendChild(item.el);
            }

            this.updateContent(item, tile, active);

            // Solo se cargan las carátulas visibles (y una pantalla por encima y dos por debajo).
            const near = slot.bottom >= top - viewport && slot.top <= top + 2 * viewport;
            if (near) {
                keep.add(tile.key);
                this.showCover(item, tile.key);
            } else if (item.coverKey) {
                item.img.removeAttribute('src');
                item.img.hidden = true;
                item.coverKey = null;
            }
        });

        for (const [key, item] of this.items) {
            if (!seen.has(key)) {
                item.controls?.dispose();
                item.el.remove();
                this.items.delete(key);
            }
        }
        Covers.trim(keep);

        // Al cambiar de canción, la que suena se pone a la vista.
        if (vm.currentKey !== this.lastActive) {
            this.lastActive = vm.currentKey;
            const slot = tiles[activeIndex] && slots.get(tiles[activeIndex]);
            if (slot && (slot.top < top || slot.bottom > top + viewport)) {
                this.scroll.scrollTo({ top: Math.max(0, slot.top - GAP), behavior: 'smooth' });
            }
        }
        // Las portadas nuevas de una tanda posterior también se animan.
        if (tiles.length === 0) this.introsLeft = ANIMATED_INTROS;
    }

    createItem(tile) {
        const el = document.createElement('div');
        el.className = 'tile';
        el.dataset.key = tile.key;
        const body = document.createElement('div');
        body.className = 'tile-body';
        body.innerHTML = `<span class="placeholder">${Icons.note}</span><img class="cover" alt="" hidden draggable="false">` +
            `<span class="badge-origin">${tile.isSpotify ? Icons.spotifyBadge : Icons.ownBadge}</span>` +
            (tile.isSpotify ? `<span class="badge-youtube" title="Suena con su vídeo de YouTube">${Icons.play}</span>` : '') +
            `<div class="tile-caption"></div><div class="tile-info"><div class="tile-title"></div><div class="tile-artist"></div></div>` +
            `<span class="grip" title="Arrastra para cambiar el tamaño">${Icons.grip}</span>`;
        el.appendChild(body);
        return {
            el, body,
            img: body.querySelector('.cover'),
            caption: body.querySelector('.tile-caption'),
            info: body.querySelector('.tile-info'),
            title: body.querySelector('.tile-title'),
            artist: body.querySelector('.tile-artist'),
            controls: null, coverKey: null, shownTitle: null, wasActive: false,
        };
    }

    updateContent(item, tile, active) {
        const vm = this.vm;
        if (active !== item.wasActive) {
            item.wasActive = active;
            if (active) {
                item.controls = new Controls(vm);
                item.info.appendChild(item.controls.el);
            } else {
                item.controls?.dispose();
                item.controls = null;
            }
        }
        if (active) {
            setText(item.title, vm.title);
            setText(item.artist, vm.artist);
        } else {
            const title = vm.meta.get(tile.key)?.title ?? SongTags.fallbackTitle(tile.key);
            if (title !== item.shownTitle) {
                item.shownTitle = title;
                item.caption.textContent = title;
                item.el.title = title;
            }
        }
    }

    showCover(item, key) {
        if (item.coverKey === key) return;
        item.coverKey = key;
        const show = url => {
            if (item.coverKey !== key || !url) return;
            item.img.src = url;
            item.img.hidden = false;
        };
        const known = Covers.peek(key);
        if (known !== undefined) show(known);
        else Covers.get(key).then(show);
    }
}

function setText(element, text) {
    if (element.textContent !== text) element.textContent = text;
}
