using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using PBS.Util;
using System.Windows;
using System.Threading;
using System.ComponentModel;
using Point = PBS.Util.Point;
using System.Data.SQLite;
using System.Xml.Linq;
using System.Net;
using PBS.Service;

namespace PBS.DataSource
{
    public class OtherMapDownloader
    {
        public void DownloadAsync(int[] Levels, string Output)
        {
            string fileName = AppDomain.CurrentDomain.BaseDirectory + "CustomTile.xml";
            if (!File.Exists(fileName))
            {
                throw new FileNotFoundException(fileName + " does not exist!");
            }
            XDocument xDoc = XDocument.Load(fileName);
            XElement element = xDoc.Element("TileInfo");

            PBS.Util.Envelope g = new PBS.Util.Envelope(Convert.ToDouble(element.Element("DownLoadExtent").Attribute("xmin").Value), Convert.ToDouble(element.Element("DownLoadExtent").Attribute("ymin").Value),
                Convert.ToDouble(element.Element("DownLoadExtent").Attribute("xmax").Value), Convert.ToDouble(element.Element("DownLoadExtent").Attribute("ymax").Value));
            string fileDest = "cache/" + Output;
            DataSourceOtherMap pgisSource = new DataSourceOtherMap("", true) { OutputFileName = Output, downloadExtent = g};
            pgisSource.ConvertCompleted += (s, a) =>
            {
                MessageBox.Show("Convert Success !");
            };
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += (s, a) =>
            {
                try
                {
                    pgisSource.ConvertToMBTiles(fileDest, "", "", "", Levels, g, false);
                }
                catch (Exception e)
                {
                    Utility.Log(LogLevel.Error, e, "Error");
                }
            };
            bw.RunWorkerAsync();
        }
    }
    public class DataSourceOtherMap : DataSourceBase
    {
        private SQLiteConnection _sqlConn;
        private static List<CustomOnlineMap> _customOnlineMaps;
        private static string CONFIG_FILE_NAME;
        public bool IS_ONLINE;
        public DataSourceOtherMap(string path, bool isOnline)
        {
            IS_ONLINE = isOnline;
            ConvertingStatus = new ConvertStatus();
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
            string _mapName = "OtherMap";
            _baseUrl = SchemaProvider.Inst.GetDownloadUrl(_mapName, null, null);
            if (!IS_ONLINE)
            {
                _sqlConn = new SQLiteConnection("Data Source=" + path);
                _sqlConn.Open();
            }
            this.Type = DataSourceTypePredefined.OtherMap.ToString();
        }
        ~DataSourceOtherMap()
        {
            if (_sqlConn != null)
                _sqlConn.Close();
            _sqlConn = null;
        }
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

        protected override void ReadTilingScheme(out TilingScheme schema)
        {
            schema = SchemaProvider.Inst.getSchema("OtherMap", null, null);
            //schema.InitialExtent = Utility.GPSToWebMercator(schema.InitialExtent);
            schema =  TilingSchemePostProcess(schema);
        }

