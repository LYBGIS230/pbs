using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Geometry;
using System.Windows.Media;

namespace PBS.APP
{
    public class BaiduTileLayer : TiledMapServiceLayer
    {
        private const double cornerCoordinate = 20037508.3427892;
        public string baseUrl = string.Empty;
        public override string GetTileUrl(int level, int row, int col)
        {
            return baseUrl + "/tile/" + string.Format("{0}/{1}/{2}", level, row, col);
        }
        public override void Initialize()
        {
            this.FullExtent = new Envelope(-20037508.3427892, -20037508.3427892, 20037508.3427892, 20037508.3427892) { SpatialReference = new SpatialReference(3857) };

            base.SpatialReference = new SpatialReference() { WKID = 3857};
            base.TileInfo = new TileInfo { Height = 256, Width = 256, Origin = new MapPoint(-cornerCoordinate, cornerCoordinate) { SpatialReference = new SpatialReference(3857) }, Lods = new Lod[20] };
            double resolution = 262144.52428904863;
            for (int i = 0; i < base.TileInfo.Lods.Length; i++)
            {
                base.TileInfo.Lods[i] = new Lod
                {
                    Resolution = resolution
                };
                resolution /= 2;
            }
            base.Initialize();
        }

        public string TileLayerURL
        {
            get;
            set;
        }
    }
}
