using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ESRI.ArcGIS.Client;
using PBS.Service;
using System.ServiceModel.Web;
using System.ServiceModel.Description;
using System.ServiceModel;
using PBS.DataSource;
using PBS.APP.Classes;
using System.Windows.Input;
using System.Windows.Forms;
using System.ComponentModel;
using System.IO;
using System.Collections.ObjectModel;
using PBS.Util;
using ImageMagick;

namespace PBS.APP.ViewModels
{  
    public class VMConvertBaiduToMBTiles : VMConvertOnlineToMBTiles
    {
        private DataSourceCustomOnlineMaps _datasource;
        private ESRI.ArcGIS.Client.Geometry.Geometry initalExtend;
        public DataSourceCustomOnlineMaps Datasource
        {
            get { return _datasource; }
            set
            {
                _datasource = value;
                NotifyPropertyChanged(p => p.Datasource);
            }
        }
        private bool _autoConfirm;
        public bool AutoConfirm
        {
            get { return _autoConfirm; }
            set
            {
                _autoConfirm = value;
            }
        }
        public VMConvertBaiduToMBTiles(Map esriMap, int port)
        {
            if (BaiDuMapManager.inst.cp == null)
            {
                BaiDuMapManager.inst.cp = new CacheVersionProvider();
                System.Threading.Tasks.Task.Factory.StartNew(() => { BaiDuMapManager.inst.cp.initVersionFormWeb(); });
            }
            if (!PBS.Util.Utility.IsConnectedToInternet())
            {
                throw new Exception("No internet connectivity!");
            }
            _port = port;
            //check the availability of the port using for this map
            if (!ServiceManager.PortEntities.ContainsKey(_port))
            {
                try
                {
                    //WebServiceHost host = new WebServiceHost(serviceProvider, new Uri("http://localhost:" + port));
                    WebServiceHost host = new WebServiceHost(typeof(PBSServiceProvider), new Uri("http://localhost:" + _port));
                    host.AddServiceEndpoint(typeof(IPBSServiceProvider), new WebHttpBinding(), "").Behaviors.Add(new WebHttpBehavior());
                    ServiceDebugBehavior stp = host.Description.Behaviors.Find<ServiceDebugBehavior>();
                    stp.HttpHelpPageEnabled = false;
                    host.Open();
                    ServiceManager.PortEntities.Add(_port, new PortEntity(host, new PBSServiceProvider()));
                }
                catch (Exception e)
                {
                    string m = "The port using for this map is not available.\r\n";
                    //HTTP 无法注册 URL http://+:7777/CalulaterServic/。进程不具有此命名空间的访问权限(有关详细信息，请参阅 http://go.microsoft.com/fwlink/?LinkId=70353)
                    if (e.Message.Contains("http://go.microsoft.com/fwlink/?LinkId=70353"))
                    {
                        throw new Exception(m + "Your Windows has enabled UAC, which restrict of Http.sys Namespace. Please reopen PortableBasemapServer by right clicking and select 'Run as Administrator'. \r\nAdd WebServiceHost Error!\r\n" + e.Message);
                    }
                    throw new Exception(m + e.Message);
                }
            }
            try
            {
                foreach (var map in DataSourceBaiduOnlineMap.CustomOnlineMaps)
                {
                    DataSourceTypes.Add(map.Name);
                }
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
            SelectedDatasourceType = DataSourceTypes[0];
            CMDClickBrowseButton = new DelegateCommand(BrowseButtonClicked);
            CMDSaveArea = new DelegateCommand(StartSaveArea);
            CMDResetArea = new DelegateCommand(ZoomToInitialExtend);
            CMDClickStartButton = new DelegateCommand(StartButtonClicked, (p) => { return !string.IsNullOrEmpty(Output) && DownloadExtent != null && (Levels != null && Levels.Length > 0) && IsIdle; });
            CMDClickProfileButton = new DelegateCommand(ProfileButtonClicked, (p) => { return !string.IsNullOrEmpty(SelectedProfile); });
            IsIdle = true;
            SelectedIndexOfDrawExtentMethod = 0;
            Profiles = new ObservableCollection<string>(_configManager.GetAllDownloadProfileNames());

            _map = esriMap;
            //current level
            _map.ExtentChanged += (s, a) =>
            {
                if ((_map.Layers[0] as TiledMapServiceLayer).TileInfo == null)
                    return;
                int i;
                for (i = 0; i < (_map.Layers[0] as TiledMapServiceLayer).TileInfo.Lods.Length; i++)
                {
                    if (Math.Abs(_map.Resolution - (_map.Layers[0] as TiledMapServiceLayer).TileInfo.Lods[i].Resolution) < 0.000001)
                    {
                        break;
                    }
                }
                lastLevel = i;
                CurrentLevel = i.ToString();
                NotifyPropertyChanged(p => p.CurrentLevel);
            };
            _graphicsLayer = new GraphicsLayer();
            _map.Layers.Add(_graphicsLayer);
            _map.MouseRightButtonDown += new MouseButtonEventHandler(MouseRightButtonDown);
            _map.MouseRightButtonUp += new MouseButtonEventHandler(MouseRightButtonUp);
            _map.MouseMove += new System.Windows.Input.MouseEventHandler(MouseMove);
            //load first map
            ChangeMap();
        }
        protected void StartSaveArea(object parameters)
        {
            DebugWindow dw = new DebugWindow();
            dw.notifyClosing += (p) =>
            {
                string func = (string)p;
                if (func == "Func1")
                {
                    DataSourceAdjustCoord transferSource = new DataSourceAdjustCoord("D:\\yygyxkjy\\DevHome\\PBS\\PortableBasemapServer\\bin\\Debug\\cache\\LZW.mbtiles");
                    Datasource = transferSource;
                    (transferSource as DataSourceAdjustCoord).ConvertCompleted += (s1, a1) =>
                    {
                        string str1 = App.Current.FindResource("msgAdjustComplete").ToString();
                        MessageBox.Show(str1, "", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    };
                    transferSource.ConvertToMBTiles(Output, "", "", "", new int[]{3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18}, null, false);
                }
                else if (func == "Func2")
                {
                    System.Drawing.Bitmap png = new System.Drawing.Bitmap("D:\\PBS\\PBS-Prd9\\cache\\T.png");
                    System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(png);
                    System.Drawing.Drawing2D.CompositingQuality q = g.CompositingQuality;
                    System.Drawing.Drawing2D.InterpolationMode m = g.InterpolationMode;
                }
                else if(func == "Func3")
                {
                    using (MagickImage image = new MagickImage("C:\\Users\\wenvi\\Desktop\\Thread.png"))
                    {
                        image.SetDefine(ImageMagick.MagickFormat.Png8, "compression", "lzw");
                        image.Write("C:\\Users\\wenvi\\Desktop\\T_C.png");
                    }
                }
            };
            dw.ShowDialog();            
        }
        protected override void ChangeMap()
        {
            if (base._map != null)
            {
                int i;
                if (int.TryParse(_hiddenServiceName.ToCharArray()[_hiddenServiceName.Length - 1].ToString(), out i))//if last char of _hiddenServiceName is int
                    ServiceManager.DeleteService(_port, _hiddenServiceName);
                else
                    _hiddenServiceName = _hiddenServiceName + ServiceManager.PortEntities[_port].ServiceProvider.Services.Count(service => service.Key.Contains(_hiddenServiceName)).ToString();//INTERNALDOWNLOAD0,INTERNALDOWNLOAD1...
                _pbsService = new PBSService(_hiddenServiceName, "", _port, SelectedDatasourceType, false, true, true, VisualStyle.None, null);
                
                ServiceManager.PortEntities[_port].ServiceProvider.Services.Add(_pbsService.ServiceName, _pbsService);
                _map.Layers.RemoveAt(0);
                BaiduTileLayer l = new BaiduTileLayer() { baseUrl = _pbsService.UrlArcGIS };
                _map.Layers.Insert(0, l);
                Util.Envelope initial = _pbsService.DataSource.TilingScheme.InitialExtent;
                initalExtend = new ESRI.ArcGIS.Client.Geometry.Envelope(initial.XMin, initial.YMin, initial.XMax,initial.YMax);
            }
        }
        private void ZoomToInitialExtend(object parameters)
        {
            _map.ZoomTo(initalExtend);
        }
        protected override void StartButtonClicked(object parameters)
        {
            if (!PBS.Util.Utility.IsValidFilename(Output))
            {
                MessageBox.Show(App.Current.FindResource("msgOutputPathError").ToString(), App.Current.FindResource("msgError").ToString(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (File.Exists(Output))
            {
                if (MessageBox.Show(App.Current.FindResource("msgOverwrite").ToString(), App.Current.FindResource("msgWarning").ToString(), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
                    return;
                else
                {
                    try
                    {
                        File.Delete(Output);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(e.Message, App.Current.FindResource("msgError").ToString(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
            }
            try
            {
                string version = null;
                if (SelectedDatasourceType == "BaiduSate")
                {
                    version = "009";
                }
                else{
                    version = BaiDuMapManager.inst.cp.getLastVersion();
                }
                Datasource = new DataSourceBaiduOnlineMap(SelectedDatasourceType) { OutputFileName = Output, Version = version, autoCorrectCoord = true};
      
                (Datasource as DataSourceBaiduOnlineMap).ConvertCompleted += (s, a) =>
                {
                    if (a.Successful)
                    {
                        if (!AutoConfirm)
                        {
                            string str = App.Current.FindResource("msgConvertComplete").ToString();
                            if (DoCompact)
                                str += "\r\n" + App.Current.FindResource("msgCompactResult").ToString() + (Datasource.ConvertingStatus.SizeBeforeCompact / 1024).ToString("N0") + "KB --> " + (Datasource.ConvertingStatus.SizeAfterCompact / 1024).ToString("N0") + "KB";
                            MessageBox.Show(str, "", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }


                        DataSourceAdjustCoord transferSource = new DataSourceAdjustCoord(Output);
                        Datasource = transferSource;
                        (transferSource as DataSourceAdjustCoord).ConvertCompleted += (s1, a1) =>
                        {
                            string str1 = App.Current.FindResource("msgAdjustComplete").ToString();
                            MessageBox.Show(str1, "", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        };
                        transferSource.ConvertToMBTiles(Output, "", "", "", Levels, null, false);
                    }
                };
                Datasource.ConvertCancelled += (s, a) =>
                {
                };
                BackgroundWorker bw = new BackgroundWorker();
                bw.DoWork += (s, a) =>
                {
                    IsIdle = false;
                    App.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        (CMDClickStartButton as DelegateCommand).RaiseCanExecuteChanged();
                    }));
                    ESRI.ArcGIS.Client.Geometry.Envelope extent = (ESRI.ArcGIS.Client.Geometry.Envelope)_webMercator.FromGeographic(DownloadExtent);
                    try
                    {
                        PBS.Util.Geometry g = _downloadPolygon == null ? (PBS.Util.Geometry)new PBS.Util.Envelope(DownloadExtent.XMin, DownloadExtent.YMin, DownloadExtent.XMax, DownloadExtent.YMax) : _downloadPolygon;
                        if (_downloadPolygon != null)
                            MessageBox.Show(App.Current.FindResource("msgDownloadByPolygonIntro").ToString(), "", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        Datasource.ConvertToMBTiles(Output, Name, Description, Attribution, Levels, g, DoCompact);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(e.Message, App.Current.FindResource("msgError").ToString(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    IsIdle = true;
                    App.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        (CMDClickStartButton as DelegateCommand).RaiseCanExecuteChanged();
                    }));
                };
                bw.RunWorkerAsync();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }
    }
}
