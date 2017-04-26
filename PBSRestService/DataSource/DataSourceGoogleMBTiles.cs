using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SQLite;
using PBS.Util;
using PBS.Service;
using System.Drawing;
using Point = PBS.Util.Point;
using System.IO;

namespace PBS.DataSource
{
    public class DataSourceGoogleMBTiles : DataSourceBase
    {
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

            return GoogleCoordCorrect(level, row, col);

        }

        #region GoogleCorrect

        const int PIC_SIZE = 256;
        const double initialResolution = 156543.03392804062;
        const double originShift = 20037508.3427892;
        const double pi = Math.PI;
        const int tileSize = 256;

        private byte[] GoogleCoordCorrect(int level, int row, int col)
        {
            // 调整行号,level、col不变，因坐标系起点不同
            int tmsCol, tmsRow;
            Utility.ConvertTMSTileToGoogleTile(level, row, col, out tmsRow, out tmsCol);

            // 7级及以下不进行偏移
            if (level < 8)
            {
                return GetOrigTileBuffer(level, tmsRow, tmsCol);
            }

            // get pixels from row and col
            LatLng p0 = new LatLng(PIC_SIZE * tmsRow, PIC_SIZE * tmsCol);
            LatLng p1 = new LatLng(PIC_SIZE * tmsRow + PIC_SIZE, PIC_SIZE * tmsCol + PIC_SIZE);

            LatLng m0 = PixelsToMeters(p0, level);
            LatLng m1 = PixelsToMeters(p1, level);

            LatLng l0 = MetersToLocation(m0);
            LatLng l1 = MetersToLocation(m1);

            LatLng L0 = DoOffset(l0);
            LatLng L1 = DoOffset(l1);

            LatLng M0 = LocationToMeters(L0);
            LatLng M1 = LocationToMeters(L1);

            LatLng P0 = MetersToPixels(M0, level);
            LatLng P1_1 = MetersToPixels(M1, level);
            LatLng P1 = new LatLng(P1_1.latitude - 1, P1_1.longitude - 1);

            return GetModifiedTile(P0, P1, level);
        }

        private byte[] GetOrigTileBuffer(int level, int row, int col)
        {
            string commandText = string.Format("SELECT {0} FROM tiles WHERE tile_column={1} AND tile_row={2} AND zoom_level={3}", "tile_data", col, row, level);
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

        private byte[] GetModifiedTile(LatLng P0, LatLng P1, int level)
        {
            // 数组T中，0代表经度相关，1代表纬度相关
            int[] T0 = PixelsToTile(P0);
            int[] T1 = PixelsToTile(P1);

            if (T0[0] == T1[0] && T0[1] == T1[1])
            {
                //原封不动的Tile
                return GetOrigTileBuffer(level, T0[1], T0[0]);
            }

            // 需要重新截取的
            //0. 计算大小
            int width = 256 * (T1[0] - T0[0] + 1);
            int height = 256 * (T1[1] - T0[1] + 1);

            //1.获取图片
            Bitmap canvas = new Bitmap(width,height);
            Graphics pics = Graphics.FromImage(canvas);
            pics.Clear(Color.Transparent);
            bool bimage = false;

            //2.拼接
            for (int x = 0; x < width / 256; ++x)
            {
                for (int y = 0; y < height / 256; ++y)
                {
                    byte[] obj = GetOrigTileBuffer(level, T0[1] + y, T0[0] + x);
                    if (obj != null)
                    {
                        bimage = true;
                        pics.DrawImage(new Bitmap(new MemoryStream(obj)), x * 256, height - y * 256 - 256);
                    }
                }
            }

            if (!bimage)
            {
                return null;
            }

            //3.截图
            int x0 = (int)Math.Ceiling((P0.longitude - T0[0] * 256.0));
            int y0 = (int)Math.Ceiling(256.0 - (P1.latitude - T1[1] * 256.0));
            
            int width0 = (int)Math.Ceiling(P1.longitude - P0.longitude);
            int height0 = (int)Math.Ceiling(P1.latitude - P0.latitude);

            Bitmap destBitmap =
                canvas.Clone(new Rectangle(x0, y0, width0, height0), canvas.PixelFormat);
            MemoryStream output = new MemoryStream();
            destBitmap.Save(output, System.Drawing.Imaging.ImageFormat.Png);
            byte[] bytes = output.GetBuffer();
            return bytes;
        }

        private double GetResolution(int level)
        {
            return initialResolution / Math.Pow(2.0, level);
        }

        private LatLng PixelsToMeters(LatLng loc, int level)
        {
            LatLng result =
                new LatLng(loc.latitude * GetResolution(level) - originShift, loc.longitude * GetResolution(level) - originShift);
            return result;
        }

        private LatLng MetersToPixels(LatLng loc, int level)
        {
            return new LatLng((loc.latitude + originShift) / GetResolution(level), (loc.longitude + originShift) / GetResolution(level));
        }

        private LatLng MetersToLocation(LatLng loc)
        {
            //"Converts XY point from Spherical Mercator EPSG:900913 to lat/lon in WGS84 Datum"
            double lon = (loc.longitude / originShift) * 180.0;
            double lat = (loc.latitude / originShift) * 180.0;

            lat = 180 / pi * (2 * Math.Atan(Math.Exp(lat * pi / 180.0)) - pi / 2.0);
            return new LatLng(lat, lon);
        }

        private LatLng LocationToMeters(LatLng loc)
        {
            //"Converts given lat/lon in WGS84 Datum to XY in Spherical Mercator EPSG:900913"
            double lon = loc.longitude * originShift / 180.0;
            double lat = Math.Log(Math.Tan((90 + loc.latitude) * pi / 360.0)) / (pi / 180.0);

            lat = lat * originShift / 180.0;
            return new LatLng(lat, lon);
        }

        private int[] PixelsToTile(LatLng loc)
        {
            int[] obj =
            {(int)Math.Ceiling(loc.longitude / (float)tileSize) - 1, (int)Math.Ceiling(loc.latitude / (float)tileSize) - 1};
            
            return obj;
        }

        private LatLng DoOffset(LatLng loc)
        {
            Point p = GoogleDoOffset.transform(loc.longitude, loc.latitude);
            return new LatLng(p.Y, p.X);
        }
        #endregion

    }


