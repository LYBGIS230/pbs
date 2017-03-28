using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SQLite;
using PBS.Util;
using PBS.Service;

namespace PBS.DataSource
{
    public class DataSourceGoogleMBTiles : DataSourceBase
    {
        const int PIC_SIZE = 256;

        private SQLiteConnection _sqlConn;

        public DataSourceGoogleMBTiles(string path)
        {
            Initialize(path);
        }

        protected override void Initialize(string path)
        {
            this.Type = DataSourceTypePredefined.GoogleMBTiles.ToString();
            _sqlConn = new SQLiteConnection("Data Source=" + path);
            _sqlConn.Open();
            base.Initialize(path);
        }

        ~DataSourceGoogleMBTiles()
        {
            if (_sqlConn != null)
                _sqlConn.Close();
            _sqlConn = null;
        }

        protected override void ReadTilingScheme(out TilingScheme tilingScheme)
        {
            //validate MBTile tile field
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
            ReadSqliteTilingScheme(out tilingScheme, _sqlConn);
            this.TilingScheme = TilingSchemePostProcess(tilingScheme); ;
        }

        public override byte[] GetTileBytes(int level, int row, int col)
        {
            int tmsCol, tmsRow;
            Utility.ConvertGoogleTileToTMSTile(level, row, col, out tmsRow, out tmsCol);
            string commandText = string.Format("SELECT {0} FROM tiles WHERE tile_column={1} AND tile_row={2} AND zoom_level={3}", "tile_data", tmsCol, tmsRow, level);
            using (SQLiteCommand sqlCmd = new SQLiteCommand(commandText, _sqlConn))
            {
                object o = sqlCmd.ExecuteScalar();//null can not directly convert to byte[], if so, will return "buffer can not be null" exception
                if (o != null)
                {
                    byte[] bytes = (byte[])o;

                    return bytes;
                }
                return null;
            }
        }

        private byte[] GoogleCoordCorrect(int level, int row, int col)
        {
            // 7级及以下不进行偏移
            if (level < 8)
            {
                return null;
            }

            // 调整行列号,level、row不变，因坐标系起点不同
            int tmsCol, tmsRow;
            Utility.ConvertTMSTileToGoogleTile(level, row, col, out tmsRow, out tmsCol);

            LatLng p0 = new LatLng(PIC_SIZE * tmsRow, PIC_SIZE * tmsCol);
            LatLng p1 = new LatLng(PIC_SIZE * tmsRow + PIC_SIZE, PIC_SIZE * tmsCol + PIC_SIZE);



            return null;
        }
    }



}
