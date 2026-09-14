// State is keyed by mount element id. An ES module is a singleton, so holding the
// viewer in a module-level variable meant a second <OsdViewer> on the same page
// clobbered the first, and either one's dispose() destroyed both.
const instances = new Map();

export function initOsd(divId, tileSourceUrl, dotNetReference, initialViewport) {
    const elem = document.getElementById(divId);
    if (!elem) return;

    // Defensive: re-initialising the same mount should not leak the previous viewer.
    dispose(divId);

    const viewer = OpenSeadragon({
        id: elem.id,
        visibilityRatio: 1,
        minZoomImageRatio: 1,
        prefixUrl: "_content/iPath.OpenSeadragon/images/",
        // Tiles are same-origin and cookie-authenticated; "Anonymous" keeps
        // credentials mode "same-origin", so the auth cookie is still sent.
        crossOriginPolicy: "Anonymous",
    });

    const instance = { viewer, dotNetRef: dotNetReference };
    instances.set(divId, instance);

    viewer.addHandler('open', () => {
        instance.dotNetRef?.invokeMethodAsync('OnOsdOpened');
        if (initialViewport && isFinite(initialViewport.x)) {
            viewer.viewport.panTo({ x: initialViewport.x, y: initialViewport.y }, true);
            viewer.viewport.zoomTo(initialViewport.zoom, null, true);
        }
    });

    viewer.addHandler('open-failed', (event) => {
        instance.dotNetRef?.invokeMethodAsync('OnOsdError', event.message);
    });

    if (tileSourceUrl) openTileSource(divId, tileSourceUrl);
}

export function openTileSource(divId, url) {
    const instance = instances.get(divId);
    if (!instance) return;
    const { viewer } = instance;

    instance.dotNetRef?.invokeMethodAsync('OnOsdLoading');

    if (url.toLowerCase().endsWith('.dzi')) {
        viewer.open(url);
        return;
    }

    import('https://cdn.jsdelivr.net/gh/episphere/GeoTIFFTileSource-JPEG2k/GeoTIFFTileSource.js')
        .then(() => OpenSeadragon.GeoTIFFTileSource.getAllTileSources(url, { logLatency: false, cache: true, slideOnly: true }))
        .then(tileSources => {
            // The component may have been disposed while the plugin was loading.
            if (instances.get(divId) === instance) viewer.open(tileSources);
        })
        .catch(err => instance.dotNetRef?.invokeMethodAsync('OnOsdError', err.message));
}

export function dispose(divId) {
    const instance = instances.get(divId);
    if (!instance) return;

    instances.delete(divId);
    instance.dotNetRef = null;
    try { instance.viewer.destroy(); } catch { /* already torn down */ }
}
