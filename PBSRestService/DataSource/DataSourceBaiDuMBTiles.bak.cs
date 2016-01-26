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
        private BdCoodOffsetProvider p;
        public DataSourceBaiDuMBTiles(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                this.serviceType = DataSourceType.DYNAMIC;
                if (BaiDuMapManager.inst.cp == null)
                {
                    BaiDuMapManager.inst.cp = new CacheVersionProvider();
                }
            }
            else
            {
                this.serviceType = DataSourceType.STATIC;               
            }
            Initialize(path);
            p = new BdCoodOffsetProvider();
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
                        tilingScheme = BaiDuMapManager.inst.getBaiDuSchema(double.Parse(bounds[0]), double.Parse(bounds[2]), double.Parse(bounds[3]), double.Parse(bounds[1]));                      
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
        private System.Drawing.Point getNewPixelOffsetOfLeftTop(int level, int row1, int col1)
        {
            System.Drawing.Point org_leftBottom = BaiDuMapManager.inst.getMetersFromRowCol(row1, col1, level);
            LatLng lpt = BaiDuMapManager.inst.MetersToLatLon(org_leftBottom);
            LatLng testPoint = p.doAdjust(lpt, level);
            PointF meters = BaiDuMapManager.inst.LatLng2Mercator(new LatLngPoint(testPoint.longitude, testPoint.latitude));
            System.Drawing.Point new_leftBottom = new System.Drawing.Point((int)(meters.X / BaiDuMapManager.inst.resolution), (int)(meters.Y / BaiDuMapManager.inst.resolution));
            return new_leftBottom;
        }
        public byte[] getModifiedTile(int level, int row1, int col1, StreamWriter s)
        {
            int PIC_SIZE = 256;
            BdCoodOffsetProvider p = new BdCoodOffsetProvider();
            System.Drawing.Point org_leftTop, new_leftTop, org_leftBottom, new_leftBottom, org_rightTop, new_rightTop, org_rightBottom, new_rightBottom;

            //System.Drawing.Point leftTop = getNewPixelOffsetOfLeftTop(level, row1, col1);
            //System.Drawing.Point leftBottom = getNewPixelOffsetOfLeftTop(level, row1 + 1, col1);
            //System.Drawing.Point rightTop = getNewPixelOffsetOfLeftTop(level, row1, col1 + 1);
            //System.Drawing.Point rightBottom = getNewPixelOffsetOfLeftTop(level, row1 + 1, col1 + 1);

            //calc pixel offset of left bottom point in the tile
            org_leftTop = BaiDuMapManager.inst.getMetersFromRowCol(row1, col1, level);
            LatLng lpt = BaiDuMapManager.inst.MetersToLatLon(org_leftTop);
            LatLng testPoint = p.doAdjust(lpt, level);
            PointF meters = BaiDuMapManager.inst.LatLng2Mercator(new LatLngPoint(testPoint.longitude, testPoint.latitude));
            new_leftTop = new System.Drawing.Point((int)(meters.X / BaiDuMapManager.inst.resolution), (int)(meters.Y / BaiDuMapManager.inst.resolution));

            org_rightBottom = BaiDuMapManager.inst.getMetersFromRowCol(row1 +1, col1 + 1, level);
            lpt = BaiDuMapManager.inst.MetersToLatLon(org_rightBottom);
            testPoint = p.doAdjust(lpt, level);
            meters = BaiDuMapManager.inst.LatLng2Mercator(new LatLngPoint(testPoint.longitude, testPoint.latitude));
            new_rightBottom = new System.Drawing.Point((int)(meters.X / BaiDuMapManager.inst.resolution) - 1, (int)(meters.Y / BaiDuMapManager.inst.resolution) + 1);

            org_leftBottom = BaiDuMapManager.inst.getMetersFromRowCol(row1 + 1, col1, level);
            lpt = BaiDuMapManager.inst.MetersToLatLon(org_leftBottom);
            testPoint = p.doAdjust(lpt, level);
            meters = BaiDuMapManager.inst.LatLng2Mercator(new LatLngPoint(testPoint.longitude, testPoint.latitude));
            new_leftBottom = new System.Drawing.Point((int)(meters.X / BaiDuMapManager.inst.resolution), (int)(meters.Y / BaiDuMapManager.inst.resolution) + 1);

            org_rightTop = BaiDuMapManager.inst.getMetersFromRowCol(row1, col1 + 1, level);
            lpt = BaiDuMapManager.inst.MetersToLatLon(org_rightTop);
            testPoint = p.doAdjust(lpt, level);
            meters = BaiDuMapManager.inst.LatLng2Mercator(new LatLngPoint(testPoint.longitude, testPoint.latitude));
            new_rightTop = new System.Drawing.Point((int)(meters.X / BaiDuMapManager.inst.resolution) - 1, (int)(meters.Y / BaiDuMapManager.inst.resolution));

            int newMaxRow = new_leftTop.Y / PIC_SIZE;
            int newMinCol = new_leftTop.X / PIC_SIZE;
            int rowOffSetStart = PIC_SIZE - new_leftTop.Y % PIC_SIZE - 1;
            int colOffSetStart = new_leftTop.X % PIC_SIZE;

            int newMinRow = new_rightBottom.Y / PIC_SIZE;
            int newMaxCol = new_rightBottom.X / PIC_SIZE;
            int rowOffSetEnd = PIC_SIZE - new_rightBottom.Y % PIC_SIZE - 1;
            int colOffSetEnd = new_rightBottom.X % PIC_SIZE;


            if (s != null)
           {
               s.WriteLine("" + col1 + "\t" + row1 + "\t" + new_leftTop.X + "\t"  + new_leftTop.Y);
           }
            int colSpan = newMaxCol - newMinCol + 1;
            int rowSpan = newMaxRow - newMinRow + 1;
            Bitmap canvas = new Bitmap(colSpan * PIC_SIZE, rowSpan * PIC_SIZE);
            Graphics pics = Graphics.FromImage(canvas);
            pics.Clear(Color.Transparent);
            for (int x = 0; x < colSpan; x++)
            {
                for (int y = 0; y < rowSpan; y++)
                {
                    byte[] picStream = getPNG(level, newMinRow + y, newMinCol + x);
                    if (picStream != null)
                    {
                        pics.DrawImage(new Bitmap(new MemoryStream(picStream)), x * PIC_SIZE, (rowSpan - y - 1) * PIC_SIZE, PIC_SIZE, PIC_SIZE);
                    }
                }
            }

            Bitmap destBitmap = new Bitmap(PIC_SIZE, PIC_SIZE);
            Graphics destPic = Graphics.FromImage(destBitmap);
            Rectangle destRect = new Rectangle(0, 0, PIC_SIZE, PIC_SIZE);//矩形容器 
            Rectangle srcRect = new Rectangle(colOffSetStart, rowOffSetStart, new_rightBottom.X - new_leftTop.X + 1, new_leftTop.Y - new_rightBottom.Y +1);
            destPic.DrawImage(canvas, destRect, srcRect, GraphicsUnit.Pixel);
            MemoryStream output = new MemoryStream();
            destBitmap.Save(output, System.Drawing.Imaging.ImageFormat.Png);
            return output.GetBuffer();
        }

        public override byte[] GetTileBytes(int level, int row, int col)
        {
            return getModifiedTile(level, row, col, null);
            /*string commandText = string.Format("SELECT {0} FROM tiles WHERE tile_column={1} AND tile_row={2} AND zoom_level={3}", "tile_data", col, row, level);
            using (SQLiteCommand sqlCmd = new SQLiteCommand(commandText, _sqlConn))
            {
                object o = sqlCmd.ExecuteScalar();//null can not directly convert to byte[], if so, will return "buffer can not be null" exception
                if (o != null)
                {
                    return (byte[])o;
                }
                return null;
            }*/
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