    #region 谷歌偏移算法

    public class GoogleDoOffset
    {
        private static double pi = 3.14159265358979324;

        private static double a = 6378245.0;

        private static double ee = 0.00669342162296594323;

        public static PBS.Util.Point transform(double wgLon, double wgLat)
        {
            if (outOfChina(wgLat, wgLon))
            {

                return new Point(wgLat, wgLon);

            }

            double dLat = transformLat(wgLon - 105.0, wgLat - 35.0);

            double dLon = transformLon(wgLon - 105.0, wgLat - 35.0);

            double radLat = wgLat / 180.0 * pi;

            double magic = Math.Sin(radLat);

            magic = 1 - ee * magic * magic;

            double sqrtMagic = Math.Sqrt(magic);

            dLat = (dLat * 180.0) / ((a * (1 - ee)) / (magic * sqrtMagic) * pi);

            dLon = (dLon * 180.0) / (a / sqrtMagic * Math.Cos(radLat) * pi);

            return new Point((double)(wgLon + dLon), (double)(wgLat + dLat));
        }

        private static bool outOfChina(double lat, double lon)
        {

            if (lon < 72.004 || lon > 137.8347)

                return true;

            if (lat < 0.8293 || lat > 55.8271)

                return true;

            return false;

        }

        private static double transformLat(double x, double y)
        {

            double ret = -100.0 + 2.0 * x + 3.0 * y + 0.2 * y * y + 0.1 * x * y + 0.2 * Math.Sqrt(Math.Abs(x));

            ret += (20.0 * Math.Sin(6.0 * x * pi) + 20.0 * Math.Sin(2.0 * x * pi)) * 2.0 / 3.0;

            ret += (20.0 * Math.Sin(y * pi) + 40.0 * Math.Sin(y / 3.0 * pi)) * 2.0 / 3.0;

            ret += (160.0 * Math.Sin(y / 12.0 * pi) + 320 * Math.Sin(y * pi / 30.0)) * 2.0 / 3.0;

            return ret;

        }

        private static double transformLon(double x, double y)
        {

            double ret = 300.0 + x + 2.0 * y + 0.1 * x * x + 0.1 * x * y + 0.1 * Math.Sqrt(Math.Abs(x));

            ret += (20.0 * Math.Sin(6.0 * x * pi) + 20.0 * Math.Sin(2.0 * x * pi)) * 2.0 / 3.0;

            ret += (20.0 * Math.Sin(x * pi) + 40.0 * Math.Sin(x / 3.0 * pi)) * 2.0 / 3.0;

            ret += (150.0 * Math.Sin(x / 12.0 * pi) + 300.0 * Math.Sin(x / 30.0 * pi)) * 2.0 / 3.0;

            return ret;

        }
    }

    #endregion
}
