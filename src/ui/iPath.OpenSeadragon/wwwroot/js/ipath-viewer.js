let viewer = null;
let dotNetRef = null;
let isApplyingRemote = false;

export function initOsd(divId, tileSourceUrl, dotNetReference, initialViewport) {
    dotNetRef = dotNetReference;

    const elem = document.getElementById(divId);
    if (!elem) return;

    viewer = OpenSeadragon({
        id: elem.id,
        visibilityRatio: 1,
        minZoomImageRatio: 1,
        prefixUrl: "_content/iPath.OpenSeadragon/images/",
        crossOriginPolicy: "CorsPolicy",
    });

    viewer.addHandler('open', () => {
        if (dotNetRef) dotNetRef.invokeMethodAsync('OnOsdOpened');
        if (initialViewport && isFinite(initialViewport.x)) {
            isApplyingRemote = true;
            viewer.viewport.panTo({ x: initialViewport.x, y: initialViewport.y }, true);
            viewer.viewport.zoomTo(initialViewport.zoom, null, true);
            requestAnimationFrame(() => { isApplyingRemote = false; });
        }
    });

    viewer.addHandler('open-failed', (event) => {
        if (dotNetRef) dotNetRef.invokeMethodAsync('OnOsdError', event.message);
    });

    if (tileSourceUrl) openTileSource(tileSourceUrl);
}

export function openTileSource(url) {
    if (!viewer) return;
    if (dotNetRef) dotNetRef.invokeMethodAsync('OnOsdLoading');

    if (url.toLowerCase().endsWith('.dzi')) {
        viewer.open(url);
    } else {
        import('https://cdn.jsdelivr.net/gh/episphere/GeoTIFFTileSource-JPEG2k/GeoTIFFTileSource.js')
            .then(() => {
                OpenSeadragon.GeoTIFFTileSource.getAllTileSources(url, { logLatency: false, cache: true, slideOnly: true })
                    .then(tileSources => { viewer.open(tileSources); })
                    .catch(err => { if (dotNetRef) dotNetRef.invokeMethodAsync('OnOsdError', err.message); });
            })
            .catch(err => {
                if (dotNetRef) dotNetRef.invokeMethodAsync('OnOsdError', 'Failed to load GeoTIFF plugin: ' + err.message);
            });
    }
}

export function setViewport(x, y, zoom) {
    if (!viewer) return;
    isApplyingRemote = true;
    viewer.viewport.panTo({ x: x, y: y }, true);
    viewer.viewport.zoomTo(zoom, null, true);
    requestAnimationFrame(() => { isApplyingRemote = false; });
}

export function getViewport() {
    if (!viewer) return null;
    const c = viewer.viewport.getCenter();
    const z = viewer.viewport.getZoom();
    return { x: c.x, y: c.y, zoom: z };
}

export function setMouseNavEnabled(enabled) {
    if (!viewer) return;
    viewer.setMouseNavEnabled(enabled);
}

export function dispose() {
    if (viewer) { viewer.destroy(); viewer = null; }
    dotNetRef = null;
    isApplyingRemote = false;
}
