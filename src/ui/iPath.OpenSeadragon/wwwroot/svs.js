/*******************************************************
 * 
 * From https://github.com/episphere/svs
 * 
 *******************************************************/

svs = {}

svs.loadImage = async (urlInGCP) => {
    urlInGCP = urlInGCP.replace(/\s/g, "_")

    document.getElementById("openseadragon1").innerHTML = ""
    const tileSources = await OpenSeadragon.GeoTIFFTileSource.getAllTileSources(urlInGCP, { logLatency: false, cache: true, slideOnly: true })
    console.log(tileSources)
    const viewer1 = OpenSeadragon({
        id: "openseadragon1",
        visibilityRatio: 1,
        minZoomImageRatio: 1,
        prefixUrl: "https://episphere.github.io/svs/openseadragon/images/",
        imageLoaderLimit: 5,
        timeout: 1000 * 1000,
        crossOriginPolicy: "Anonymous",
        tileSources: await OpenSeadragon.GeoTIFFTileSource.getAllTileSources(urlInGCP, { logLatency: false, cache: true, slideOnly: true })
    });
}

if (typeof (define) != 'undefined') {
    define(svs)
}
