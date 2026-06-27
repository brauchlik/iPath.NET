### Task 8: JS interop module — `ipath-caseroom.js`

**Files:**
- Create: `src/ui/iPath.RazorLib/wwwroot/js/ipath-caseroom.js`

**Interfaces:**
- Consumes: OpenSeadragon CDN (already used by existing OSD integration)
- Produces: `initOsd(divId, tileSourceUrl, dotNetRef)`, `openTileSource(url)`, `setViewport(x, y, zoom)`, `getViewport()`, `dispose()`

- [ ] **Step 1: Create the JS interop module**

Create `src/ui/iPath.RazorLib/wwwroot/js/ipath-caseroom.js`:

```javascript
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

    viewer.addHandler('open', () => { /* loader could be hidden here */ });

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
    // Release guard after OSD's own viewport-change settles
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
```

- [ ] **Step 2: Build to verify static file is included**

Run: `dotnet build src/ui/iPath.RazorLib/iPath.RazorLib.csproj`
Expected: Build succeeded (JS file picked up automatically as static web asset).

- [ ] **Step 3: Commit**

```bash
git add src/ui/iPath.RazorLib/wwwroot/js/ipath-caseroom.js
git commit -m "feat(caseroom): add OSD JS interop module with throttled viewport sync"
```

