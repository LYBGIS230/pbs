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
        private static double[] array1 = { 75, 60, 45, 30, 15, 0 };
        private static double[][] array2 = {new double[]{-0.0015702102444
, 111320.7020616939, 1704480524535203, -10338987376042340, 26112667856603880, -35149669176653700, 26595700718403920, -10725012454188240, 1800819912950474, 82.5}
                                               ,new double[]{0.0008277824516172526, 111320.7020463578, 647795574.6671607, -4082003173.641316, 10774905663.51142, -15171875531.51559, 12053065338.62167, -5124939663.577472, 913311935.9512032, 67.5}
                                               ,new double[]{0.00337398766765, 111320.7020202162, 4481351.045890365, -23393751.19931662, 79682215.47186455, -115964993.2797253, 97236711.15602145,
-43661946.33752821, 8477230.501135234, 52.5}
                                               ,new double[]{0.00220636496208, 111320.7020209128, 51751.86112841131, 3796837.749470245, 992013.7397791013, -1221952.21711287, 1340652.697009075, -620943.6990984312, 144416.9293806241, 37.5}
                                               ,new double[]{-0.0003441963504368392, 111320.7020576856, 278.2353980772752, 2485758.690035394, 6070.750963243378, 54821.18345352118, 9540.606633304236, -2710.55326746645, 1405.483844121726, 22.5}
                                               ,new double[]{-0.0003218135878613132, 111320.7020701615, 0.00369383431289, 823725.6402795718, 0.46104986909093, 2351.343141331292, 1.58060784298199,
8.77738589078284, 0.37238884252424, 7.45}};
        private static double[] array3 = { 12890594.86, 8362377.87, 5591021, 3481989.83, 1678043.12, 0 };
        private static double[][] array4 = {new double[]{1.410526172116255e-8, 0.00000898305509648872, -1.9939833816331, 200.9824383106796
, -187.2403703815547, 91.6087516669843, -23.38765649603339, 2.57121317296198, -0.03801003308653, 17337981.2}
                                               ,new double[]{-7.435856389565537e-9, 0.000008983055097726239, -0.78625201886289, 96.32687599759846, -1.85204757529826, -59.36935905485877, 47.40033549296737, -16.50741931063887, 2.28786674699375, 10260144.86}
                                               ,new double[]{-3.030883460898826e-8, 0.00000898305509983578, 0.30071316287616, 59.74293618442277, 7.357984074871, -25.38371002664745, 13.45380521110908, -3.29883767235584, 0.32710905363475, 6856817.37}
                                               ,new double[]{-1.981981304930552e-8, 0.000008983055099779535, 0.03278182852591, 40.31678527705744, 0.65659298677277, -4.44255534477492, 0.85341911805263, 0.12923347998204, -0.04625736007561, 4482777.06}
                                               ,new double[]{3.09191371068437e-9, 0.000008983055096812155, 0.00006995724062, 23.10934304144901, -0.00023663490511, -0.6321817810242, -0.00663494467273, 0.03430082397953, -0.00466043876332, 2555164.4}
                                               ,new double[]{2.890871144776878e-9, 0.000008983055095805407, -3.068298e-8, 7.47137025468032, -0.00000353937994, -0.02145144861037, -0.00001234426596
, 0.00010322952773, -0.00000323890364, 826088.5}};
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

#region google-correct
        public LatLng doGoogleCorrect(LatLng o, int zoom)
        {
            LatLng google = transformFromWGSToGCJ(o);
            return google;
        }
