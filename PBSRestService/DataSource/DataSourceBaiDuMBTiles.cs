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
        public DataSourceBaiDuMBTiles(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                this.serviceType = DataSourceType.DYNAMIC;
                /*if (BaiDuMapManager.inst.cp == null)
                {
                    BaiDuMapManager.inst.cp = new CacheVersionProvider();
                }*/
            }
            else
            {
                this.serviceType = DataSourceType.STATIC;               
            }
            Initialize(path);
        }

        protected override void Initialize(string path)
        {
            if (serviceType == DataSourceType.STATIC)
            {
                _sqlConn = new SQLiteConnection("Data Source=" + path);
                _sqlConn.Open();
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
        public byte[] GetTileBytes(int level, int row, int col, object otherParam)
        {
            byte[] result = null;
            Hashtable param = otherParam as Hashtable;
            string time = param["TIME"] as string;
            string fileName = BaiDuMapManager.inst.cp.getCacheFile(time);
            if(fileName != null){
                using (SQLiteConnection connection = new SQLiteConnection("Data source = cache/" + fileName))
                {
                    connection.Open();
                    if (connection == null)
                    {
                        result = new byte[0];
                    }
                    else
                    {
                        string commandText = string.Format("SELECT {0} FROM tiles WHERE tile_column={1} AND tile_row={2} AND zoom_level={3}", "tile_data", col, row, level);
                        using (SQLiteCommand sqlCmd = new SQLiteCommand(commandText, connection))
                        {
                            object o = sqlCmd.ExecuteScalar();//null can not directly convert to byte[], if so, will return "buffer can not be null" exception
                            if (o != null)
                            {
                                result = (byte[])o;
                            }
                        }
                        connection.Close();
                    }
                }
            }
            return result;
        }
    }
}
