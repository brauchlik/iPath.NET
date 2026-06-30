let viewer = null;
let dotNetRef = null;
let throttleTimer = null;
let isApplyingRemote = false;
let pointerToolActive = false;
let pointerElement = null;
let isDraggingArrow = false;
let lastPointerSyncTime = 0;

export function initOsd(divId, tileSourceUrl, dotNetReference, initialViewport) {
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

    viewer.addHandler('open', () => {
        if (initialViewport && isFinite(initialViewport.x)) {
            console.debug('[CaseRoom] applying initial viewport: x=%.4f, y=%.4f, zoom=%.4f',
                initialViewport.x, initialViewport.y, initialViewport.zoom);
            isApplyingRemote = true;
            viewer.viewport.panTo({ x: initialViewport.x, y: initialViewport.y }, true);
            viewer.viewport.zoomTo(initialViewport.zoom, null, true);
        }
        requestAnimationFrame(() => { isApplyingRemote = false; });
    });

    viewer.addHandler('open-failed', (event) => {
        console.error('OSD open failed:', event.message);
        isApplyingRemote = false;
    });

    viewer.addHandler('viewport-change', onViewportChange);

    if (tileSourceUrl) openTileSource(tileSourceUrl);
}

export function openTileSource(url) {
    if (!viewer) return;
    isApplyingRemote = true;
    if (url) {
        const cleanUrl = url.split('?')[0];
        if (cleanUrl.toLowerCase().endsWith('.dzi')) {
            viewer.open(url);
        } else {
            viewer.open({ type: 'image', url: url, buildPyramid: false });
        }
    }
    requestAnimationFrame(() => { isApplyingRemote = false; });
}

export function setViewport(x, y, zoom) {
    if (!viewer) return;
    if (throttleTimer) { clearTimeout(throttleTimer); throttleTimer = null; }
    console.debug('[CaseRoom] applying remote viewport: x=%.4f, y=%.4f, zoom=%.4f', x, y, zoom);
    isApplyingRemote = true;
    viewer.addOnceHandler('animation-finish', () => { isApplyingRemote = false; });
    setTimeout(() => { isApplyingRemote = false; }, 500);
    viewer.viewport.panTo({ x: x, y: y }, false);
    viewer.viewport.zoomTo(zoom, null, false);
}

export function getViewport() {
    if (!viewer) return null;
    const c = viewer.viewport.getCenter();
    const z = viewer.viewport.getZoom();
    return { x: c.x, y: c.y, zoom: z };
}

export function dispose() {
    if (throttleTimer) { clearTimeout(throttleTimer); throttleTimer = null; }
    if (pointerTracker) { pointerTracker.destroy(); pointerTracker = null; }
    if (viewer) { viewer.destroy(); viewer = null; }
    dotNetRef = null;
    isApplyingRemote = false;
    pointerElement = null;
}

export function setMouseNavEnabled(enabled) {
    if (!viewer) return;
    viewer.setMouseNavEnabled(enabled);
}

function onViewportChange() {
    if (isApplyingRemote || !dotNetRef) return;
    if (throttleTimer) return;
    throttleTimer = setTimeout(() => {
        const c = viewer.viewport.getCenter();
        const z = viewer.viewport.getZoom();
        console.debug('[CaseRoom] local viewport: x=%.4f, y=%.4f, zoom=%.4f', c.x, c.y, z);
        dotNetRef.invokeMethodAsync('OnViewportChanged', c.x, c.y, z);
        throttleTimer = null;
    }, 150);
}

export function getViewportCenter() {
    if (!viewer) return { x: 0.5, y: 0.5, zoom: 1.0 };
    const c = viewer.viewport.getCenter();
    return { x: c.x, y: c.y, zoom: 1.0 };
}

export function setPointerToolActive(active) {
    pointerToolActive = active;
    if (!active) {
        hidePointer();
    }
}

