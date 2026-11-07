// Canvas rendering and pointer handling for the world map.
// Rectangles are supplied in world units; the canvas scales them to its pixel size.

const states = new WeakMap();

// Tileset image: a grid of 15px cells, a 1px gridline on each cell's top/left edge. Column = terrain type ID,
// row = climate ID (row 0 and column 0 hold the sheet's labels).
const TILE_PITCH = 15;
const TILE_SIZE = 14;
const tileset = new Image();
tileset.src = new URL('../img/tiles1.png', import.meta.url).href;

export function attach(canvas, dotNetRef) {
    const state = { canvas, dotNetRef, rects: null, width: 1, height: 1, selected: null };
    states.set(canvas, state);

    const frac = (e) => {
        const r = canvas.getBoundingClientRect();
        return [(e.clientX - r.left) / r.width, (e.clientY - r.top) / r.height];
    };
    canvas.addEventListener('click', (e) => {
        const [fx, fy] = frac(e);
        dotNetRef.invokeMethodAsync('OnMapPointer', fx, fy, false);
    });
    canvas.addEventListener('dblclick', (e) => {
        const [fx, fy] = frac(e);
        dotNetRef.invokeMethodAsync('OnMapPointer', fx, fy, true);
    });

    state.onTilesLoaded = () => paint(state);
    tileset.addEventListener('load', state.onTilesLoaded);

    state.observer = new ResizeObserver(() => paint(state));
    state.observer.observe(canvas);
}

export function draw(canvas, width, height, rects, selected) {
    const state = states.get(canvas);
    if (!state) return;
    state.width = width;
    state.height = height;
    state.rects = rects;
    state.selected = selected;
    paint(state);
}

export function setSelection(canvas, selected) {
    const state = states.get(canvas);
    if (!state) return;
    state.selected = selected;
    paint(state);
}

export function detach(canvas) {
    const state = states.get(canvas);
    if (state) {
        state.observer.disconnect();
        tileset.removeEventListener('load', state.onTilesLoaded);
        states.delete(canvas);
    }
}

function paint(state) {
    const { canvas, rects, width, height, selected } = state;
    const dpr = window.devicePixelRatio || 1;
    const pw = Math.max(1, Math.round(canvas.clientWidth * dpr));
    const ph = Math.max(1, Math.round(canvas.clientHeight * dpr));
    if (canvas.width !== pw) canvas.width = pw;
    if (canvas.height !== ph) canvas.height = ph;

    const ctx = canvas.getContext('2d');
    ctx.clearRect(0, 0, pw, ph);
    if (!rects) return;

    const sx = pw / width, sy = ph / height;
    ctx.imageSmoothingEnabled = false;
    const tilesReady = tileset.complete && tileset.naturalWidth > 0;
    for (let i = 0; i < rects.length; i += 6) {
        const x0 = Math.floor(rects[i] * sx), y0 = Math.floor(rects[i + 1] * sy);
        const x1 = Math.ceil((rects[i] + rects[i + 2]) * sx), y1 = Math.ceil((rects[i + 1] + rects[i + 3]) * sy);
        const tile = rects[i + 5];
        if (tile >= 0 && tilesReady) {
            const col = tile & 255, row = tile >> 8;
            ctx.drawImage(tileset, col * TILE_PITCH + 1, row * TILE_PITCH + 1, TILE_SIZE, TILE_SIZE, x0, y0, x1 - x0, y1 - y0);
        } else {
            ctx.fillStyle = '#' + rects[i + 4].toString(16).padStart(6, '0');
            ctx.fillRect(x0, y0, x1 - x0, y1 - y0);
        }
    }

    if (selected) {
        ctx.lineWidth = Math.max(2, 2 * dpr);
        ctx.strokeStyle = '#000';
        ctx.strokeRect(selected[0] * sx, selected[1] * sy, selected[2] * sx, selected[3] * sy);
        ctx.lineWidth = Math.max(1, dpr);
        ctx.strokeStyle = '#fff';
        ctx.strokeRect(selected[0] * sx, selected[1] * sy, selected[2] * sx, selected[3] * sy);
    }
}
