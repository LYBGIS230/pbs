using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using PBS.DataSource;


namespace PBS.Service
{
    public class LatLng
    {
        public LatLng(double _latitude, double _longitude)
        {
            latitude = _latitude;
            longitude = _longitude;
        }

        public double latitude { get; set; }
        public double longitude { get; set; }
    }
    public class BdCoodOffsetProvider
    {
        double ee = 0.00669342162296594323;
        double x_pi = 3.14159265358979324 * 3000.0 / 180.0;
        double a = 6378245.0;
        double r = 6371393;
        public static BdCoodOffsetProvider inst;
        TilingScheme baiduSchema;
        public static BdCoodOffsetProvider getInstance()
        {
            if(inst == null)
            {
                inst = new BdCoodOffsetProvider();
            }
            return inst;
        }
        private BdCoodOffsetProvider()
        {
            baiduSchema = BaiDuMapManager.inst.getBaiDuSchema();
            inst = this;
        }
        public LatLng doAdjust(LatLng o, int zoom)
        {
            if (zoom < 8)
            {
                return o;
            }
            double resolution = baiduSchema.LODs[zoom].Resolution;
            LatLng bd = transformFromGCJToBD(transformFromWGSToGCJ(o));
            return bd;
        }
        //gps转gcj（例如Google、高德坐标）
        public PBS.Util.Point getOffSet(LatLngPoint o, int zoom)
        {
            if(zoom < 8)
            {
                return new Util.Point(0, 0);
            }
            LatLng orgin = new LatLng(o.Lat, o.Lng);
            double resolution = baiduSchema.LODs[zoom].Resolution;
            LatLng bd = transformFromGCJToBD(transformFromWGSToGCJ(orgin));
            double lonBd = bd.longitude;
            double latBd = bd.latitude;
            double intervalLon = lonBd - orgin.longitude;
            double intervalLat = latBd - orgin.latitude;
            double pixelIntervalLon = Math.PI * r * intervalLon / 180 / resolution;
            double tan = Math.Tan(orgin.latitude * Math.PI / 180);
            double pixelIntervalLat = r * (1 + tan * tan) / resolution * (intervalLat * Math.PI / 180);
            return new Util.Point(pixelIntervalLon, pixelIntervalLat);
        }
        //gcj转百度坐标
        public LatLng transformFromGCJToBD(LatLng gcjLoc)
        {
            double x = gcjLoc.longitude, y = gcjLoc.latitude;
            double z = Math.Sqrt(x * x + y * y) + 0.00002 * Math.Sin(y * x_pi);
            double theta = Math.Atan2(y, x) + 0.000003 * Math.Cos(x * x_pi);
            return new LatLng(z * Math.Sin(theta) + 0.006, z * Math.Cos(theta) + 0.0065);
        }
        public LatLng transformFromWGSToGCJ(LatLng wgLoc)
        {

            //如果在国外，则默认不进行转换
            if (outOfChina(wgLoc.latitude, wgLoc.longitude))
            {
                return new LatLng(wgLoc.latitude, wgLoc.longitude);
            }
            double dLat = transformLat(wgLoc.longitude - 105.0,
                            wgLoc.latitude - 35.0);
            double dLon = transformLon(wgLoc.longitude - 105.0,
                            wgLoc.latitude - 35.0);
            double radLat = wgLoc.latitude / 180.0 * Math.PI;
            double magic = Math.Sin(radLat);
            magic = 1 - ee * magic * magic;
            double sqrtMagic = Math.Sqrt(magic);
            dLat = (dLat * 180.0) / ((a * (1 - ee)) / (magic * sqrtMagic) * Math.PI);
            dLon = (dLon * 180.0) / (a / sqrtMagic * Math.Cos(radLat) * Math.PI);

            return new LatLng(wgLoc.latitude + dLat, wgLoc.longitude + dLon);
        }
        public Boolean outOfChina(double lat, double lon)
        {
            if (lon < 72.004 || lon > 137.8347)
                return true;
            if (lat < 0.8293 || lat > 55.8271)
                return true;
            return false;
        }
        public double transformLat(double x, double y)
        {
            double ret = -100.0 + 2.0 * x + 3.0 * y + 0.2 * y * y + 0.1 * x * y
                            + 0.2 * Math.Sqrt(x > 0 ? x : -x);
            ret += (20.0 * Math.Sin(6.0 * x * Math.PI) + 20.0 * Math.Sin(2.0 * x
                            * Math.PI)) * 2.0 / 3.0;
            ret += (20.0 * Math.Sin(y * Math.PI) + 40.0 * Math.Sin(y / 3.0
                            * Math.PI)) * 2.0 / 3.0;
            ret += (160.0 * Math.Sin(y / 12.0 * Math.PI) + 320 * Math.Sin(y
                            * Math.PI / 30.0)) * 2.0 / 3.0;
            return ret;
        }
        public double transformLon(double x, double y)
        {
            double ret = 300.0 + x + 2.0 * y + 0.1 * x * x + 0.1 * x * y + 0.1
                            * Math.Sqrt(x > 0 ? x : -x);
            ret += (20.0 * Math.Sin(6.0 * x * Math.PI) + 20.0 * Math.Sin(2.0 * x
                            * Math.PI)) * 2.0 / 3.0;
            ret += (20.0 * Math.Sin(x * Math.PI) + 40.0 * Math.Sin(x / 3.0
                            * Math.PI)) * 2.0 / 3.0;
            ret += (150.0 * Math.Sin(x / 12.0 * Math.PI) + 300.0 * Math.Sin(x
                            / 30.0 * Math.PI)) * 2.0 / 3.0;
            return ret;
        }
    }

}
