using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SQLite;
using PBS.Util;
using System.Drawing;
using System.Configuration;
using System.IO;
using System.Windows;
using Point = PBS.Util.Point;
using PBS.Service;
using System.Xml.Linq;
using ImageMagick;
using PBS.DataSource;
using System.Runtime.Caching;
using System.Collections.Specialized;

namespace PBS.Service
{
    public class RCRange
    {
        public int MaxRow;
        public int MinCol;
        public int MinRow;
        public int MaxCol;
    }
    public class LatLngPoint
    {
        public double Lat;
        public double Lng;
        public LatLngPoint(double x, double y)
        {
            Lng = x;
            Lat = y;
        }
        public LatLngPoint getCenter(LatLngPoint a)
        {
            return new LatLngPoint((Lng + a.Lng) / 2, (Lat + a.Lat) / 2);
        }
    }
    public class BaiDuMapManager
    {
        const double cornerCoordinate = 20037508.3427892;
        public static BaiDuMapManager inst;
        public int maxThreadCount = 0;
        public int bundleSize = 0;
        public int startVersion = 0;
        public long roundInterval = 0;
        public CacheVersionProvider cp;
        public ObjectCache instantCache;
        private CacheItemPolicy policy;
        public string RunMode;
        public void initCache()
        {
            NameValueCollection config = new NameValueCollection();
            config.Add("pollingInterval", "00:00:02");
            config.Add("physicalMemoryLimitPercentage", "10");
            config.Add("cacheMemoryLimitMegabytes", "100");
            instantCache = new MemoryCache("BaiDuInstant", config);
            policy  = new CacheItemPolicy();
        }
        public object getFromCache(string mapType, int row, int column, int level)
        {
            string key = mapType + "_" + level + "_" + row + "_" + column;
            return instantCache.Get(key);
        }
        public void setCache(string mapType, int row, int column, int level, object png)
        {
            DateTime localTime = DateTime.Now.AddSeconds(30);
            policy.AbsoluteExpiration = new DateTimeOffset(localTime, TimeZoneInfo.Local.GetUtcOffset(localTime));
            string key = mapType + "_" + level + "_" + row + "_" + column;
            instantCache.Set(key, png, policy);
        }
        public BaiDuMapManager()
        {
            BaiDuMapManager.inst = this;        
        }
        public System.Drawing.Point getNewPixelOffsetOfLeftTop(int level, int row1, int col1)
        {
            BdCoodOffsetProvider p = BdCoodOffsetProvider.getInstance();
            System.Drawing.Point org_leftBottom = getMetersFromRowCol(row1, col1, level);
            LatLng lpt = MetersToLatLon(org_leftBottom);
            LatLng testPoint = p.doAdjust(lpt, level);
            PointF meters = LatLng2Mercator(new LatLngPoint(testPoint.longitude, testPoint.latitude));
            System.Drawing.Point new_leftBottom = new System.Drawing.Point((int)(meters.X / resolution), (int)(meters.Y / resolution));
            return new_leftBottom;
        }
        public RCRange getArcgisRCRangeFromMercator(Envelope mercatorArea, int level)
        {
            double minRow = (cornerCoordinate - mercatorArea.YMax) / Math.Pow(2, 18 - level) / 256;
            double minCol = (mercatorArea.XMin + cornerCoordinate) / Math.Pow(2, 18 - level) / 256;
            double maxCol = (mercatorArea.XMax + cornerCoordinate) / Math.Pow(2, 18 - level) / 256;
            double maxRow = (cornerCoordinate - mercatorArea.YMin) / Math.Pow(2, 18 - level) / 256;
            int row_min = Convert.ToInt32(Math.Floor(minRow));
            int col_min = Convert.ToInt32(Math.Floor(minCol));
            int row_max = Convert.ToInt32(Math.Floor(maxRow));
            int col_max = Convert.ToInt32(Math.Floor(maxCol));
            return new RCRange() { MaxCol = col_max, MaxRow = row_max, MinCol = col_min, MinRow = row_min};
        }
        public delegate byte[] TileRequestMethod(int level, int row, int col, object otherParam);
        private int getTileNumber(int absoluteOffSet, int SIZE)
        {
            return absoluteOffSet > 0 ? absoluteOffSet / SIZE : (absoluteOffSet % SIZE == 0 ? absoluteOffSet / SIZE : absoluteOffSet / SIZE - 1);
        }
        public byte[] getBaiduTile(DataSourceBase baiduSource, int level, int row1, int col1,object otherParam, TileRequestMethod getPNG)
        {
            byte[] bytes;

            int PIC_SIZE = 256;
            System.Drawing.Point new_leftTop, new_leftBottom, new_rightTop, new_rightBottom;
            new_leftTop = BaiDuMapManager.inst.getNewPixelOffsetOfLeftTop(level, row1, col1);

            new_rightBottom = BaiDuMapManager.inst.getNewPixelOffsetOfLeftTop(level, row1 + 1, col1 + 1);
            new_rightBottom.X = new_rightBottom.X - 1;
            new_rightBottom.Y = new_rightBottom.Y + 1;

            new_leftBottom = BaiDuMapManager.inst.getNewPixelOffsetOfLeftTop(level, row1 + 1, col1);
            new_leftBottom.X = new_leftBottom.X;
            new_leftBottom.Y = new_leftBottom.Y + 1;

            new_rightTop = BaiDuMapManager.inst.getNewPixelOffsetOfLeftTop(level, row1, col1 + 1);
            new_rightTop.X = new_rightTop.X - 1;
            new_rightTop.Y = new_rightTop.Y;

            int maxY = new_leftTop.Y > new_rightTop.Y ? new_leftTop.Y + 5 : new_rightTop.Y + 5;
            int minY = new_rightBottom.Y < new_leftBottom.Y ? new_rightBottom.Y - 5 : new_leftBottom.Y - 5;
            int maxX = new_rightBottom.X > new_rightTop.X ? new_rightBottom.X + 5 : new_rightTop.X + 5;
            int minX = new_leftTop.X < new_leftBottom.X ? new_leftTop.X - 5 : new_leftBottom.X - 5;
            int newMaxRow = getTileNumber(maxY, PIC_SIZE);
            int newMinCol = getTileNumber(minX, PIC_SIZE);
            int newMinRow = getTileNumber(minY, PIC_SIZE);
            int newMaxCol = getTileNumber(maxX, PIC_SIZE);

            int colSpan = newMaxCol - newMinCol + 1;
            int rowSpan = newMaxRow - newMinRow + 1;

            System.Drawing.Point newLeftTop = new System.Drawing.Point(new_leftTop.X - newMinCol * PIC_SIZE, (newMaxRow + 1) * PIC_SIZE - new_leftTop.Y - 1);
            System.Drawing.Point newRightTop = new System.Drawing.Point(new_rightTop.X - newMinCol * PIC_SIZE, (newMaxRow + 1) * PIC_SIZE - new_rightTop.Y - 1);
            System.Drawing.Point newLeftBottom = new System.Drawing.Point(new_leftBottom.X - newMinCol * PIC_SIZE, (newMaxRow + 1) * PIC_SIZE - new_leftBottom.Y - 1);
            System.Drawing.Point newRightBottom = new System.Drawing.Point(new_rightBottom.X - newMinCol * PIC_SIZE, (newMaxRow + 1) * PIC_SIZE - new_rightBottom.Y - 1);

            if (colSpan < 1 || rowSpan < 1) return null;//indicate that the input is not valid
            Bitmap canvas = new Bitmap(colSpan * PIC_SIZE, rowSpan * PIC_SIZE);
            Graphics pics = Graphics.FromImage(canvas);
            pics.Clear(Color.Transparent);
            for (int x = 0; x < colSpan; x++)
            {
                for (int y = 0; y < rowSpan; y++)
                {
                    byte[] picStream = getPNG(level, newMinRow + y, newMinCol + x, otherParam);
                    if (picStream != null)
                    {
                        pics.DrawImage(new Bitmap(new MemoryStream(picStream)), x * PIC_SIZE, (rowSpan - y - 1) * PIC_SIZE, PIC_SIZE, PIC_SIZE);
                    }
                }
            }
            //canvas.Save("whole.png", System.Drawing.Imaging.ImageFormat.Png);
            FastImageConvertor c = new FastImageConvertor();
            Bitmap destBitmap = c.ConvertToSquare(canvas, newLeftTop, newRightTop, newLeftBottom, newRightBottom);
            MemoryStream output = new MemoryStream();
            destBitmap.Save(output, System.Drawing.Imaging.ImageFormat.Png);
            //destBitmap.Save("part.png", System.Drawing.Imaging.ImageFormat.Png);
            bytes = output.GetBuffer();
            return bytes;
        }
        public byte[] compressPNG(byte[] srcBytes)
        {
            byte[] result;
            MemoryStream ms = new MemoryStream(srcBytes);
            MemoryStream output = new MemoryStream();
            using (MagickImage image = new MagickImage(ms))
            {
                image.SetDefine(ImageMagick.MagickFormat.Png8, "compression", "lzw");
                image.Write(output);
            }
            result = output.GetBuffer();
            return result;
            /*ms.Position = 0;
            Image srcBitmap = Bitmap.FromStream(ms);
            MemoryStream output = new MemoryStream();
            System.Drawing.Imaging.EncoderParameters ep = new System.Drawing.Imaging.EncoderParameters(2);
            ep.Param[0] = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.ColorDepth, 8L);
            ep.Param[1] = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 80L);
            srcBitmap.Save(output, GetEncoderInfo("image/jpeg"), ep);
            return output.GetBuffer();*/
        }
        private System.Drawing.Imaging.ImageCodecInfo GetEncoderInfo(String mimeType)
        {
            int j;
            System.Drawing.Imaging.ImageCodecInfo[] encoders;
            encoders = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders();
            for (j = 0; j < encoders.Length; ++j)
            {
                if (encoders[j].MimeType == mimeType)
                    return encoders[j];
            }
            return null;
        }
        public RCRange getBaiduRCRangeFromGPS(Envelope area, int level)
        {
            int PIC_SIZE = 256;
            updateResolutionAndZoom(level);
            BdCoodOffsetProvider p = BdCoodOffsetProvider.getInstance();
            LatLng leftTopLpt = new LatLng(area.YMax, area.XMin);
            LatLng testPoint = p.doAdjust(leftTopLpt, level);
            PointF meters = LatLng2Mercator(new LatLngPoint(testPoint.longitude, testPoint.latitude));
            System.Drawing.Point new_leftTop = new System.Drawing.Point((int)(meters.X / resolution), (int)(meters.Y / resolution));

            LatLng rightBottomLpt = new LatLng(area.YMin, area.XMax);
            testPoint = p.doAdjust(rightBottomLpt, level);
            meters = LatLng2Mercator(new LatLngPoint(testPoint.longitude, testPoint.latitude));
            System.Drawing.Point new_rightBottom = new System.Drawing.Point((int)(meters.X / resolution), (int)(meters.Y / resolution));

            return new RCRange() { MaxCol = (new_rightBottom.X + 5) / PIC_SIZE, MaxRow = (new_leftTop.Y + 5) / PIC_SIZE, MinCol = (new_leftTop.X - 5) / PIC_SIZE, MinRow = (new_rightBottom.Y - 5) / PIC_SIZE };
        }
        public RCRange getRCRangeFromMercator(int level, int row1, int col1)
        {
            int PIC_SIZE = 256;
            BdCoodOffsetProvider p = BdCoodOffsetProvider.getInstance();
            System.Drawing.Point new_leftTop, new_leftBottom, new_rightTop, new_rightBottom;

            //calc pixel offset of left bottom point in the tile
            new_leftTop = getNewPixelOffsetOfLeftTop(level, row1, col1);

            new_rightBottom = getNewPixelOffsetOfLeftTop(level, row1 + 1, col1 + 1);
            new_rightBottom.X = new_rightBottom.X - 1;
            new_rightBottom.Y = new_rightBottom.Y + 1;

            new_leftBottom = getNewPixelOffsetOfLeftTop(level, row1 + 1, col1);
            new_leftBottom.X = new_leftBottom.X;
            new_leftBottom.Y = new_leftBottom.Y + 1;

            new_rightTop = getNewPixelOffsetOfLeftTop(level, row1, col1 + 1);
            new_rightTop.X = new_rightTop.X - 1;
            new_rightTop.Y = new_rightTop.Y;

            int maxY = new_leftTop.Y > new_rightTop.Y ? new_leftTop.Y + 5 : new_rightTop.Y + 5;
            int minY = new_rightBottom.Y < new_leftBottom.Y ? new_rightBottom.Y - 5 : new_leftBottom.Y - 5;
            int maxX = new_rightBottom.X > new_rightTop.X ? new_rightBottom.X + 5 : new_rightTop.X + 5;
            int minX = new_leftTop.X < new_leftBottom.X ? new_leftTop.X - 5 : new_leftBottom.X - 5;

            return new RCRange() { MaxCol = maxX / PIC_SIZE, MaxRow = maxY / PIC_SIZE, MinCol = minX / PIC_SIZE, MinRow = minY / PIC_SIZE };
        }
        public List<CustomOnlineMap> ReadOnlineMapsConfigFile(string fileName)
        {
            List<CustomOnlineMap> _customOnlineMaps = null;
            string CONFIG_FILE_NAME = "";
            try
            {
                CONFIG_FILE_NAME = AppDomain.CurrentDomain.BaseDirectory + fileName;
                _customOnlineMaps = new List<CustomOnlineMap>();
                if (!File.Exists(CONFIG_FILE_NAME))
                {
                    throw new FileNotFoundException(CONFIG_FILE_NAME + " does not exist!");
                }
                XDocument xDoc = XDocument.Load(CONFIG_FILE_NAME);
                var maps = from map in xDoc.Descendants("onlinemapsources").Elements()
                           select new
                           {
                               Name = map.Element("name"),
                               Url = map.Element("url"),
                               Servers = map.Element("servers")
                           };
                foreach (var map in maps)
                {
                    _customOnlineMaps.Add(new CustomOnlineMap()
                    {
                        Name = map.Name.Value,
                        Servers = map.Servers.Value.Split(new char[] { ',' }),
                        Url = map.Url.Value,
                        MainType = map.Name.Attribute("mainType").Value
                    });
                }
            }
            catch (Exception e)
            {
                throw new Exception("Could not parse" + CONFIG_FILE_NAME + " file!\r\n" + e.Message);
            }
            return _customOnlineMaps;
        }

