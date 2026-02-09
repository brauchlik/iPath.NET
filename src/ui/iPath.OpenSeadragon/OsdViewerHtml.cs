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

        <style>
    #osd-loader-container {
        position: absolute;
        top: 0; left: 0; width: 100%; height: 100%;
        background: rgba(0, 0, 0, 0.6);
        display: flex;
        align-items: center;
        justify-content: center;
        z-index: 1000;
        color: white;
        font-family: sans-serif;
    }

    .osd-loader-hidden { display: none !important; }

    .osd-loader-content { text-align: center; }

    .spinner {
        border: 4px solid rgba(255, 255, 255, 0.3);
        border-top: 4px solid #ffffff;
        border-radius: 50%;
        width: 40px;
        height: 40px;
        animation: spin 1s linear infinite;
        margin: 0 auto 15px;
    }

    @keyframes spin {
        0% { transform: rotate(0deg); }
        100% { transform: rotate(360deg); }
    }
</style>

    </head>
    <body style="backgroud-color: black; color: white;">

    <div id="osd-loader-container" class="osd-loader-hidden">
        <div class="osd-loader-content">
            <div class="spinner"></div>
            <h3>Loading Slide...</h3>
            <p>Fetching metadata ...</p>
        </div>
    </div>

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
            var loader = document.getElementById("osd-loader-container");

            if (!elem) return;

            // Show the loader before initializing OSD
            if (loader) {
                loader.classList.remove("osd-loader-hidden");
            }

            /*
            try {
                var viewer = OpenSeadragon({
                    id: elem.id,
                    prefixUrl: "_content/iPath.OpenSeadragon/images/",
                    tileSources: {
                        type: 'image',
                        url: url,
                        buildPyramid: false,
                    }
                });

                // Hide loader when the first tile/metadata is ready
                viewer.addHandler('open', function() {
                    if (loader) loader.classList.add("osd-loader-hidden");
                });

                // Error handling
                viewer.addHandler('open-failed', function(event) {
                    if (loader) {
                        loader.querySelector(".osd-loader-content").innerHTML = 
                            "<h3 style='color:#ff4444;'>❌ Load Failed</h3><p>" + event.message + "</p>";
                    }
                });

            } catch (e) {
                console.error(e);
            }
            */


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
                crossOriginPolicy: "CorsPolicy",
            });

            // Hide loader when the first tile/metadata is ready
            osd.addHandler('open', function() {
                if (loader) loader.classList.add("osd-loader-hidden");
            });

            // Error handling
            osd.addHandler('open-failed', function(event) {
                if (loader) {
                    loader.querySelector(".osd-loader-content").innerHTML = 
                        "<h3 style='color:#ff4444;'>❌ Load Failed</h3><p>" + event.message + "</p>";
                }
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
