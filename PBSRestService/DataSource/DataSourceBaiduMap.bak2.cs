//****************************************
//Copyright@diligentpig, https://geopbs.codeplex.com
//Please using source code under LGPL license.
//****************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Xml.Linq;
using PBS.Util;
using System.Data.SQLite;
using PBS.Service;
using System.Diagnostics;
using System.Drawing;
using Point = PBS.Util.Point;
namespace PBS.DataSource
{
    public class DataSourceBaiduMap : DataSourceCustomOnlineMaps
    {
        FileStream eStream;
        StreamWriter eWriter;
        public double resolution;
        public double dpi = 96;
        public double scaleFactor = 1.001134;
        //public double MAPUNIT = 111194.64983247776;
        public double MAPUNIT = 1;
        public int tileHeight = 256;
        public int packetSize = 128;
        public Coordinate origin = new Coordinate(-20037508.3427892, 20037508.3427892);
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

        private string _mapName;
        private string _baseUrl;
        private string[] _subDomains;
        private static string CONFIG_FILE_NAME;
        private static List<CustomOnlineMap> _customOnlineMaps;
        public static List<CustomOnlineMap> CustomOnlineMaps
        {
            get
            {
                if (_customOnlineMaps == null)
                    ReadOnlineMapsConfigFile();
                return _customOnlineMaps;
            }
        }
        /// <summary>
        /// Read custom online maps conifgs from xml file.
        /// </summary>
        private static void ReadOnlineMapsConfigFile()
        {
            try
            {
                CONFIG_FILE_NAME = AppDomain.CurrentDomain.BaseDirectory + "CustomOnlineMaps.xml";
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
                        Url = map.Url.Value
                    });
                }
            }
            catch (Exception e)
            {
                throw new Exception("Could not parse" + CONFIG_FILE_NAME + " file!\r\n" + e.Message);
            }
        }

        /// <summary>
        /// when isForConvert==true, gettile() method will return null instead of returning an error image byte[]
        /// </summary>
        /// <param name="name"></param>
        public DataSourceBaiduMap(string name)
        {
            _mapName = name;
            Initialize("N/A");
            if (ConfigManager.App_AllowFileCacheOfOnlineMaps)
            {
                //init local cache file if does not exist.
                string localCacheFileName = ConfigManager.App_FileCachePath + "\\" + _mapName.Trim().ToLower() + ".cache";
                ValidateLocalCacheFile(localCacheFileName);
                TileLoaded += new EventHandler<TileLoadEventArgs>(InternalOnTileLoaded);
            }
            ConvertingStatus = new ConvertStatus();
        }

        //public DataSourceCustomOnlineMaps(string name)
        //    : this(name, false)<param name="isForConvert">indicate this datasource is for format convert purpose, not for PBS service purpose. when this is true, gettile() method will return null instead of returning an error image byte[]</param>
        //{

        //}

        protected override void Initialize(string path)
        {
            this.Type = _mapName;
            this.Path = path;
            TilingScheme ts;
            try
            {
                ReadTilingScheme(out ts);
            }
            catch (Exception e)
            {
                throw new Exception("Reading tiling shceme failed!\r\n" + e.Message + "\r\n" + e.StackTrace);
            }
            TilingScheme = ts;
            IsOnlineMap = IsOnlineMaps(Type);

            CustomOnlineMap map = CustomOnlineMaps.Where(m => m.Name == _mapName).ToList()[0];
            _baseUrl = map.Url.Replace("{$s}", "{0}").Replace("{$x}", "{2}").Replace("{$y}", "{3}").Replace("{$z}", "{1}");
            _subDomains = map.Servers;
            //for bing maps imagery, add bing key if has one
            if (_mapName.ToLower().Contains("bing") && _mapName.ToLower().Contains("image") && !string.IsNullOrEmpty(ConfigManager.App_BingMapsAppKey))
                _baseUrl += "&token=" + ConfigManager.App_BingMapsAppKey;
        }

        protected override void ReadTilingScheme(out TilingScheme tilingScheme)
        {
            ReadBaiduTilingScheme(out tilingScheme);
            this.TilingScheme = TilingSchemePostProcess(tilingScheme);

            //eStream = new FileStream("C:/Users/wenvi/Desktop/Portable1.txt", FileMode.Create);
            //eWriter = new StreamWriter(eStream);
            //for (int row = 320791; row <= 320847; row++)
            //{
            //    for (int col = 453247; col <= 453287; col++)
            //    {
            //        Point baiduPrj = ArcGISTileInfoToBD09Prj(row, col, 16);
            //        //eWriter.Write("Before, level:" + 3 + " row:" + row + " Column:" + col + ", After, row: " + baiduPrj.Y + " Column:" + baiduPrj.X + "\n");
            //    }       
            ////}
            //eWriter.Flush();
            //eWriter.Close();
            //eStream.Close();
            if (_mapName.ToLower().Contains("image"))
                this.TilingScheme.CacheTileFormat = ImageFormat.JPG;
        }

        protected void ReadBaiduTilingScheme(out TilingScheme tilingScheme)
        {
            tilingScheme = new TilingScheme();
            StringBuilder sb;
            tilingScheme.Path = "N/A";
            tilingScheme.CacheTileFormat = ImageFormat.PNG;
            tilingScheme.CompressionQuality = 75;
            tilingScheme.DPI = 96;
            //LODs
            tilingScheme.LODs = new LODInfo[20];
            const double cornerCoordinate = 20037508.3427892;
            double resolution = 32730.948640372855 * 8;
            double scale = 123707275.005262 * 8;
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
            //create json
            sb = new StringBuilder("\r\n");
            //{"level" : 0, "resolution" : 156543.033928, "scale" : 591657527.591555}, 
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
        }
        public LatLngPoint RowColToLatLng(Point p, int zoom)
        {
            PointF merCatorPoint = new PointF((float)((p.X + 0.5) * 256 * Math.Pow(2, 18 - zoom)), (float)((p.Y + 0.5) * 256 * Math.Pow(2, 18 - zoom)));
            LatLngPoint result = Mercator2LatLng(merCatorPoint);
            //eWriter.Write("row:" + p.Y + " Column:" + p.X + "BaiDu Lat: " + result.Lat + " Lng:" + result.Lng + "\n");
            return result;
        }
        private static LatLngPoint Mercator2LatLng(PointF p)
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
        public Point ArcGISTileInfoToBD09Prj(int absoluteRow, int absoluteCol, int z)
        {
            LatLngPoint p = getLatLngFromTile(absoluteRow, absoluteCol, getScaleFromZoom(z, dpi));
            //eWriter.Write("WGS row:" + absoluteRow + " Column:" + absoluteCol + " WGS84 Lat: " + p.Lat + " Lng:" + p.Lng + "..");
            return getRowCol(p, z);
        }
        public Point getRowCol(LatLngPoint p, int zoom)
        {
            PointF merCatorPoint = LatLng2Mercator(p);          
            double f_row = merCatorPoint.Y / Math.Pow(2, 18 - zoom) / 256;
            double f_col = merCatorPoint.X / Math.Pow(2, 18 - zoom) / 256;
            int row = Convert.ToInt32(Math.Floor(f_row));
            int col = Convert.ToInt32(Math.Floor(f_col));
            //eWriter.Write("Baidu row: " + f_row + " col:" + f_col + "\n");
            return new Point(col, row);
        }
        public Point getRowColFromMercator(int absoluteRow, int absoluteCol, int z)
        {
            LatLngPoint p = getLatLngFromTile(absoluteRow, absoluteCol, getScaleFromZoom(z, dpi));
            double f_row = p.Lat / Math.Pow(2, 18 - z) / 256;
            double f_col = p.Lng / Math.Pow(2, 18 - z) / 256;
            int row = Convert.ToInt32(Math.Floor(f_row));
            int col = Convert.ToInt32(Math.Floor(f_col));
            return new Point(col, row);
        }
        private static PointF LatLng2Mercator(LatLngPoint p)
        {
            double[] arr = null;
            double n_lat = p.Lat > 74 ? 74 : p.Lat;
            n_lat = n_lat < -74 ? -74 : n_lat;
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
            return tileToCoordArea(absoluteRow, absoluteCol, resolution * tileHeight, packetSize, origin).getCenter();
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
        public override byte[] GetTileBytes(int level, int row, int col)
        {
            string baseUrl = _baseUrl;
            string subdomain = string.Empty;
            string uri = string.Empty;
            subdomain = _subDomains[(level + col + row) % _subDomains.Length];
            Point baiduPrj = getRowColFromMercator(row, col, level);
            //if (col == 4 && level == 0)
            //{
            //    eWriter.Write("Before, level:" + level + " row:" + row + " Column:" + col + ", After, row: " + baiduPrj.Y + " Column:" + baiduPrj.X + "\n");
            //}
            //RowColToLatLng(baiduPrj, level + 3);

            byte[] bytes;
            try
            {
                uri = string.Format(baseUrl, subdomain, level, baiduPrj.X, baiduPrj.Y);
                if (!ConvertingStatus.IsInProgress)//accessing from PBSServiceProvider by PBS service client
                {
                    bytes = HttpGetTileBytes(uri);
                    return bytes;
                }
                else
                //accessing from DataSource directly when do format converting
                //Because when convert to MBTiles, bytes are retriving from datasource directly other than from PBSService, so TileLoaded event and local file cache checking in PBSServiceProvider.cs will not fire. Need to check if local file cache exist first, if not, fire TileLoaded event to let internal function to save tile to local file cache.
                {
                    TileLoadEventArgs tileLEA = new TileLoadEventArgs()
                    {
                        Level = level,
                        Row = row,
                        Column = col
                    };
                    //check if tile exist in local file cache
                    bytes = GetTileBytesFromLocalCache(level, row, col);
                    if (bytes != null)
                    {
                        tileLEA.GeneratedMethod = TileGeneratedSource.FromFileCache;
                    }
                    else
                    {
                        bytes = HttpGetTileBytes(uri);
                        tileLEA.GeneratedMethod = TileGeneratedSource.DynamicOutput;
                    }
                    tileLEA.TileBytes = bytes;
                    if (TileLoaded != null)
                        TileLoaded(this, tileLEA);
                }
                return bytes;
            }
            catch (Exception e)
            {
                //when this datasource is using for converting online tiles to offline format, return null if there is something wrong with downloading, otherwise, return a error image for PBS service to display.
                if (ConvertingStatus.IsInProgress)
                    return null;
                //if server has response(not a downloading error) and tell pbs do not have the specific tile, return null
                if (e is WebException && (e as WebException).Response != null && ((e as WebException).Response as HttpWebResponse).StatusCode == HttpStatusCode.NotFound)
                    return null;

                string suffix = this.TilingScheme.CacheTileFormat.ToString().ToUpper().Contains("PNG") ? "png" : "jpg";
                Stream stream = this.GetType().Assembly.GetManifestResourceStream("PBS.Assets.badrequest" + this.TilingScheme.TileCols + "." + suffix);
                bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);
                return bytes;
            }
        }

        #region IFormatConverter
        /// <summary>
        /// not implemented, using public void ConvertToMBTiles(string outputPath, string name, string description, string attribution, int[] levels, Envelope extent) instead.
        /// </summary>
        /// <param name="outputPath">The output path and file name of .mbtiles file.</param>
        /// <param name="name">The plain-english name of the tileset, required by MBTiles.</param>
        /// <param name="description">A description of the tiles as plain text., required by MBTiles.</param>
        /// <param name="attribution">An attribution string, which explains in English (and HTML) the sources of data and/or style for the map., required by MBTiles.</param>
        /// <param name="doCompact">implementing the reducing redundant tile bytes part of MBTiles specification?</param>
        public void ConvertToMBTiles(string outputPath, string name, string description, string attribution, bool doCompact)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Convert to MBTiles format.
        /// </summary>
        /// <param name="outputPath">The output path and file name of .mbtiles file.</param>
        /// <param name="name">The plain-english name of the tileset, required by MBTiles.</param>
        /// <param name="description">A description of the tiles as plain text., required by MBTiles.</param>
        /// <param name="attribution">An attribution string, which explains in English (and HTML) the sources of data and/or style for the map., required by MBTiles.</param>
        /// <param name="levels">tiles in which levels to convert to mbtiles.</param>
        /// <param name="geometry">convert/download extent or polygon, sr=3857. If this is Envelope, download by rectangle, if this is polygon, download by polygon's shape.</param>
        /// <param name="doCompact">implementing the reducing redundant tile bytes part of MBTiles specification?</param>
        public override void ConvertToMBTiles(string outputPath, string name, string description, string attribution, int[] levels, Geometry geometry, bool doCompact)
        {
            try
            {
                DoConvertToMBTiles(outputPath, name, description, attribution, levels, geometry, doCompact);
                if (ConvertCancelled != null && ConvertingStatus.IsCancelled)
                {
                    ConvertCancelled(this, new EventArgs());
                }
                if (ConvertCompleted != null)
                    ConvertCompleted(this, new ConvertEventArgs(ConvertingStatus.IsCompletedSuccessfully));
            }
            catch (Exception e)
            {
                throw new Exception("Online maps converting to MBTiles error!\r\n" + e.Message);
            }
        }
        /// <summary>
        /// Cancel any pending converting progress, and fire the ConvertCancelled event  when cancelled successfully.
        /// </summary>
        public void CancelConverting()
        {
            CancelDoConvertToMBTiles();
        }
        /// <summary>
        /// Fire when converting completed.
        /// </summary>
        public event ConvertEventHandler ConvertCompleted;
        /// <summary>
        /// Fire when converting cancelled gracefully.
        /// </summary>
        public event EventHandler ConvertCancelled;
        #endregion
    }
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
    }
}
