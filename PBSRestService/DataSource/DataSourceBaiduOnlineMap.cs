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
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using Point = PBS.Util.Point;
using System.Text.RegularExpressions;


namespace PBS.DataSource
{
    public class DataSourceBaiduOnlineMap : DataSourceCustomOnlineMaps
    {
        public string OutputFileName{ get; set;}
        public string Version { get; set; }
        public bool autoCorrectCoord { get; set; }
        public PBS.Util.Envelope downloadExtent;


        private SQLiteConnection _outputconn;
        private string _correctOutput;
        private SQLiteConnection _correctOutputconn;
        private string _mapName;
        protected long _wroteBytes = 0;
        protected long _wroteCounts = 0;
        public long WroteBytes { get{return _wroteBytes;} }
        public long WroteCounts { get { return _wroteCounts; } }
        private string _baseUrl;
        private string[] _subDomains;
        private static string CONFIG_FILE_NAME;
        private static List<CustomOnlineMap> _customOnlineMaps;
        public static List<CustomOnlineMap> CustomOnlineMaps
        {
            get
            {
                if (_customOnlineMaps == null)
                    _customOnlineMaps = BaiDuMapManager.inst.ReadOnlineMapsConfigFile("BaiduMaps.xml");
                return _customOnlineMaps;
            }
        }
        public override byte[] GetTileBytesFromLocalCache(int level, int row, int col)
        {
            return null;
        }
        private void CreateCCMBTilesFile(string outputPath, string name, string description, string attribution, Geometry geometry)
        {
            int lastPointIndex = outputPath.LastIndexOf(".");
            string pre = outputPath.Substring(0, lastPointIndex);
            string post = outputPath.Substring(lastPointIndex, outputPath.Length - lastPointIndex);
            _correctOutput = pre + "_CC" + post;
            CreateMBTilesFileAndWriteMetaData(_correctOutput, name, description, attribution, geometry);
            _correctOutputconn = new SQLiteConnection("Data source = " + _correctOutput);
            _correctOutputconn.Open();
            using (SQLiteTransaction transaction = _correctOutputconn.BeginTransaction())
            {
                using (SQLiteCommand cmd = new SQLiteCommand(_correctOutputconn))
                {
                    cmd.CommandText = "CREATE TABLE scope (zoom_level INTEGER, rowStart INTEGER, rowEnd INTEGER, colStart INTEGER, colEnd INTEGER)";
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }
        }

        protected override void CreateMBTilesFileAndWriteMetaData(string outputPath, string name, string description, string attribution, Geometry geometry)
        {
            //check the wkid
            if (!TilingScheme.WKID.Equals(102100) && !TilingScheme.WKID.Equals(3857))
            {
                throw new Exception("The WKID of ArcGIS Cache is not 3857 or 102100!");
            }
            //check the numbers of lods
            if (TilingScheme.LODs.Length != 20)
            {
                throw new Exception("The count of levels must be 20! Current levels count = " + TilingScheme.LODs.Length);
            }
            //check the tile dimension
            if (!TilingScheme.TileCols.Equals(256) || !TilingScheme.TileRows.Equals(256))
            {
                throw new Exception("The size of a tile is not 256*256!");
            }
            //create new sqlite database
            SQLiteConnection.CreateFile(outputPath);
            using (SQLiteConnection conn = new SQLiteConnection("Data source = " + outputPath))
            {
                conn.Open();
                using (SQLiteTransaction transaction = conn.BeginTransaction())
                {
                    using (SQLiteCommand cmd = new SQLiteCommand(conn))
                    {
                        #region create tables and indexes
                        //ref http://mapbox.com/developers/mbtiles/
                        //metadata table
                        cmd.CommandText = "CREATE TABLE metadata (name TEXT, value TEXT)";
                        cmd.ExecuteNonQuery();
                        //images table
                        cmd.CommandText = "CREATE TABLE images (tile_data BLOB, tile_id TEXT)";
                        cmd.ExecuteNonQuery();

                        cmd.CommandText = "CREATE TABLE errorInfo (url TEXT, retryTimes INTEGER, recovered INTEGER, descrption TEXT)";
                        cmd.ExecuteNonQuery();
                        //map table
                        cmd.CommandText = "CREATE TABLE map (zoom_level INTEGER, tile_column INTEGER, tile_row INTEGER, tile_id TEXT)";
                        cmd.ExecuteNonQuery();
                        //tiles view
                        cmd.CommandText = @"CREATE VIEW tiles AS SELECT
    map.zoom_level AS zoom_level,
    map.tile_column AS tile_column,
    map.tile_row AS tile_row,
    images.tile_data AS tile_data
FROM map JOIN images ON images.tile_id = map.tile_id";
                        cmd.ExecuteNonQuery();
                        //indexes
                        cmd.CommandText = "CREATE UNIQUE INDEX images_id on images (tile_id)";
                        cmd.ExecuteNonQuery();
                        cmd.CommandText = "CREATE UNIQUE INDEX map_index on map (zoom_level, tile_column, tile_row)";
                        cmd.ExecuteNonQuery();
                        cmd.CommandText = @"CREATE UNIQUE INDEX name ON metadata (name)";
                        cmd.ExecuteNonQuery();
                        #endregion
                        #region write metadata
                        //name
                        cmd.CommandText = @"INSERT INTO metadata(name,value) VALUES (""name"",""" + name + @""")";
                        cmd.ExecuteNonQuery();
                        //type
                        cmd.CommandText = "INSERT INTO metadata(name,value) VALUES ('type','baselayer')";
                        cmd.ExecuteNonQuery();
                        //version
                        cmd.CommandText = "INSERT INTO metadata(name,value) VALUES ('version','1.2')";
                        cmd.ExecuteNonQuery();
                        //description
                        cmd.CommandText = @"INSERT INTO metadata(name,value) VALUES (""description"",""" + description + @""")";
                        cmd.ExecuteNonQuery();
                        //format
                        string f = TilingScheme.CacheTileFormat.ToString().ToUpper().Contains("PNG") ? "png" : "jpg";
                        cmd.CommandText = "INSERT INTO metadata(name,value) VALUES ('format','" + f + "')";
                        cmd.ExecuteNonQuery();
                        //bounds
                        //Point bottomLeft = Utility.WebMercatorToGeographic(new Point(geometry.Extent.XMin, geometry.Extent.YMin));
                        //Point upperRight = Utility.WebMercatorToGeographic(new Point(geometry.Extent.XMax, geometry.Extent.YMax));
                        cmd.CommandText = "INSERT INTO metadata(name,value) VALUES ('bounds','" + string.Format("{0},{1},{2},{3}", geometry.Extent.XMin, geometry.Extent.YMin, geometry.Extent.XMax, geometry.Extent.YMax) + "')";
                        cmd.ExecuteNonQuery();
                        //attribution
                        cmd.CommandText = @"INSERT INTO metadata(name,value) VALUES (""attribution"",""" + attribution + @""")";
                        cmd.ExecuteNonQuery();
                        #endregion
                    }
                    transaction.Commit();
                }
            }
        }
        protected override void  SaveOneLevelTilesToMBTiles(int level, int startRowLevel, int startColLevel, int endRowLevel, int endColLevel)
        {
            int bundleSize = BaiDuMapManager.inst.bundleSize;
            Bundle startBundle = new Bundle(bundleSize, level, startRowLevel / bundleSize, startColLevel / bundleSize, TilingScheme);
            Bundle endBundle = new Bundle(bundleSize, level, endRowLevel / bundleSize, endColLevel / bundleSize, TilingScheme);
            List<Bundle> allBundles = new List<Bundle>();
            for (int bRow = startBundle.Row; bRow <= endBundle.Row; bRow++)
            {
                for (int bCol = startBundle.Col; bCol <= endBundle.Col; bCol++)
                {
                    Bundle b = new Bundle(bundleSize, level, bRow, bCol, TilingScheme);
                    if (_downloadGeometry is Polygon)
                    {
                        bool bPolygonTouchesWithBundle = false;
                        Polygon polygon = _downloadGeometry as Polygon;
                        if (polygon.ContainsPoint(b.Extent.LowerLeft) || polygon.ContainsPoint(b.Extent.LowerRight) || polygon.ContainsPoint(b.Extent.UpperLeft) || polygon.ContainsPoint(b.Extent.UpperRight))
                            bPolygonTouchesWithBundle = true;
                        if (b.Extent.ContainsPoint(polygon.Extent.LowerLeft) && b.Extent.ContainsPoint(polygon.Extent.LowerRight) && b.Extent.ContainsPoint(polygon.Extent.UpperLeft) && b.Extent.ContainsPoint(polygon.Extent.UpperRight))
                            bPolygonTouchesWithBundle = true;
                        if (polygon.IsIntersectsWithPolygon(b.Extent.ToPolygon()))
                            bPolygonTouchesWithBundle = true;
                        if (!bPolygonTouchesWithBundle)
                            continue;
                    }
                    allBundles.Add(b);
                }
            }

            int maxThreadCount = BaiDuMapManager.inst.maxThreadCount;
            int queueCount = allBundles.Count % maxThreadCount == 0 ? allBundles.Count / maxThreadCount : allBundles.Count / maxThreadCount + 1;
            
            
            for (int queue = 0; queue < queueCount; queue++)
            {
                int startBundleIndex = maxThreadCount * queue;
                int endBundleIndex = startBundleIndex + maxThreadCount - 1;
                endBundleIndex = endBundleIndex > allBundles.Count ? allBundles.Count - 1 : endBundleIndex;
                List<Task> tasks = new List<Task>();
                for (int i = startBundleIndex; i <= endBundleIndex; i++)
                {
                    if (_convertingStatus.IsCancelled)
                        return;
                    Bundle b = allBundles[i];
                    int startR = startRowLevel > b.StartTileRow ? startRowLevel : b.StartTileRow;
                    int startC = startColLevel > b.StartTileCol ? startColLevel : b.StartTileCol;
                    int endR = endRowLevel > b.EndTileRow ? b.EndTileRow : endRowLevel;
                    int endC = endColLevel > b.EndTileCol ? b.EndTileCol : endColLevel;
                    Task t = Task.Factory.StartNew(() => { WriteTilesToSqlite(GetTilesByExtent(level, startR, startC, endR, endC), _outputconn); }, _cts.Token);
                    tasks.Add(t);
                }
                if (tasks.Count > _convertingStatus.ThreadCount)
                {
                    _convertingStatus.ThreadCount = tasks.Count;
                }
                try
                {
                    Task.WaitAll(tasks.ToArray());
                }
                catch (AggregateException)
                {
                }
            }
            _convertingStatus.IsCommittingTransaction = true;          
            _convertingStatus.IsCommittingTransaction = false;
            
        }
        private void WriteTilesToSqlite(int level, int col, int row, byte[] data, SQLiteCommand cmd)
        {
            string guid = Guid.NewGuid().ToString();
            cmd.CommandText = "INSERT INTO images VALUES (@tile_data,@tile_id)";
            cmd.Parameters.AddWithValue("tile_data", data);
            cmd.Parameters.AddWithValue("tile_id", guid);
            cmd.ExecuteNonQuery();
            cmd.CommandText = "INSERT INTO map VALUES (@zoom_level,@tile_column,@tile_row,@tile_id)";
            cmd.Parameters.AddWithValue("zoom_level", level);
            cmd.Parameters.AddWithValue("tile_column", col);
            cmd.Parameters.AddWithValue("tile_row", row);
            cmd.Parameters.AddWithValue("tile_id", guid);
            cmd.ExecuteNonQuery();
        }
        protected void WriteTilesToSqlite(Dictionary<string, byte[]> dict, SQLiteConnection conn)
        {
            lock (DataSourceBase._locker)
            {
                SQLiteTransaction transaction = conn.BeginTransaction();
                using (SQLiteCommand cmd = new SQLiteCommand(conn))
                {
                    try
                    {
                        foreach (KeyValuePair<string, byte[]> kvp in dict)
                        {
                            string[] splitParts = kvp.Key.Split(new char[] { '/' });
                            int level = int.Parse(splitParts[0]);
                            int col = int.Parse(splitParts[1]);
                            int row = int.Parse(splitParts[2]);
                            WriteTilesToSqlite(level, col, row, kvp.Value, cmd);
                        }
                    }
                    catch(Exception e)
                    {
                        Utility.Log(LogLevel.Error, e, "WriteTilesToSqlite Error !");
                    }
                }
                transaction.Commit();
                if (transaction != null)
                    transaction.Dispose();
            }
        }
        protected override void InternalOnTileLoaded(object o, TileLoadEventArgs a)
        {
            if (a.GeneratedMethod != TileGeneratedSource.DynamicOutput)
                return;
            if (o is DataSourceRasterImage && !ConfigManager.App_AllowFileCacheOfRasterImage ||
                IsOnlineMap && !ConfigManager.App_AllowFileCacheOfOnlineMaps)
                return;
            string key = string.Format("{0}/{1}/{2}", a.Level, a.Column, a.Row);
            if (base._dictTilesToBeLocalCached.ContainsKey(key))
                return;
            _dictTilesToBeLocalCached.TryAdd(key, a.TileBytes);
            if (_dictTilesToBeLocalCached.Count == 1000)
            {
                WriteTilesToLocalCacheFile(_dictTilesToBeLocalCached);
                _dictTilesToBeLocalCached.Clear();
            }
        }
        /// <summary>
        /// when isForConvert==true, gettile() method will return null instead of returning an error image byte[]
        /// </summary>
        /// <param name="name"></param>
        public DataSourceBaiduOnlineMap(string name)
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
            _baseUrl = map.Url.Replace("{$s}", "{0}").Replace("{$x}", "{2}").Replace("{$y}", "{3}").Replace("{$z}", "{1}").Replace("{$v}", "{4}");
            _subDomains = map.Servers;
        }

        protected override void ReadTilingScheme(out TilingScheme tilingScheme)
        {
            tilingScheme = SchemaProvider.Inst.getSchema("BaiDuOnline", "default", null);
            //this.TilingScheme = TilingSchemePostProcess(tilingScheme);

            if (_mapName.ToLower().Contains("image"))
                this.TilingScheme.CacheTileFormat = ImageFormat.JPG;
        }

        protected override Dictionary<string, byte[]> GetTilesByExtent(int level, int startRow, int startCol, int endRow, int endCol)
        {
            int totalBytes = 0;

            Dictionary<string, byte[]> dict = new Dictionary<string, byte[]>();
            for (int r = startRow; r <= endRow; r++)
            {
                for (int c = startCol; c <= endCol; c++)
                {
                    byte[] bytes = GetTileBytes(level, r, c);
                    if (bytes != null)
                    {
                        if (bytes.Length > 145)
                        {
                            dict.Add(string.Format("{0}/{1}/{2}", level, c, r), bytes);
                            totalBytes = totalBytes + bytes.Length;
                            Interlocked.Add(ref _wroteBytes, bytes.Length);
                            Interlocked.Increment(ref _wroteCounts);
                        }

                        _convertingStatus.LevelCompleteCount = Interlocked.Increment(ref _levelCompleteCount);
                        _convertingStatus.CompleteCount = Interlocked.Increment(ref _completeCount);
                        _convertingStatus.CompleteTotalBytes = Interlocked.Add(ref _completeTotalBytes, bytes.Length);
                    }
                    else
                    {
                        _convertingStatus.LevelErrorCount = Interlocked.Increment(ref _levelErrorCount);
                        _convertingStatus.ErrorCount = Interlocked.Increment(ref _errorCount);
                    }
#if Debug
                    System.Diagnostics.Debug.WriteLine(_convertingStatus.LevelCompleteCount + " / " + _convertingStatus.LevelTotalCount + "  |||  " + level + "/" + r + "/" + c + "thread:" + Thread.CurrentThread.ManagedThreadId);
#endif
                }
            }
            return dict;
        }
        private void recordFailure(string url, string reason)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(_outputconn))
            {
                cmd.CommandText = "INSERT INTO errorInfo VALUES (@url,@retryTimes, @recovered, @descrption)";
                cmd.Parameters.AddWithValue("url", url);
                cmd.Parameters.AddWithValue("retryTimes", 0);
                cmd.Parameters.AddWithValue("recovered", 0);
                cmd.Parameters.AddWithValue("descrption", reason);
                cmd.ExecuteNonQuery();
            }
        }
        protected override byte[] HttpGetTileBytes(string uri)
        {
            byte[] result = null;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri + "&t=" + Guid.NewGuid().ToString("N"));
            request.Accept = "*/*";
            request.KeepAlive = true;
            request.Method = "GET";
            if (this.Type == DataSourceTypePredefined.ArcGISTiledMapService.ToString())
            {
                request.Referer = this.Path + "?f=jsapi";
            }

            request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.4 (KHTML, like Gecko) Chrome/22.0.1229.94 Safari/537.4";

            request.Proxy = null;
            request.Timeout = 20000;

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    recordFailure(uri, response.StatusCode.ToString());
                }
                if (!response.ContentType.ToLower().Contains("image"))
                    throw new Exception("download(http get) result is not image");

                result = Util.Utility.StreamToBytes(response.GetResponseStream());
                if (result == null)
                {
                    recordFailure(uri, response.StatusDescription);
                }
                return result;
            }

        }

        public override byte[] GetTileBytes(int level, int row, int col)
        {
            string baseUrl = _baseUrl;
            string subdomain = string.Empty;
            string uri = string.Empty;
            subdomain = _subDomains[Math.Abs(level + col + row) % _subDomains.Length];
            byte[] bytes;
            try
            {
                if ("BaiduBase" == _mapName)
                {
                    uri = string.Format(baseUrl, level, col, row, subdomain);
                }
                else if ("BaiduSate" == _mapName)
                {
                    uri = string.Format(baseUrl, level, col, row, Version, subdomain);
                }
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
                return bytes;
            }
            catch (Exception e)
            {
                //when this datasource is using for converting online tiles to offline format, return null if there is something wrong with downloading, otherwise, return a error image for PBS service to display.
                if (ConvertingStatus.IsInProgress)
                {
                    recordFailure(uri, e.Message);
                    return null;
                }
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
                {                   
                    ConvertCompleted(this, new ConvertEventArgs(ConvertingStatus.IsCompletedSuccessfully));
                }
            }
            catch (Exception e)
            {
                throw new Exception("Online maps converting to MBTiles error!\r\n" + e.Message);
            }
        }
        private void recordCount(int count)
        {
            using (SQLiteTransaction transaction = _correctOutputconn.BeginTransaction())
            {
                using (SQLiteCommand cmd = new SQLiteCommand(_correctOutputconn))
                {
                    cmd.CommandText = "INSERT INTO metadata(name,value) VALUES ('TotalTileCount','" + count + "')";
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }
        }
        private void recordScope(int level, int startR, int startC, int endR, int endC)
        {
            using (SQLiteTransaction transaction = _correctOutputconn.BeginTransaction())
            {
                using (SQLiteCommand cmd = new SQLiteCommand(_correctOutputconn))
                {

                    cmd.CommandText = "INSERT INTO scope VALUES (@zoom_level, @rowStart, @rowEnd, @colStart, @colEnd)";
                    cmd.Parameters.AddWithValue("zoom_level", level);
                    cmd.Parameters.AddWithValue("rowStart", startR);
                    cmd.Parameters.AddWithValue("rowEnd", endR);
                    cmd.Parameters.AddWithValue("colStart", startC);
                    cmd.Parameters.AddWithValue("colEnd", endC);
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }
        }
        protected override void DoConvertToMBTiles(string outputPath, string name, string description, string attribution, int[] levels, Geometry geometry, bool doCompact)
        {
            Util.Envelope initial = geometry as Envelope;
            Util.Point pLeftTop = Utility.GeographicToWebMercator(new Util.Point(initial.XMin, initial.YMax));
            Util.Point pRightBottom = Utility.GeographicToWebMercator(new Util.Point(initial.XMax, initial.YMin));
            Envelope downloadExtentMercator = new Envelope(pLeftTop.X, pRightBottom.Y, pRightBottom.X, pLeftTop.Y);

            _outputFile = outputPath;
            _downloadGeometry = geometry;
            _convertingStatus.IsInProgress = true;
            try
            {
                CreateMBTilesFileAndWriteMetaData(outputPath, name, description, attribution, geometry);
                if (autoCorrectCoord)
                {
                    CreateCCMBTilesFile(outputPath, name, description, attribution, geometry);
                }
                _outputconn = new SQLiteConnection("Data source = " + base._outputFile);
                _outputconn.Open();
                
                #region calculate startCol/Row and endCol/Row and tiles count of each level
                _convertingStatus.TotalCount = 0;
                string[] keyTileInfos = new string[levels.Length];
                int[] tilesCountOfLevel = new int[levels.Length];
                for (int i = 0; i < levels.Length; i++)
                {
                    int level = TilingScheme.LODs[levels[i]].LevelID;
                    RCRange range = BaiDuMapManager.inst.getBaiduRCRangeFromGPS(geometry as Envelope, level);
                    int startTileRow = range.MinRow;
                    int startTileCol = range.MinCol;
                    int endTileRow = range.MaxRow;
                    int endTileCol = range.MaxCol;
                    keyTileInfos[i] = string.Format("{0},{1},{2},{3}", startTileRow, startTileCol, endTileRow, endTileCol);
                    tilesCountOfLevel[i] = Math.Abs((endTileCol - startTileCol + 1) * (endTileRow - startTileRow + 1));
                    _convertingStatus.TotalCount += tilesCountOfLevel[i];
                }
                _totalCount = _convertingStatus.TotalCount;
                _completeCount = _errorCount = 0;
                _wroteBytes = _wroteCounts = 0;
                #endregion
                int arcgisTileCount = 0;
                for (int i = 0; i < levels.Length; i++)
                {
                    int level, startR, startC, endR, endC;//startTileRow,startTileCol,...
                    level = TilingScheme.LODs[levels[i]].LevelID;
                    startR = int.Parse(keyTileInfos[i].Split(new char[] { ',' })[0]);
                    startC = int.Parse(keyTileInfos[i].Split(new char[] { ',' })[1]);
                    endR = int.Parse(keyTileInfos[i].Split(new char[] { ',' })[2]);
                    endC = int.Parse(keyTileInfos[i].Split(new char[] { ',' })[3]);
                    _convertingStatus.Level = level;
                    _convertingStatus.LevelTotalCount = tilesCountOfLevel[i];
                    _convertingStatus.LevelCompleteCount = _convertingStatus.LevelErrorCount = 0;
                    _levelTotalCount = _convertingStatus.LevelTotalCount;
                    _levelCompleteCount = _levelErrorCount = 0;                    
                    SaveOneLevelTilesToMBTiles(level, startR, startC, endR, endC);
                    if (autoCorrectCoord)
                    {
                        RCRange range = BaiDuMapManager.inst.getArcgisRCRangeFromMercator(downloadExtentMercator, levels[i]);
                        startR = range.MinRow;
                        startC = range.MinCol;
                        endR = range.MaxRow;
                        endC = range.MaxCol;
                        recordScope(level, startR, startC, endR, endC);
                        arcgisTileCount += (endR - startR + 1) * (endC - startC + 1);
                    }
                    if (_convertingStatus.IsCancelled)
                    {
                        _convertingStatus.IsCompletedSuccessfully = false;
                        break;
                    }
                }
                recordCount(arcgisTileCount);
                if (doCompact)
                {
                    _convertingStatus.IsDoingCompact = true;
                    _convertingStatus.SizeBeforeCompact = new FileInfo(_outputFile).Length;
                    CompactMBTiles(_outputFile);
                    _convertingStatus.IsDoingCompact = false;
                    _convertingStatus.SizeAfterCompact = new FileInfo(_outputFile).Length;
                }
                if (!_convertingStatus.IsCancelled)
                    _convertingStatus.IsCompletedSuccessfully = true;
            }
            finally
            {
                if (_correctOutputconn != null)
                {
                    _correctOutputconn.Dispose();
                }
                RecoverFailure();
                _convertingStatus.IsInProgress = false;
                _convertingStatus.IsCommittingTransaction = false;
                _convertingStatus.IsDoingCompact = false;
                _outputconn.Close();
                _outputconn.Dispose();
            }
        }
        private System.Collections.Hashtable parseParam(String url)
        {
            System.Collections.Hashtable result = new System.Collections.Hashtable();
            string b = _baseUrl;
            Regex r = new Regex("\\{\\d+\\}");
            string urlReg = r.Replace(_baseUrl, "(\\d+)").Replace("?", "\\?");
            Match match = new Regex(urlReg).Match(url);
            result.Add("row", match.Groups[3].Value);
            result.Add("col", match.Groups[2].Value);
            result.Add("zoom", match.Groups[4].Value);
            return result;
        }
        private void fetchSingleTile(string[] urls)
        {
            foreach (string url in urls)
            {
                try
                {
                    byte[] bytes = HttpGetTileBytes(url);
                    if (bytes != null)
                    {
                        lock (DataSourceBase._locker)
                        {
                            using (SQLiteCommand command = new SQLiteCommand(_outputconn))
                            {
                                command.CommandText = "UPDATE errorInfo SET retryTimes = retryTimes + 1, recovered = 1 where url = @u";
                                command.Parameters.AddWithValue("u", url);
                                command.ExecuteNonQuery();
                                _convertingStatus.CompleteCount = Interlocked.Increment(ref _completeCount);
                                _convertingStatus.CompleteTotalBytes = Interlocked.Add(ref _completeTotalBytes, bytes.Length);
                                _convertingStatus.ErrorCount = Interlocked.Decrement(ref _errorCount);
                                if (bytes.Length > 145)
                                {
                                    Interlocked.Add(ref _wroteBytes, bytes.Length);
                                    Interlocked.Increment(ref _wroteCounts);
                                    System.Collections.Hashtable p = parseParam(url);
                                    //mPoint represent our mercator row col num
                                    WriteTilesToSqlite(int.Parse(p["zoom"] as string), int.Parse(p["col"] as string), int.Parse(p["row"] as string), bytes, command);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Utility.Log(LogLevel.Error, e, "Recover Single Tile failed!");
                }
            }
        }
        protected void RecoverFailure()
        {
            int maxThreadCount = BaiDuMapManager.inst.maxThreadCount;
            int seq = 0;
            List<Task> tasks = new List<Task>();
            
            using (SQLiteCommand cmd = new SQLiteCommand(_outputconn))
            {
                cmd.CommandText = "SELECT count(*) FROM errorInfo where recovered = 0";
                SQLiteDataReader reader = cmd.ExecuteReader();
                reader.Read();

                int failureCount = reader.GetInt32(0);
                if (failureCount == 0) return;
                int threadCount = maxThreadCount > failureCount ? failureCount : maxThreadCount;

                int queueLength = (failureCount + threadCount - 1) / threadCount;
                int heavyThreadCount = failureCount % threadCount;
                int swithPoint = queueLength - 1;
                if (heavyThreadCount == 0)
                {
                    queueLength++;
                    swithPoint = queueLength - 2;
                }
                reader.Close();

                SQLiteTransaction updateTransaction = _outputconn.BeginTransaction();
                cmd.CommandText = "SELECT url, retryTimes FROM errorInfo where recovered = 0";
                reader = cmd.ExecuteReader();
                List<string> urls = new List<string>();
                while (reader.Read())
                {
                    string url = reader.GetString(0);
                    urls.Add(url);
                    int retry = reader.GetInt32(1);
                    if (seq == swithPoint)
                    {
                        Task t = Task.Factory.StartNew(u => { fetchSingleTile(u as string[]); }, urls.ToArray());
                        urls.Clear();
                        heavyThreadCount--;
                        if (heavyThreadCount > 0)
                        {
                            swithPoint += queueLength;
                        }
                        else
                        {
                            swithPoint += queueLength - 1;
                        }
                        tasks.Add(t);
                    }
                    seq = seq + 1;
                }
                Task.WaitAll(tasks.ToArray());
                updateTransaction.Commit();
                updateTransaction.Dispose();
            }
 
            
            //_outputconn.Close();
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
}