export function showPointer(x, y, isDraggable) {
    if (!viewer) return;
    
    if (!pointerElement) {
        pointerElement = document.createElement('div');
        pointerElement.id = 'osd-pointer';
        pointerElement.style.width = '40px';
        pointerElement.style.height = '40px';
        pointerElement.style.pointerEvents = 'auto'; // Receive clicks/drags
        pointerElement.style.touchAction = 'none'; // Prevent browser scrolling during touch drag
        
        pointerElement.innerHTML = `
            <svg width="40" height="40" viewBox="0 0 40 40" fill="none" xmlns="http://www.w3.org/2000/svg" style="display: block; pointer-events: none;">
              <path d="M0 0L30 15L15 17.5L12.5 30L0 0Z" fill="#ffeb3b" stroke="black" stroke-width="2.5" stroke-linejoin="round"/>
            </svg>
        `;
        
        console.log("[Pointer] Overlay created and listeners being attached.");

        // Drag-and-drop pointer event handlers
        pointerElement.addEventListener('pointerdown', (e) => {
            console.log("[Pointer] pointerdown event fired. isDraggable =", pointerElement.dataset.draggable);
            if (pointerElement.dataset.draggable !== 'true') return;
            e.stopPropagation(); // Stop OSD from panning the slide
            e.preventDefault();
            isDraggingArrow = true;
            pointerElement.setPointerCapture(e.pointerId);
            pointerElement.style.cursor = 'grabbing';
        });

        pointerElement.addEventListener('pointermove', (e) => {
            if (!isDraggingArrow) return;
            e.stopPropagation();
            
            const rect = viewer.element.getBoundingClientRect();
            const clickX = e.clientX - rect.left;
            const clickY = e.clientY - rect.top;
            
            console.log("[Pointer] pointermove during drag, clickX =", clickX, "clickY =", clickY);

            const viewportPos = viewer.viewport.viewerElementToViewportCoordinates(new OpenSeadragon.Point(clickX, clickY));
            viewer.updateOverlay(pointerElement, viewportPos);
            
            sendPointerUpdate(clickX, clickY);
        });

        pointerElement.addEventListener('pointerup', (e) => {
            console.log("[Pointer] pointerup event fired.");
            if (!isDraggingArrow) return;
            isDraggingArrow = false;
            pointerElement.releasePointerCapture(e.pointerId);
            pointerElement.style.cursor = 'grab';
            
            const rect = viewer.element.getBoundingClientRect();
            const clickX = e.clientX - rect.left;
            const clickY = e.clientY - rect.top;
            sendPointerUpdate(clickX, clickY, true);
        });

        // Key listener for manual Escape key clear
        window.addEventListener('keydown', (e) => {
            if (e.key === 'Escape' && pointerToolActive) {
                hidePointer();
                dotNetRef.invokeMethodAsync('OnPointerHidden');
            }
        });
    }

    pointerElement.dataset.draggable = isDraggable ? 'true' : 'false';
    pointerElement.style.cursor = isDraggable ? 'grab' : 'default';

    const point = new OpenSeadragon.Point(x, y);
    const existing = viewer.getOverlayById('osd-pointer');
    if (existing) {
        viewer.updateOverlay('osd-pointer', point);
    } else {
        // Placement TOP_LEFT aligns the tip of the SVG (0,0) perfectly with coordinate (x,y)
        viewer.addOverlay({
            element: pointerElement,
            location: point,
            placement: OpenSeadragon.Placement.TOP_LEFT
        });
    }
}

function sendPointerUpdate(clickX, clickY, force = false) {
    const now = Date.now();
    if (force || (now - lastPointerSyncTime > 100)) {
        const viewportPos = viewer.viewport.viewerElementToViewportCoordinates(new OpenSeadragon.Point(clickX, clickY));
        dotNetRef.invokeMethodAsync('OnPointerMoved', viewportPos.x, viewportPos.y, true);
        lastPointerSyncTime = now;
    }
}

export function hidePointer() {
    if (viewer && pointerElement) {
        viewer.removeOverlay(pointerElement);
    }
    isDraggingArrow = false;
}