#endregion

        //gcj转百度坐标
        public LatLng transformFromGCJToBD(LatLng gcjLoc)
        {
            double x = gcjLoc.longitude, y = gcjLoc.latitude;
            double z = Math.Sqrt(x * x + y * y) + 0.00002 * Math.Sin(y * x_pi);
            double theta = Math.Atan2(y, x) + 0.000003 * Math.Cos(x * x_pi);
            return new LatLng(z * Math.Sin(theta) + 0.006, z * Math.Cos(theta) + 0.0065);
        }
        public LatLng transformFromBDToGCJ(LatLng bdLoc)
        {
            double x = bdLoc.longitude - 0.0065, y = bdLoc.latitude - 0.006;
            double z = Math.Sqrt(x * x + y * y) - 0.00002 * Math.Sin(y * x_pi);
            double theta = Math.Atan2(y, x) - 0.000003 * Math.Cos(x * x_pi);
            return new LatLng(z * Math.Sin(theta), z * Math.Cos(theta));
        }
        public LatLng StandardGPS2BaiduGPS(LatLng o)
        {
            LatLng bd =
                transformFromGCJToBD(
                    transformFromWGSToGCJ(
                        o
                    )
                );
            return bd;
        }
        public static LatLng BaiduGPS2BaiduMercator(LatLng p)
        {
            double[] arr = null;
            for (var i = 0; i < array1.Length; i++)
            {
                if (p.latitude >= array1[i])
                {
                    arr = array2[i];
                    break;
                }
            }
            if (arr == null)
            {
                for (var i = array1.Length - 1; i >= 0; i--)
                {
                    if (p.latitude <= -array1[i])
                    {
                        arr = array2[i];
                        break;
                    }
                }
            }
            double[] res = BaiDuMapManager.Convertor(p.longitude, p.latitude, arr);
            return new LatLng(res[1], res[0]);
        }
        private LatLng BaiduMercator2BaiduGPS(LatLng p)
        {
            double[] arr = null;
            LatLng np = new LatLng(Math.Abs(p.latitude), Math.Abs(p.longitude));
            for (var i = 0; i < array3.Length; i++)
            {
                if (np.latitude >= array3[i])
                {
                    arr = array4[i];
                    break;
                }
            }
            double[] res = BaiDuMapManager.Convertor(np.longitude, np.latitude, arr);
            return new LatLng(res[1], res[0]);
        }
        public static LatLng WebMercatorToGPS(LatLng p)
        {
            double originShift = 2 * Math.PI * 6378137 / 2.0;
            double lon = (p.longitude / originShift) * 180.0;
            double lat = (p.latitude / originShift) * 180.0;
            lat = 180 / Math.PI * (2 * Math.Atan(Math.Exp(lat * Math.PI / 180.0)) - Math.PI / 2.0);
            return new LatLng(lat, lon);
        }
        public static LatLng GPSToWebMercator(LatLng p)
        {
            double x = p.longitude;
            double y = p.latitude;
            if ((y < -90.0) || (y > 90.0))
            {
                throw new ArgumentException("Point does not fall within a valid range of a geographic coordinate system.");
            }
            double num = x * 0.017453292519943295;
            double xx = 6378137.0 * num;
            double a = y * 0.017453292519943295;
            return new LatLng(3189068.5 * Math.Log((1.0 + Math.Sin(a)) / (1.0 - Math.Sin(a))), xx);
        }
        public LatLng BaiduGPS2StandardGPS(LatLng o)
        {
            LatLng gcj = transformFromGCJToWGS(transformFromBDToGCJ(o));
            return gcj;
        }
        public LatLng BaiduMercator2StandardMercator(LatLng o)
        {
            LatLng baiduGPS = BaiduMercator2BaiduGPS(o);
            LatLng standardGPS = transformFromGCJToWGS(transformFromBDToGCJ(baiduGPS));
            LatLng standardMercator = GPSToWebMercator(standardGPS);
            return standardMercator;
        }


        public LatLng StandardMercator2BaiduMercator(LatLng o)
        {
            LatLng standardGPS = WebMercatorToGPS(o);
            LatLng baiduGPS = StandardGPS2BaiduGPS(standardGPS);
            LatLng baiduMercator = BaiduGPS2BaiduMercator(baiduGPS);
            return baiduMercator;
        }
        public LatLng transformFromGCJToWGS(LatLng gcjLoc)
        {
            LatLng gPt = transformFromWGSToGCJ(gcjLoc);
            double dLon = gPt.longitude - gcjLoc.longitude;
            double dLat = gPt.latitude - gcjLoc.latitude;
            return new LatLng(gcjLoc.latitude - dLat, gcjLoc.longitude - dLon);
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
