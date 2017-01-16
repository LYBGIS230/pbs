using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SQLite;
using PBS.Util;
using System.Collections;
using PBS.Service;
using System.Drawing;
using System.IO;
using System.Timers;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace PBS.DataSource
{
    public class DataSourceBaiDuMBTiles : DataSourceBase
    {
        public enum DataSourceType
        {
            STATIC,
            DYNAMIC
        }
        private DataSourceType serviceType;
        private SQLiteConnection _sqlConn;
        // 有网络情况下，对于实时路况和路况预测，还是可以使用百度在线的服务的
        private DataSourceBaiDuTileProxy baiDuTileProxy;
        public DataSourceBaiDuMBTiles(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                this.serviceType = DataSourceType.DYNAMIC;
            }
            else
            {
                this.serviceType = DataSourceType.STATIC;               
            }
            Initialize(path);
            baiDuTileProxy = new DataSourceBaiDuTileProxy("BaiDuOnline");
        }
        
        protected override void Initialize(string path)
        {
            if (serviceType == DataSourceType.STATIC)
            {
                _sqlConn = new SQLiteConnection("Data Source=" + path);
                _sqlConn.Open();
            }
            else
            {
                if(BaiDuMapManager.inst.cp == null)
                {
                    BaiDuMapManager.inst.cp = new CacheVersionProvider();
                }
            }
            this.Type = DataSourceTypePredefined.BaiduMBTiles.ToString();
            base.Initialize(path);
        }

        ~DataSourceBaiDuMBTiles()
        {
            if (_sqlConn != null)
                _sqlConn.Close();
            _sqlConn = null;
        }

        protected override void ReadTilingScheme(out TilingScheme tilingScheme)
        {
            //validate MBTile tile field
            if (serviceType == DataSourceType.STATIC)
            {
                using (SQLiteCommand sqlCmd = new SQLiteCommand("SELECT tile_data FROM tiles", _sqlConn))
                {
                    try
                    {
                        object o = sqlCmd.ExecuteScalar();
                    }
                    catch (Exception e)
                    {
                        throw new Exception("Selected file is not a valid MBTile file\r\n" + e.Message);
                    }
                }
            }
            ReadSqliteTilingScheme(out tilingScheme, _sqlConn);
            this.TilingScheme = TilingSchemePostProcess(tilingScheme); ;
        }

        protected override void ReadSqliteTilingScheme(out TilingScheme tilingScheme, SQLiteConnection sqlConn)
        {
            tilingScheme = new TilingScheme();
            StringBuilder sb;
            #region read MBTile tiling scheme
            tilingScheme.Path = "N/A";
            
            tilingScheme.CacheTileFormat = ImageFormat.PNG;

            
            //two extent
            if (serviceType == DataSourceType.STATIC)
            {
                using (SQLiteCommand sqlCmd = new SQLiteCommand(sqlConn))
                {
                    sqlCmd.CommandText = string.Format("SELECT value FROM metadata WHERE name='bounds'");//will raise exception if metadata table not exists
                    object o = sqlCmd.ExecuteScalar();//null can not directly convert to byte[], if so, will return "buffer can not be null" exception
                    if (o != null)
                    {
                        string[] bounds = o.ToString().Split(new char[] { ',' });
                        double xmin = double.Parse(bounds[0]);
                        double ymin = double.Parse(bounds[1]);
                        double xmax = double.Parse(bounds[2]);
                        double ymax = double.Parse(bounds[3]);
                        Util.Point pLeftTop = Utility.GeographicToWebMercator(new Util.Point(xmin, ymax));
                        Util.Point pRightBottom = Utility.GeographicToWebMercator(new Util.Point(xmax, ymin));
                        tilingScheme = SchemaProvider.Inst.getSchema("BaiDuOnline", null, null);
                        tilingScheme.InitialExtent = new Envelope(pLeftTop.X, pRightBottom.Y, pRightBottom.X, pLeftTop.Y);
                    }
                    else
                    {
                        throw new Exception();
                    }
                }
            }
            else
            {
                tilingScheme = BaiDuMapManager.inst.getBaiDuSchema(-180, 180, 85, -85);
            }

           
            #endregion
        }
        public override string generateSymbolText(int level, int row, int col)
        {
            return "Level/Row/Column(TMS)\r\n" + level + "/" + row + "/" + col;
        }
        private byte[] getPNG(int level, int row, int col)
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
            //return getModifiedTile(level, row, col, null);
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
        public void updateTileBytes()
        {
            byte[] dataTemp = new byte[12000];
            FileStream pngFile = new FileStream("19_28542_105664.png", FileMode.Open, FileAccess.Read);
            BinaryReader pngReader = new BinaryReader(pngFile);
            int actualLength = pngReader.Read(dataTemp, 0, 12000);
            byte[] actualData = new byte[actualLength];
            Buffer.BlockCopy(dataTemp, 0, actualData, 0, actualLength);

            SQLiteTransaction updateTr = _sqlConn.BeginTransaction();
            using (SQLiteCommand cmd = new SQLiteCommand(_sqlConn))
            {
                cmd.CommandText = "delete from images where tile_id = @tile_id";
                cmd.Parameters.AddWithValue("tile_id", "0a5ccc01-c683-4843-b404-ae81b005f23e");
                cmd.ExecuteNonQuery();
                cmd.CommandText = "INSERT INTO images VALUES (@tile_data,@tile_id)";
                cmd.Parameters.AddWithValue("tile_data", actualData);
                cmd.Parameters.AddWithValue("tile_id", "0a5ccc01-c683-4843-b404-ae81b005f23e");
                cmd.ExecuteNonQuery();
            }
            updateTr.Commit();
            updateTr.Dispose();
        }

        private string parseTrafficHisTime(string time)// input format: "{day:1,hour:11,time:1}"
        {
            var p = ParseJsonTime(time);

            long timeOffset;
            if ("1".Equals(p.time))
            {
                // 路况预测（取一周的历史作为预测）这个目前是跑不到的分支
                int requestDay = int.Parse(p.day);
                int requestHour = int.Parse(p.hour);
                DateTime now = DateTime.Now;
                int currentDayOfWeek = (int) now.DayOfWeek;
                DateTime timeInThisWeek = now.Add(
                    new TimeSpan(
                        requestDay - currentDayOfWeek, 
                        requestHour - now.Hour,
                        0 - now.Minute,
                        0 - now.Second,
                        0 - now.Millisecond
                        ));
                if (timeInThisWeek > now)
                {
                    DateTime targetTime = timeInThisWeek.Subtract(new TimeSpan(7, 0, 0, 0));
                    timeOffset = GetTimeInMs(targetTime);
                }
                else
                {
                    timeOffset = GetTimeInMs(timeInThisWeek);
                }
            }
            else
            {
                // 路况历史
                timeOffset = long.Parse(p.time);
            }
            
            return timeOffset.ToString();
        }

        private static DataSourceBaiDuTileProxy.TrafficHisParam ParseJsonTime(string time)
        {
            JavaScriptSerializer jss = new JavaScriptSerializer();
            DataSourceBaiDuTileProxy.TrafficHisParam p = jss.Deserialize<DataSourceBaiDuTileProxy.TrafficHisParam>(time);
            return p;
        }

        private long GetTimeInMs(DateTime t)
        {
            return (long)(t - new DateTime(1970, 1, 1)).TotalMilliseconds;
        }

        private string parseTrifficTime(string time)
        {
            DateTime d = BaiDuMapManager.inst.ConvertLongToDateTime(long.Parse(time));
            int milliseconds = 3600 * 1000 * d.Hour + 60 * 1000 * d.Minute + 1000 * d.Second + d.Millisecond;
            return milliseconds.ToString();
        }
        private string parseHotTime(string time)
        {
            return (long.Parse(time) % (3600 * 1000 * 24 * 7)).ToString();
        }
        public byte[] GetTileBytes(int level, int row, int col, object otherParam)
        {
            byte[] result = null;
            string time = "";
            Hashtable param = otherParam as Hashtable;
            string timeParam = param["TIME"] as string;
            // 预测和历史
            if ("TrafficHis".Equals(param["TYPE"]))
            {
                var p = ParseJsonTime(timeParam);
                if (p.time == "1")
                {
                    //预测
                    return baiDuTileProxy.GetTileBytes(level, row, col, otherParam);
                }
                else
                {
                    //历史
                    time = parseTrafficHisTime(timeParam);
                    using (NoCacheVersionProvider noCacheVersionProvider = new NoCacheVersionProvider("versionsT.db"))
                    {
                        string filename = noCacheVersionProvider.GetTileFile(long.Parse(time));
                        if (filename != null)
                        {
                            return GetTileFromFile(level, row, col, filename);
                        }
                        return null;
                    }
                }
            }
            //实时
            else if("traffic".Equals(param["TYPE"]))
            {
                return baiDuTileProxy.GetTileBytes(level, row, col, otherParam);
            }
            else if("hot".Equals(param["TYPE"] as string))
            {
                time = parseHotTime(timeParam);
            }
            string fileName = BaiDuMapManager.inst.cp.getCacheFile(time, param["TYPE"] as string);

            if(fileName != null){
                result = GetTileFromFile(level, row, col, fileName);
            }
            return result;
        }

        private static byte[] GetTileFromFile(int level, int row, int col, string fileName)
        {
            byte[] result = null;
            using (SQLiteConnection connection = new SQLiteConnection("Data source = " + fileName))
            {
                connection.Open();
                string commandText =
                    string.Format("SELECT {0} FROM tiles WHERE tile_column={1} AND tile_row={2} AND zoom_level={3}", "tile_data",
                        col, row, level);
                using (SQLiteCommand sqlCmd = new SQLiteCommand(commandText, connection))
                {
                    object o = sqlCmd.ExecuteScalar();
                        //null can not directly convert to byte[], if so, will return "buffer can not be null" exception
                    if (o != null)
                    {
                        result = (byte[]) o;
                    }
                }
                connection.Close();
            }
            return result;
        }
    }
}