        public virtual void ConvertToMBTiles(string outputPath, string name, string description, string attribution, int[] levels, Geometry geometry, bool doCompact)
        {
            try
            {
                DoConvertToMBTiles(outputPath, name, description, attribution, levels, geometry, doCompact);
                if (ConvertCompleted != null)
                    ConvertCompleted(this, new ConvertEventArgs(ConvertingStatus.IsCompletedSuccessfully));
            }
            catch (Exception e)
            {
                throw new Exception("PGIS converting to MBTiles error!\r\n" + e.Message);
            }
        }
        public string OutputFileName { get; set; }
        public event ConvertEventHandler ConvertCompleted;
        public PBS.Util.Envelope downloadExtent;
        private int TransferRow(int row, int level)
        {
            int gRow = ((int)(Math.Pow(2.0, (double)level) - 1.0)) - row - (int)Math.Pow(2.0, (double)(level - 1));
            return gRow;
        }
        private string _baseUrl;
        protected override Dictionary<string, byte[]> GetTilesByExtent(int level, int startRow, int startCol, int endRow, int endCol)
        {
            Dictionary<string, byte[]> dict = new Dictionary<string, byte[]>();
            for (int r = startRow; r <= endRow; r++)
            {
                for (int c = startCol; c <= endCol; c++)
                {
                    int transfromedRow = TransferRow(r, level);
                    byte[] bytes = GetTileBytes(level, r, c);
                    if (bytes != null)
                    {
                        if (bytes.Length > 0)
                        {
                            dict.Add(string.Format("{0}/{1}/{2}", level, c, r), bytes);
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
                }
            }
            return dict;
        }
        public byte[] getPNGFromNet(int level, int row, int col)
        {
            string baseUrl = _baseUrl;
            string subdomain = string.Empty;
            string uri = string.Empty;

            byte[] bytes;
            try
            {
                uri = string.Format(baseUrl, level, row, col);
                bytes = HttpGetTileBytes(uri);
                return bytes;
            }
            catch (Exception e)
            {
                Utility.Log(LogLevel.Error, e, "Download PGIS " + uri + " Error!");
                string suffix = this.TilingScheme.CacheTileFormat.ToString().ToUpper().Contains("PNG") ? "png" : "jpg";
                Stream stream = this.GetType().Assembly.GetManifestResourceStream("PBS.Assets.badrequest" + this.TilingScheme.TileCols + "." + suffix);
                bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);
                return bytes;
            }
        }
        public  byte[] getPNGFromFile(int level, int row, int col)
        {
            string commandText = string.Format("SELECT {0} FROM tiles WHERE tile_column={1} AND tile_row={2} AND zoom_level={3}", "tile_data", col, row, level);
            using (SQLiteCommand sqlCmd = new SQLiteCommand(commandText, _sqlConn))
            {
                object o = sqlCmd.ExecuteScalar();//null can not directly convert to byte[], if so, will return "buffer can not be null" exception
                if (o != null)
                {
                    return (byte[])o;
                }
                return null;
            }
        }

        public override byte[] GetTileBytes(int level, int row, int col)
        {
            if (IS_ONLINE)
            {
                return getPNGFromNet(level, row, col);
            }
            else
            {
                return getPNGFromFile(level, row, col);
            }
        }

        protected override byte[] HttpGetTileBytes(string uri)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.Accept = "*/*";
            request.KeepAlive = true;
            request.Method = "GET";
            //request.Headers.Add("Accept-Encoding", "gzip,deflate,sdch");
            if (this.Type == DataSourceTypePredefined.ArcGISTiledMapService.ToString())
            {
                request.Referer = this.Path + "?f=jsapi";
            }

            request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.4 (KHTML, like Gecko) Chrome/22.0.1229.94 Safari/537.4";

            request.Proxy = null;//==no proxy
            request.Timeout = 20000;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                return Util.Utility.StreamToBytes(response.GetResponseStream());
            }
        }

        protected override void WriteTilesToSqlite(Dictionary<string, byte[]> dict)
        {
            lock (_locker)
            {
                using (SQLiteConnection conn = new SQLiteConnection("Data source = " + _outputFile))
                {
                    conn.Open();
                    SQLiteTransaction transaction = conn.BeginTransaction();
                    try
                    {
                        using (SQLiteCommand cmd = new SQLiteCommand(conn))
                        {
                            foreach (KeyValuePair<string, byte[]> kvp in dict)
                            {
                                int level = int.Parse(kvp.Key.Split(new char[] { '/' })[0]);
                                int col = int.Parse(kvp.Key.Split(new char[] { '/' })[1]);
                                int row = int.Parse(kvp.Key.Split(new char[] { '/' })[2]);
                                string guid = Guid.NewGuid().ToString();
                                cmd.CommandText = "INSERT INTO images VALUES (@tile_data,@tile_id)";
                                cmd.Parameters.AddWithValue("tile_data", kvp.Value);
                                cmd.Parameters.AddWithValue("tile_id", guid);
                                cmd.ExecuteNonQuery();
                                cmd.CommandText = "INSERT INTO map VALUES (@zoom_level,@tile_column,@tile_row,@tile_id)";
                                cmd.Parameters.AddWithValue("zoom_level", level);
                                cmd.Parameters.AddWithValue("tile_column", col);
                                cmd.Parameters.AddWithValue("tile_row", row);
                                cmd.Parameters.AddWithValue("tile_id", guid);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        _convertingStatus.IsCommittingTransaction = true;
                        transaction.Commit();
                        _convertingStatus.IsCommittingTransaction = false;
                    }
                    finally
                    {
                        if (transaction != null)
                            transaction.Dispose();
                    }
                }
            }
        }
        protected override void CreateMBTilesFileAndWriteMetaData(string outputPath, string name, string description, string attribution, Geometry geometry)
        {
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
                        Point bottomLeft = Utility.WebMercatorToGeographic(new Point(geometry.Extent.XMin, geometry.Extent.YMin));
                        Point upperRight = Utility.WebMercatorToGeographic(new Point(geometry.Extent.XMax, geometry.Extent.YMax));
                        cmd.CommandText = "INSERT INTO metadata(name,value) VALUES ('bounds','" + string.Format("{0},{1},{2},{3}", bottomLeft.X.ToString(), bottomLeft.Y.ToString(), upperRight.X.ToString(), upperRight.Y.ToString()) + "')";
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
        public void CancelConverting()
        {
            CancelDoConvertToMBTiles();
        }
    }
}
