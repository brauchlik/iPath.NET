let viewer = null;
let dotNetRef = null;
let throttleTimer = null;
let isApplyingRemote = false;

export function initOsd(divId, tileSourceUrl, dotNetReference) {
    dotNetRef = dotNetReference;

    const elem = document.getElementById(divId);
    if (!elem) return;

    elem.innerHTML = '';

    viewer = OpenSeadragon({
        id: elem.id,
        prefixUrl: "https://cdn.jsdelivr.net/npm/openseadragon@5.0.1/build/openseadragon/images/",
        visibilityRatio: 1.0,
        constrainDuringPan: true,
        defaultZoomLevel: 0,
        minZoomLevel: 0.5,
        maxZoomLevel: 100,
        showNavigationControl: false,
        crossOriginPolicy: "Anonymous"
    });

    viewer.addHandler('open', () => { });

    viewer.addHandler('open-failed', (event) => {
        console.error('OSD open failed:', event.message);
    });

    viewer.addHandler('viewport-change', onViewportChange);

    if (tileSourceUrl) openTileSource(tileSourceUrl);
}

export function openTileSource(url) {
    if (!viewer) return;
    if (url && url.toLowerCase().endsWith('.dzi')) {
        viewer.open(url);
    } else {
        viewer.open({ type: 'image', url: url, buildPyramid: false });
    }
}

export function setViewport(x, y, zoom) {
    if (!viewer) return;
    isApplyingRemote = true;
    viewer.viewport.panTo({ x: x, y: y }, true);
    viewer.viewport.zoomTo(zoom, null, true);
    setTimeout(() => { isApplyingRemote = false; }, 50);
}

export function getViewport() {
    if (!viewer) return null;
    const c = viewer.viewport.getCenter();
    const z = viewer.viewport.getZoom();
    return { x: c.x, y: c.y, zoom: z };
}

export function dispose() {
    if (throttleTimer) { clearTimeout(throttleTimer); throttleTimer = null; }
    if (viewer) { viewer.destroy(); viewer = null; }
    dotNetRef = null;
    isApplyingRemote = false;
}

function onViewportChange() {
    if (isApplyingRemote || !dotNetRef) return;
    if (throttleTimer) return;
    throttleTimer = setTimeout(() => {
        const c = viewer.viewport.getCenter();
        const z = viewer.viewport.getZoom();
        dotNetRef.invokeMethodAsync('OnViewportChanged', c.x, c.y, z);
        throttleTimer = null;
    }, 150);
}
