using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PBS.Service;
using System.Net;
using System.IO;

namespace PBS.DataSource
{
    class DataSourceOtherMapProxy : DataSourceBase
    {
        public DataSourceOtherMapProxy(string type)
        {
            this.Type = type;
            this.Path = string.Empty;
            Initialize("N/A");
        }

        protected override void ReadTilingScheme(out TilingScheme tilingScheme)
        {
            ReadOtherMapTilingScheme(out tilingScheme);
            this.TilingScheme = TilingSchemePostProcess(tilingScheme);
        }

        protected void ReadOtherMapTilingScheme(out TilingScheme tilingScheme)
        {
            tilingScheme = SchemaProvider.Inst.getSchema(this.Type, null, null);
        }

        public override byte[] GetTileBytes(int level, int row, int col)
        {
            string baseUrl = SchemaProvider.Inst.GetDownloadUrl(this.Type, "default", "default");
            baseUrl = string.Format(baseUrl, level, row,col);
            try
            {
                return HttpGetTileBytes(baseUrl);
            }
            catch (Exception e)
            {
                //if server has response(not a downloading error) and tell pbs do not have the specific tile, return null
                if (e is WebException && (e as WebException).Response != null && ((e as WebException).Response as HttpWebResponse).StatusCode == HttpStatusCode.NotFound)
                    return null;

                string suffix = this.TilingScheme.CacheTileFormat.ToString().ToUpper().Contains("PNG") ? "png" : "jpg";
                Stream stream = this.GetType().Assembly.GetManifestResourceStream("PBS.Assets.badrequest" + this.TilingScheme.TileCols + "." + suffix);
                byte[] bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);
                return bytes;
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
                if (response.StatusCode != HttpStatusCode.OK)
                    throw new Exception(response.StatusDescription);
                return Util.Utility.StreamToBytes(response.GetResponseStream());
            }
        }
    }
}
