using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PBS.DataSource;
using System.IO;
using System.Xml.Linq;

namespace PBS.Service
{
    public class SchemaProvider
    {
        private static SchemaProvider inst;
        private class SameTypeSchemas
        {
            public string GetDownloadUrl(string dataSourceSubType, string dataSourceName)
            {
                string result;
                string key = dataSourceSubType == null ? "default" : dataSourceSubType;
                SubTypeSchemas s2;
                if (!_TSchemas.ContainsKey(key))
                {
                    result = null;
                }
                else
                {
                    s2 = _TSchemas[key];
                    result = s2.GetDownloadUrl(dataSourceName);
                }
                return result;
            }
            public void SetDownloadUrl(string dataSourceSubType, string dataSourceName, string url)
            {
                string key = dataSourceSubType == null ? "default" : dataSourceSubType;
                SubTypeSchemas s2;
                if (!_TSchemas.ContainsKey(key))
                {
                    s2 = new SubTypeSchemas();
                    _TSchemas.Add(key, s2);
                }
                else
                {
                    s2 = _TSchemas[key];
                }
                s2.SetDownloadUrl(dataSourceName, url);
            }
            public TilingScheme Get(string dataSourceSubType, string dataSourceName)
            {
                TilingScheme result;
                string key = dataSourceSubType == null ? "default" : dataSourceSubType;
                SubTypeSchemas s2;
                if (!_TSchemas.ContainsKey(key))
                {
                    result = null;
                }
                else
                {
                    s2 = _TSchemas[key];
                    result = s2.Get(dataSourceName);
                }
                return result;
            }
            public void Add(string dataSourceSubType, string dataSourceName, TilingScheme schema)
            {
                string key = dataSourceSubType == null ? "default" : dataSourceSubType;
                SubTypeSchemas s2;
                if (!_TSchemas.ContainsKey(key))
                {
                    s2 = new SubTypeSchemas();
                    _TSchemas.Add(key, s2);
                }
                else
                {
                    s2 = _TSchemas[key];
                }
                s2.Add(dataSourceName, schema);
            }
            public SameTypeSchemas()
            {
                _TSchemas = new Dictionary<string, SubTypeSchemas>();
            }
            private class SubTypeSchemas
            {
                private Dictionary<string, TilingScheme> _STSchemas;
                private Dictionary<string, string> _downloadUrls;
                public SubTypeSchemas()
                {
                    _STSchemas = new Dictionary<string, TilingScheme>();
                    _downloadUrls = new Dictionary<string, string>();
                }
                public string GetDownloadUrl(string dataSourceName)
                {
                    string result;
                    string key = dataSourceName == null ? "default" : dataSourceName;
                    if (!_downloadUrls.ContainsKey(key))
                    {
                        result = null;
                    }
                    else
                    {
                        result = _downloadUrls[key];
                    }
                    return result;
                }
                public TilingScheme Get(string dataSourceName)
                {
                    TilingScheme result;
                    string key = dataSourceName == null ? "default" : dataSourceName;
                    if (!_STSchemas.ContainsKey(key))
                    {
                        result = null;
                    }
                    else
                    {
                        TilingScheme temp = _STSchemas[key];
                        result = new TilingScheme();
                        result.CacheTileFormat = temp.CacheTileFormat;
                        result.CompressionQuality = temp.CompressionQuality;
                        result.DPI = temp.DPI;
                        result.FullExtent = temp.FullExtent;
                        result.InitialExtent = new Util.Envelope() { XMin = temp.InitialExtent.XMin, XMax = temp.InitialExtent.XMax, YMin = temp.InitialExtent.YMin, YMax = temp.InitialExtent.YMax };
                        result.LODs = temp.LODs;
                        result.LODsJson = temp.LODsJson;
                        result.PacketSize = temp.PacketSize;
                        result.Path = temp.Path;
                        result.RestResponseArcGISJson = temp.RestResponseArcGISJson;
                        result.RestResponseArcGISPJson = temp.RestResponseArcGISPJson;
                        result.StorageFormat = temp.StorageFormat;
                        result.TileCols = temp.TileCols;
                        result.TileOrigin = temp.TileOrigin;
                        result.TileRows = temp.TileRows;
                        result.WKID = temp.WKID;
                        result.WKT = temp.WKT;
                    }
                    return result;
                }
                public void Add(string dataSourceName, TilingScheme schema)
                {
                    string key = dataSourceName == null ? "default" : dataSourceName;
                    _STSchemas.Add(dataSourceName, schema);
                }
                public void SetDownloadUrl(string dataSourceName, string url)
                {
                    string key = dataSourceName == null ? "default" : dataSourceName;
                    _downloadUrls.Add(dataSourceName, url);
                }
            }
            private Dictionary<string, SubTypeSchemas> _TSchemas;
            private string TypeUrlConfig;
            private PBS.Util.Envelope DownloadExtend;
            public PBS.Util.Envelope getDownloadExtend()
            {
                return DownloadExtend;
            }
            public void setDownloadExtend(PBS.Util.Envelope e)
            {
                DownloadExtend = e;
            }
            public string getUrlConfig()
            {
                return TypeUrlConfig;
            }
            public void setUrlConfig(string url)
            {
                TypeUrlConfig = url;
            }
        }
        public void setDownloadExtend(string dataSourceType, PBS.Util.Envelope e)
        {
            string key = dataSourceType == null ? "default" : dataSourceType;
            SameTypeSchemas s1 = schemas[key];
            s1.setDownloadExtend(e);
        }
        public PBS.Util.Envelope getDownloadExtend(string dataSourceType)
        {
            string key = dataSourceType == null ? "default" : dataSourceType;
            SameTypeSchemas s1 = schemas[key];
            return s1.getDownloadExtend();
        }
        public void setConfigPath(string dataSourceType, string path)
        {
            string key = dataSourceType == null ? "default" : dataSourceType;
            SameTypeSchemas s1 = schemas[key];
            s1.setUrlConfig(path);
        }
        public string getConfigPath(string dataSourceType)
        {
            string key = dataSourceType == null ? "default" : dataSourceType;
            SameTypeSchemas s1 = schemas[key];
            return s1.getUrlConfig();
        }
        private Dictionary<string, SameTypeSchemas> schemas { get; set; }
        public void SetDownloadUrl(string dataSourceType, string dataSourceSubType, string dataSourceName, string url)
        {
            string key = dataSourceType == null ? "default" : dataSourceType;
            SameTypeSchemas s1;
            if (!schemas.ContainsKey(key))
            {
                s1 = new SameTypeSchemas();
                schemas.Add(key, s1);
            }
            else
            {
                s1 = schemas[key];
            }
            s1.SetDownloadUrl(dataSourceSubType, dataSourceName, url);
        }
        public string GetDownloadUrl(string dataSourceType, string dataSourceSubType, string dataSourceName)
        {
            string result;
            string key = dataSourceType == null ? "default" : dataSourceType;
            SameTypeSchemas s1;
            if (!schemas.ContainsKey(key))
            {
                result = null;
            }
            else
            {
                s1 = schemas[key];
                result = s1.GetDownloadUrl(dataSourceSubType, dataSourceName);
            }
            return result;
        }
        public void addSchema(string dataSourceType, string dataSourceSubType, string dataSourceName, TilingScheme schema)
        {
            string key = dataSourceType == null ? "default" : dataSourceType;
            SameTypeSchemas s1;
            if (!schemas.ContainsKey(key))
            {
                s1 = new SameTypeSchemas();
                schemas.Add(key, s1);
            }
            else
            {
                s1 = schemas[key];
            }
            s1.Add(dataSourceSubType, dataSourceName, schema);
        }
        public TilingScheme getSchema(string dataSourceType, string dataSourceSubType, string dataSourceName)
        {
            TilingScheme result;
            string key = dataSourceType == null ? "default" : dataSourceType;
            SameTypeSchemas s1;
            if (!schemas.ContainsKey(key))
            {
                result = null;
            }
            else
            {
                s1 = schemas[key];
                result = s1.Get(dataSourceSubType, dataSourceName);
            }
            return result;
        }
        private SchemaProvider()
        {
            schemas = new Dictionary<string, SameTypeSchemas>();
        }
        public static SchemaProvider Inst
        {
            get
            {
                if (inst == null)
                {
                    inst = new SchemaProvider();
                    inst.ReadTilingScheme();
                }
                return inst;
            }
        }
        public void ReadTilingScheme()
        {
            string configName = AppDomain.CurrentDomain.BaseDirectory + "CustomTile.xml";
            if (!File.Exists(configName))
            {
                throw new FileNotFoundException(configName + " does not exist!");
            }
            XDocument xDoc = XDocument.Load(configName);
            XElement schemaElement = xDoc.Element("Services");

            foreach (XElement se in schemaElement.Elements())
            {
                string type = se.Attribute("type").Value;
                string subType = se.Attribute("subType").Value;
                string name = se.Attribute("name").Value;
                var lods = from map in se.Descendants("Lods").Elements()
                           select new
                           {
                               Level = map.Attribute("level").Value,
                               Resolution = map.Attribute("resolution").Value,
                               Scale = map.Attribute("scale").Value
                           };

                TilingScheme tilingScheme = new TilingScheme();
                StringBuilder sb;
                tilingScheme.Path = "N/A";
                tilingScheme.CacheTileFormat = PBS.DataSource.ImageFormat.PNG;
                tilingScheme.CompressionQuality = 75;
                tilingScheme.DPI = Convert.ToInt32(se.Element("Dpi").Attribute("value").Value);
                tilingScheme.LODs = new LODInfo[lods.Count()];

                sb = new StringBuilder("\r\n");
                int i = 0;
                foreach (var lod in lods)
                {
                    int levelId = Convert.ToInt32(lod.Level);
                    tilingScheme.LODs[i] = new LODInfo()
                    {
                        LevelID = levelId,
                        Resolution = Convert.ToDouble(lod.Resolution),
                        Scale = Convert.ToDouble(lod.Scale)
                    };
                    sb.Append(@"      {""level"":" + lod.Level + "," + @"""resolution"":" + lod.Resolution + "," + @"""scale"":" + lod.Scale + @"}," + "\r\n");
                    i++;
                }
                tilingScheme.LODsJson = sb.ToString().Remove(sb.ToString().Length - 3);//remove last "," and "\r\n"
                //two extent
                tilingScheme.InitialExtent = new PBS.Util.Envelope(Convert.ToDouble(se.Element("InitialExtent").Attribute("xmin").Value), Convert.ToDouble(se.Element("InitialExtent").Attribute("ymin").Value)
                    , Convert.ToDouble(se.Element("InitialExtent").Attribute("xmax").Value), Convert.ToDouble(se.Element("InitialExtent").Attribute("ymax").Value));
                tilingScheme.FullExtent = new PBS.Util.Envelope(Convert.ToDouble(se.Element("FullExtent").Attribute("xmin").Value), Convert.ToDouble(se.Element("FullExtent").Attribute("ymin").Value)
                    , Convert.ToDouble(se.Element("FullExtent").Attribute("xmax").Value), Convert.ToDouble(se.Element("FullExtent").Attribute("ymax").Value));
                tilingScheme.PacketSize = 128;
                tilingScheme.StorageFormat = StorageFormat.esriMapCacheStorageModeExploded;
                tilingScheme.TileRows = Convert.ToInt32(se.Element("Rows").Attribute("value").Value);
                tilingScheme.TileCols = Convert.ToInt32(se.Element("Cols").Attribute("value").Value);

                tilingScheme.TileOrigin = new PBS.Util.Point(Convert.ToDouble(se.Element("Origin").Attribute("x").Value), Convert.ToDouble(se.Element("Origin").Attribute("y").Value));
                tilingScheme.WKID = Convert.ToInt32(se.Element("SpatialReference").Attribute("wkid").Value);
                tilingScheme.WKT = @"PROJCS[""WGS_1984_Web_Mercator_Auxiliary_Sphere"",GEOGCS[""GCS_WGS_1984"",DATUM[""D_WGS_1984"",SPHEROID[""WGS_1984"",6378137.0,298.257223563]],PRIMEM[""Greenwich"",0.0],UNIT[""Degree"",0.0174532925199433]],PROJECTION[""Mercator_Auxiliary_Sphere""],PARAMETER[""False_Easting"",0.0],PARAMETER[""False_Northing"",0.0],PARAMETER[""Central_Meridian"",0.0],PARAMETER[""Standard_Parallel_1"",0.0],PARAMETER[""Auxiliary_Sphere_Type"",0.0],UNIT[""Meter"",1.0],AUTHORITY[""ESRI"",""3857""]]";
                addSchema(type, subType, name, tilingScheme);
                string configPath = se.Element("MainTypeUrlConfig").Attribute("path").Value;
                setConfigPath(type, configPath);
                string downloadUrl = se.Element("MapUrl").Attribute("value").Value;
                SetDownloadUrl(type, subType, name, downloadUrl);
                PBS.Util.Envelope downLoadExtent = new PBS.Util.Envelope(Convert.ToDouble(se.Element("DownLoadExtent").Attribute("xmin").Value), Convert.ToDouble(se.Element("DownLoadExtent").Attribute("ymin").Value),
                    Convert.ToDouble(se.Element("DownLoadExtent").Attribute("xmax").Value), Convert.ToDouble(se.Element("DownLoadExtent").Attribute("ymax").Value));
                setDownloadExtend(type, downLoadExtent);
            }
        }
    }
}
