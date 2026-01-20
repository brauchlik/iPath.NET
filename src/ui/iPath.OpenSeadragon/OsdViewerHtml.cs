namespace iPath.OpenSeadragon;


public class OsdViewerHtml
{
    public string CreateIframeHtml(string imgPath, string viewerStyle = "width: 1000; height: 700;")
    {
        var _html = """
<!DOCTYPE html>
<html>
    <head>
        <!-- OpenSeadragon (image viewer)-->
        <script src="https://cdn.jsdelivr.net/npm/openseadragon@5.0.1/build/openseadragon/openseadragon.min.js"></script>
        <script src="https://cdn.jsdelivr.net/gh/episphere/GeoTIFFTileSource-JPEG2k/GeoTIFFTileSource.js" type="module" crossorigin="anonymous"></script>
    </head>
    <body style="backgroud-color: black; color: white;">

    <div id="osd" style=" 
""" + viewerStyle + """
    "></div>
  
    <script type="text/javascript">
        document.addEventListener("DOMContentLoaded", (event) => {
            // alert("@url");
            loadImage('osd', '
""" + imgPath + """
');
        });

        async function loadImage(elemId, url)
        {
            var elem = document.getElementById(elemId);

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
        }
    </script>
    <body>
</html>
""";

        return _html;
    }
}
