using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PBS.Util;
using System.Collections;
using PBS.Service;
using System.Web.Script.Serialization;
using System.Timers;


namespace PBS.DataSource
{
    public class DataSourceBaiDuTileProxy : DataSourceBase
    {
        private string _baseUrl;
        private string _mapName;
        public string SubType
        {
            set
            {
                _mapName = value;
            }
        }
        private  List<CustomOnlineMap> _customOnlineMaps;
        public Dictionary<string, string> baseUrls; 
        public List<CustomOnlineMap> CustomOnlineMaps
        {
            get
            {
                if (_customOnlineMaps == null)
                    _customOnlineMaps = BaiDuMapManager.inst.ReadOnlineMapsConfigFile("BaiduMaps.xml");
                return _customOnlineMaps;
            }
        }
        private string[] _subDomains;
        private CacheVersionProvider.MBVersion buildVersionInfo(int version, long start, long end)
        {
            CacheVersionProvider.MBVersion result = new CacheVersionProvider.MBVersion()
            {
                version = version,
                start = start,
                end = end,
                name = "proxy-non",
                threadCount = 0,
                status = "",
                timeSpan = 1,
                completeCount = 1,
                totalCount = 1,
                receivedBytes = 1,
                wroteBytes = 1,
                networkSpeed = 1,
                wroteCounts = 1
            };
            return result;
        }
        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            if (BaiDuMapManager.inst.RunMode == "ONLINE")
            {
                int version = 0;

                if (CacheVersionProvider.arrangedVersion == 0)
                {
                    BaiDuMapManager.inst.cp.initVersionFormWeb();
                }
                else
                {
                    version = Convert.ToInt32(BaiDuMapManager.inst.cp.getCurrentVersion());
                }
                
                if (version > 0)
                {
                    if (version > CacheVersionProvider.arrangedVersion)
                    {
                        long start = BaiDuMapManager.inst.ConvertDateTimeLong(DateTime.Now);
                        CacheVersionProvider.MBVersion newOne = buildVersionInfo(version, start, start + 300 * 60000);
                        BaiDuMapManager.inst.cp.RecordDownloadRecord(newOne, "BYTIME");

                        int currentVerison = CacheVersionProvider.arrangedVersion;
                        int endVerison = version - 1;
                        while(currentVerison != endVerison + 1)
                        {
                            newOne = buildVersionInfo(currentVerison, 100, 200);
                            BaiDuMapManager.inst.cp.RecordDownloadRecord(newOne, "BYROUND");
                            currentVerison = currentVerison + 1;
                        }
                        CacheVersionProvider.arrangedVersion = version + 1;
                    }
                    else if (version == CacheVersionProvider.arrangedVersion)
                    {
                        long start = BaiDuMapManager.inst.ConvertDateTimeLong(DateTime.Now);
                        CacheVersionProvider.MBVersion newOne = buildVersionInfo(version, start, start + 300 * 60000);
                        BaiDuMapManager.inst.cp.RecordDownloadRecord(newOne, "BYTIME");
                        CacheVersionProvider.arrangedVersion = CacheVersionProvider.arrangedVersion + 1;
                    }
                    else if(version < CacheVersionProvider.arrangedVersion - 1)
                    {
                        BaiDuMapManager.inst.cp.resetVersions();
                        OnTimedEvent(null, null);
                    }
                }

            }
        }
        public void startVersionRepository()
        {
            if (BaiDuMapManager.inst.cp == null)
            {
                BaiDuMapManager.inst.cp = new CacheVersionProvider();
            }
            Timer t = new Timer(BaiDuMapManager.inst.roundInterval);
            t.Elapsed += new System.Timers.ElapsedEventHandler(OnTimedEvent);
            t.AutoReset = true;
            if (BaiDuMapManager.inst.RunMode == "ONLINE")
            {
                BaiDuMapManager.inst.cp.initVersionFormWeb();
                t.Enabled = true;

            }
            OnTimedEvent(null, null);
        }
        public DataSourceBaiDuTileProxy(string name)
        {
            TilingScheme ts;
            ReadTilingScheme(out ts);
            TilingScheme = ts;
            _subDomains = new string[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };           
            baseUrls = new Dictionary<string, string>();
            string urlConfigFile = SchemaProvider.Inst.getConfigPath(name);
            _customOnlineMaps = BaiDuMapManager.inst.ReadOnlineMapsConfigFile(urlConfigFile);
            foreach (CustomOnlineMap m in _customOnlineMaps)
            {
                baseUrls.Add(m.Name, m.Url);
            }
            this.Type = name;
            this.Path = "";
        }
        private class TrafficHisParam
        {
            public string day { get; set; }
            public string hour { get; set; }
            public string time { get; set; }
        }
        private byte[] getPNG(int level, int row, int col, object otherParam)
        {
            byte[] result = null;
            string uri = string.Empty;
            string type = "";
            if (otherParam != null)
            {
                Hashtable paramTable = otherParam as Hashtable;
                type = paramTable["TYPE"] as string;
                if ("traffic" == type)
                {
                    string time = paramTable["TIME"] as string;
                    uri = string.Format(baseUrls["BaiduTraffic"], level, col, row, time);
                }
                else if ("hot" == type)
                {
                    string subdomain = _subDomains[Math.Abs(level + col + row) % _subDomains.Length];
                    string time = paramTable["TIME"] as string;
                    string version = BaiDuMapManager.inst.cp.getVersionFromTime(time);
                    uri = string.Format(baseUrls["BaiduHot"], level, col, row, version, subdomain);
                }
                else if ("TrafficHis" == type)
                {
                    JavaScriptSerializer jss = new JavaScriptSerializer();
                    TrafficHisParam p = jss.Deserialize<TrafficHisParam>(paramTable["TIME"] as string);
                    uri = string.Format(baseUrls["BaiduTrafficHis"], level, col, row, p.time, p.day, p.hour);
                }
            }
            else
            {
                if ("BaiduTraffic" == _mapName)
                {
                    uri = string.Format(baseUrls["BaiduTraffic"], level, col, row, BaiDuMapManager.inst.ConvertDateTimeLong(DateTime.Now));
                    type = "traffic";
                }
                else if ("BaiduBase" == _mapName)
                {
                    string subdomain = _subDomains[Math.Abs(level + col + row) % _subDomains.Length];
                    uri = string.Format(baseUrls["BaiduBase"], level, col, row, subdomain);
                    type = "base";
                }
                else if ("BaiduHot" == _mapName)
                {
                    if (BaiDuMapManager.inst.cp == null)
                    {
                        BaiDuMapManager.inst.cp = new CacheVersionProvider();
                    }
                    string recentVersion = BaiDuMapManager.inst.cp.getCurrentVersion();
                    string subdomain = _subDomains[Math.Abs(level + col + row) % _subDomains.Length];
                    uri = string.Format(baseUrls["BaiduHot"], level, col, row, recentVersion, subdomain);
                    type = "hot";
                }
                else if ("BaiduSate" == _mapName)
                {
                    string recentVersion = "009";
                    string subdomain = _subDomains[Math.Abs(level + col + row) % _subDomains.Length];
                    uri = string.Format(baseUrls["BaiduSate"], level, col, row, recentVersion, subdomain);
                    type = "sate";
                }
                else if ("BaiduPanoMark" == _mapName)
                {
                    string subdomain = _subDomains[Math.Abs(level + col + row) % 3];
                    uri = string.Format(baseUrls["BaiduPanoMark"], "20160330", col, row, level);
                    type = "street";
                }
            }
            try
            {
                if (BaiDuMapManager.inst.instantCache != null)
                {
                    result = (byte[])BaiDuMapManager.inst.getFromCache(type, row, col, level);
                    if (result != null) return result;
                }
                result = HttpGetTileBytes(uri);
                if (result != null && BaiDuMapManager.inst.instantCache != null)
                {
                    BaiDuMapManager.inst.setCache(type, row, col, level, result);
                }
            }
            catch (Exception e)
            {
                return result;
            }
            return result;
        }
        public override byte[] GetTileBytes(int level, int row1, int col1)
        {
            if (this._mapName == "BaiduTraffic" && level < 7)
            {
                return null;
            }
            else
            {
                return BaiDuMapManager.inst.getBaiduTile(this, level, row1, col1, null, getPNG);
            }
        }
        public override byte[] GetTileBytesFromLocalCache(int level, int row, int col)
        {
            return null;
        }
        public byte[] GetTileBytes(int level, int row1, int col1, object otherParam)
        {
            return BaiDuMapManager.inst.getBaiduTile(this, level, row1, col1, otherParam, getPNG);
        }
        public CustomOnlineMap mapInfo;

        protected override void ReadTilingScheme(out TilingScheme tilingScheme)
        {
            ReadBaiduTilingScheme(out tilingScheme);
            Util.Envelope initial = tilingScheme.InitialExtent;
            Util.Point pLeftTop = Utility.GeographicToWebMercator(new Util.Point(initial.XMin, initial.YMax));
            Util.Point pRightBottom = Utility.GeographicToWebMercator(new Util.Point(initial.XMax, initial.YMin));
            tilingScheme.InitialExtent = new Envelope(pLeftTop.X, pRightBottom.Y, pRightBottom.X, pLeftTop.Y);
            this.TilingScheme = TilingSchemePostProcess(tilingScheme);
        }

        protected void ReadBaiduTilingScheme(out TilingScheme tilingScheme)
        {
            tilingScheme = SchemaProvider.Inst.getSchema("BaiDuOnline", null, null);
        }
    }
}
