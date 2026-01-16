svs = {}


svs.hasError = false;


/**
 * GeoTIFF plugin is using multiple partial loadfing reuests in parallel
 * make sure that the servers security (e.g. mod_evasive in apache) do not
 * block such requests with temproal 403 errors
 */


svs.loadImage = async (elemId, url) => {
    var elem = document.getElementById(elemId);
    if (!elem) {
        alert("Element not found #" + elemId);
        return;
    }

    // alert("Loading " + url + " into #" + elemId);

    try
    {
        elem.innerHTML = ""

        var tileSources = await OpenSeadragon.GeoTIFFTileSource.getAllTileSources(url, { logLatency: false, cache: true, slideOnly: true });
        var dim = tileSources[0].dimensions;

        var osd = OpenSeadragon({
            id: elem.id,
            visibilityRatio: 1,
            minZoomImageRatio: 1,
            ajaxWithCredentials: true,
            prefixUrl: "_content/iPath.OpenSeadragon/images/",
            imageLoaderLimit: 5,
            timeout: 1000 * 1000,
            crossOriginPolicy: "Anonymous",
        });

        osd.open(tileSources);

        return dim.toString();

    }
    catch (t)
    {
        return t.toString();
    }
}