        public TilingScheme getBaiDuSchema()
        {
            TilingScheme tilingScheme = new TilingScheme();
            StringBuilder sb;
            tilingScheme.Path = "N/A";
            tilingScheme.CacheTileFormat = ImageFormat.PNG;
            tilingScheme.CompressionQuality = 75;
            tilingScheme.DPI = 96;
            //LODs
            tilingScheme.LODs = new LODInfo[20];

            double resolution = 262144;
            double scale = 990780472.44094491;
            for (int i = 0; i < tilingScheme.LODs.Length; i++)
            {
                tilingScheme.LODs[i] = new LODInfo()
                {
                    Resolution = resolution,
                    LevelID = i,
                    Scale = scale
                };
                resolution /= 2;
                scale /= 2;
            }
            sb = new StringBuilder("\r\n");
            foreach (LODInfo lod in tilingScheme.LODs)
            {
                sb.Append(@"      {""level"":" + lod.LevelID + "," + @"""resolution"":" + lod.Resolution + "," + @"""scale"":" + lod.Scale + @"}," + "\r\n");
            }
            tilingScheme.LODsJson = sb.ToString().Remove(sb.ToString().Length - 3);//remove last "," and "\r\n"
            //two extent
            tilingScheme.InitialExtent = new Envelope(-cornerCoordinate, -cornerCoordinate, cornerCoordinate, cornerCoordinate);
            tilingScheme.FullExtent = tilingScheme.InitialExtent;
            tilingScheme.PacketSize = 128;
            tilingScheme.StorageFormat = StorageFormat.esriMapCacheStorageModeExploded;
            tilingScheme.TileCols = tilingScheme.TileRows = 256;

            tilingScheme.TileOrigin = new Point(-cornerCoordinate, cornerCoordinate);
            tilingScheme.WKID = 3857;//102100;
            tilingScheme.WKT = @"PROJCS[""WGS_1984_Web_Mercator_Auxiliary_Sphere"",GEOGCS[""GCS_WGS_1984"",DATUM[""D_WGS_1984"",SPHEROID[""WGS_1984"",6378137.0,298.257223563]],PRIMEM[""Greenwich"",0.0],UNIT[""Degree"",0.0174532925199433]],PROJECTION[""Mercator_Auxiliary_Sphere""],PARAMETER[""False_Easting"",0.0],PARAMETER[""False_Northing"",0.0],PARAMETER[""Central_Meridian"",0.0],PARAMETER[""Standard_Parallel_1"",0.0],PARAMETER[""Auxiliary_Sphere_Type"",0.0],UNIT[""Meter"",1.0],AUTHORITY[""ESRI"",""3857""]]";
            return tilingScheme;
        }
        public TilingScheme getBaiDuSchema(double left, double right, double top, double bottom)
        {
            TilingScheme tilingScheme = getBaiDuSchema();
            Point leftBottom = new Point(left, bottom);
            Point rightTop = new Point(right, top);
            tilingScheme.InitialExtent = new Envelope(leftBottom.X, leftBottom.Y, rightTop.X, rightTop.Y);
            return tilingScheme;
        }
        public long ConvertDateTimeLong(System.DateTime time)
        {
            System.DateTime startTime = TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1, 0, 0, 0, 0));
            long t = (time.Ticks - startTime.Ticks) / 10000;
            return t;
        }
        #region old transform API Area, potential usable
        public double MAPUNIT = 1;
        public int tileHeight = 256;
        public double scaleFactor = 1;
        private Coordinate origin = new Coordinate(-20037508.3427892, 20037508.3427892);
        public int packetSize = 128;
        private int zoomLevel = 0;
        public double resolution;
        public double dpi = 96;
        private static double[] array1 = { 75, 60, 45, 30, 15, 0 };
        private static double[] array3 = { 12890594.86, 8362377.87, 5591021, 3481989.83, 1678043.12, 0 };
        private static double[][] array2 = {new double[]{-0.0015702102444
, 111320.7020616939, 1704480524535203, -10338987376042340, 26112667856603880, -35149669176653700, 26595700718403920, -10725012454188240, 1800819912950474, 82.5}
                                               ,new double[]{0.0008277824516172526, 111320.7020463578, 647795574.6671607, -4082003173.641316, 10774905663.51142, -15171875531.51559, 12053065338.62167, -5124939663.577472, 913311935.9512032, 67.5}
                                               ,new double[]{0.00337398766765, 111320.7020202162, 4481351.045890365, -23393751.19931662, 79682215.47186455, -115964993.2797253, 97236711.15602145, 
-43661946.33752821, 8477230.501135234, 52.5}
                                               ,new double[]{0.00220636496208, 111320.7020209128, 51751.86112841131, 3796837.749470245, 992013.7397791013, -1221952.21711287, 1340652.697009075, -620943.6990984312, 144416.9293806241, 37.5}
                                               ,new double[]{-0.0003441963504368392, 111320.7020576856, 278.2353980772752, 2485758.690035394, 6070.750963243378, 54821.18345352118, 9540.606633304236, -2710.55326746645, 1405.483844121726, 22.5}
                                               ,new double[]{-0.0003218135878613132, 111320.7020701615, 0.00369383431289, 823725.6402795718, 0.46104986909093, 2351.343141331292, 1.58060784298199,
8.77738589078284, 0.37238884252424, 7.45}};
        private static double[][] array4 = {new double[]{1.410526172116255e-8, 0.00000898305509648872, -1.9939833816331, 200.9824383106796
, -187.2403703815547, 91.6087516669843, -23.38765649603339, 2.57121317296198, -0.03801003308653, 17337981.2}
                                               ,new double[]{-7.435856389565537e-9, 0.000008983055097726239, -0.78625201886289, 96.32687599759846, -1.85204757529826, -59.36935905485877, 47.40033549296737, -16.50741931063887, 2.28786674699375, 10260144.86}
                                               ,new double[]{-3.030883460898826e-8, 0.00000898305509983578, 0.30071316287616, 59.74293618442277, 7.357984074871, -25.38371002664745, 13.45380521110908, -3.29883767235584, 0.32710905363475, 6856817.37}
                                               ,new double[]{-1.981981304930552e-8, 0.000008983055099779535, 0.03278182852591, 40.31678527705744, 0.65659298677277, -4.44255534477492, 0.85341911805263, 0.12923347998204, -0.04625736007561, 4482777.06}
                                               ,new double[]{3.09191371068437e-9, 0.000008983055096812155, 0.00006995724062, 23.10934304144901, -0.00023663490511, -0.6321817810242, -0.00663494467273, 0.03430082397953, -0.00466043876332, 2555164.4}
                                               ,new double[]{2.890871144776878e-9, 0.000008983055095805407, -3.068298e-8, 7.47137025468032, -0.00000353937994, -0.02145144861037, -0.00001234426596
, 0.00010322952773, -0.00000323890364, 826088.5}};
        public class Coordinate
        {
            public double X;
            public double Y;
            public Coordinate(double longitude, double latitude)
            {
                X = longitude;
                Y = latitude;
            }
            public Coordinate(LatLngPoint point)
            {
                X = point.Lng;
                Y = point.Lat;
            }
        }
        
        class Area
        {
            public double XMax;
            public double XMin;
            public double YMax;
            public double YMin;
            public LatLngPoint getCenter()
            {
                return new LatLngPoint((XMax + XMin) / 2, (YMax + YMin) / 2);
            }
            public LatLngPoint getLeftTop()
            {
                return new LatLngPoint(XMin, YMax);
            }
        }

        public LatLng MetersToLatLon(System.Drawing.Point m)
        {
	        //"Converts XY point from Spherical Mercator EPSG:900913 to lat/lon in WGS84 Datum"
            double lon = (m.X / cornerCoordinate) * 180.0;
            double lat = (m.Y / cornerCoordinate) * 180.0;
            lat = 180 / Math.PI * (2 * Math.Atan(Math.Exp(lat * Math.PI / 180.0)) - Math.PI / 2.0);
            return new LatLng(lat, lon);
        }
        private LatLngPoint Mercator2LatLng(PointF p)
        {
            double[] arr = null;
            PointF np = new PointF(Math.Abs(p.X), Math.Abs(p.Y));
            for (var i = 0; i < array3.Length; i++)
            {
                if (np.Y >= array3[i])
                {
                    arr = array4[i];
                    break;
                }
            }
            double[] res = Convertor(np.X, np.Y, arr);
            return new LatLngPoint(res[0], res[1]);
        }
        public System.Drawing.Point getMetersFromRowCol(int absoluteRow, int absoluteCol, int z)
        {
            LatLngPoint p = getLatLngFromTile(absoluteRow, absoluteCol, getScaleFromZoom(z, dpi));
            return new System.Drawing.Point(Convert.ToInt32(Math.Floor(p.Lng)), Convert.ToInt32(Math.Floor(p.Lat)));
        }
        public PointF LatLng2Mercator(LatLngPoint p)
        {
            double[] arr = null;
            for (var i = 0; i < array1.Length; i++)
            {
                if (p.Lat >= array1[i])
                {
                    arr = array2[i];
                    break;
                }
            }
            if (arr == null)
            {
                for (var i = array1.Length - 1; i >= 0; i--)
                {
                    if (p.Lat <= -array1[i])
                    {
                        arr = array2[i];
                        break;
                    }
                }
            }
            double[] res = Convertor(p.Lng, p.Lat, arr);
            return new PointF((float)res[0], (float)res[1]);
        }
        private static double[] Convertor(double x, double y, double[] param)
        {
            var T = param[0] + param[1] * Math.Abs(x);
            var cC = Math.Abs(y) / param[9];
            var cF = param[2] + param[3] * cC + param[4] * cC * cC + param[5] * cC * cC * cC + param[6] * cC * cC * cC * cC + param[7] * cC * cC * cC * cC * cC + param[8] * cC * cC * cC * cC * cC * cC;
            T *= (x < 0 ? -1 : 1);
            cF *= (y < 0 ? -1 : 1);
            return new double[] { T, cF };
        }
        public LatLngPoint getLatLngFromTile(int absoluteRow, int absoluteCol, double scale)
        {
            setResolution(scale);
            return tileToCoordArea(absoluteRow, absoluteCol, resolution * tileHeight, packetSize, origin).getLeftTop();
        }
        private Area tileToCoordArea(double absoluteRow, double absoluteCol, double tileWidthByMapUnit, int packetSize, Coordinate origin)
        {
            double maxY = origin.Y - absoluteRow * tileWidthByMapUnit;
            double minX = origin.X + absoluteCol * tileWidthByMapUnit;
            double minY = maxY - tileWidthByMapUnit;
            double maxX = minX + tileWidthByMapUnit;
            Area result = new Area();
            result.XMax = maxX;
            result.XMin = minX;
            result.YMax = maxY;
            result.YMin = minY;
            return result;
        }
        private void setResolution(double scale)
        {
            resolution = 2.54 * scale / 100 / dpi / MAPUNIT;
        }
        public double getScaleFromZoom(int z, double dpi)
        {
            return 100 * Math.Pow(2, 18 - z) / 2.54 * dpi / scaleFactor;
        }
        private void updateResolutionAndZoom(int z)
        {
            if (zoomLevel != z)
            {
                resolution = Math.Pow(2, 18 - z)  / MAPUNIT;
                zoomLevel = z;
            }
        }
        #endregion
    }
}
