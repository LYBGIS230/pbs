using PBS.DataSource;
using PBS.Service;
using PBS.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace PBS.DataSource
{
    public delegate void NotifyStatus(int id, bool isReady);
    public delegate void NotifyEvent(object param);
    public interface IWorker
    {
        string Message { get; set; }
        event NotifyEvent JobFinished;
        event NotifyEvent ErrorOccurred;
        event NotifyStatus Notify;
        IWorker initProperties(int num, EventWaitHandle jobSingal, NotifyStatus notifyCallBack);
    }
    public interface ILauncher
    {
        void start();
    }
    public abstract class WorkerBase : IWorker
    {
        private event NotifyStatus notifier;
        private event NotifyEvent onJobFinished;
        public event NotifyEvent ErrorOccurred;
        static readonly object _locker = new object();
        protected int threadNumber;
        private Thread worker;
        private EventWaitHandle _downloadSingal;
        protected string message;
        public string Message
        {
            get
            {
                return message;
            }
            set
            {
                message = value;
            }
        }
        public event NotifyStatus Notify
        {
            add
            {
                notifier += value;
            }
            remove
            {
                notifier -= value;
            }
        }
        public event NotifyEvent JobFinished
        {
            add
            {
                onJobFinished += value;
            }
            remove
            {
                onJobFinished -= value;
            }
        }
        public IWorker initProperties(int num, EventWaitHandle jobSingal, NotifyStatus notifyCallBack)
        {
            notifier = notifyCallBack;
            _downloadSingal = jobSingal;
            this.threadNumber = num;
            worker = new Thread(Work);
            worker.Start();
            return this;
        }
        public void FireErrorEvent(object param)
        {
            if (ErrorOccurred != null)
            {
                ErrorOccurred(param);
            }
        }
        private void OnJobFinished(object praram)
        {
            if (this.onJobFinished != null)
            {
                this.onJobFinished(praram);
            }
        }
        public abstract object WorkContent();
        void Work()
        {
            object product = null;
            while (true)
            {
                notifier.Invoke(threadNumber, true);
                _downloadSingal.WaitOne();
                notifier.Invoke(threadNumber, false);
                try
                {
                    product = WorkContent();
                }
                finally
                {
                    OnJobFinished(product);
                }
            }
        }
    }

    public abstract class Dispatcher<T> : ILauncher where T : WorkerBase
    {
        private static Dispatcher<T> _inst;
        public static Dispatcher<T> getInst(int childThreadNum)
        {
            return _inst;
        }
        public event NotifyEvent AllJobFinished;
        private int currentIndex = 0; //currently inquiryed thread number
        private int childCount = 5;
        private EventWaitHandle[] _download;
        protected int jobCount;
        protected int finishedJobCount;
        protected bool allJobArrived;
        string[] _messages;
        WorkerBase[] downloaders;
        private bool[] downloaderStatus;

        readonly object _tasklocker = new object();
        Queue<object> _tasks = new Queue<object>();
        private Semaphore _consumer = new Semaphore(0, 100);
        private Thread _consumerThread;

        public readonly object _threadChooselocker = new object();
        readonly object _errorlocker = new object();
        Queue<object> _errorInfo = new Queue<object>();
        private EventWaitHandle _errorHandler = new AutoResetEvent(false);
        public abstract object DoConsume(Object product);

        DateTime jobStart;
        public abstract void Run();
        public void setTotalJobCount(int count)
        {
            jobCount = count;
            finishedJobCount = 0;
            allJobArrived = false;
        }
        public void RunJob(string param)
        {
            DoJob(chooseIdleThread(), param);
        }

        private int chooseIdleThread()
        {
            int result = 0;
            lock (_threadChooselocker)
            {
                for (; currentIndex < childCount; currentIndex++)
                {
                    if (downloaderStatus[currentIndex])
                    {
                        result = currentIndex;
                        currentIndex = (currentIndex + 1) % childCount;
                        break;
                    }

                    if (currentIndex == childCount - 1)
                    {
                        currentIndex = -1;
                    }
                }
            }
            return result;
        }
        private void DoJob(int i, string message)
        {
            downloaderStatus[i] = false;
            downloaders[i].Message = message;
            _download[i].Set();
        }
        private void updateStatus(int id, bool status)
        {
            downloaderStatus[id] = status;
        }
        protected virtual void afterJobFinished(object param)
        {
            if (param != null)
            {
                EnqueueTask(param);
                onWorkFinished(param);
                finishedJobCount++;
                if (jobCount == finishedJobCount)
                {
                    allJobArrived = true;
                }
            }
        }
        public abstract void onWorkFinished(object param);
        public Dispatcher(int childThreadNum)
        {
            childCount = childThreadNum;
            _download = new EventWaitHandle[childCount];
            _messages = new string[childCount];
            downloaders = new WorkerBase[childCount];
            downloaderStatus = new bool[childCount];
            finishedJobCount = 0;
            initThread();
            _inst = this;
        }
        private void initThread()
        {
            for (int i = 0; i < childCount; i++)
            {
                _download[i] = new AutoResetEvent(false);
                downloaderStatus[i] = true;
            }
            for (int i = 0; i < childCount; i++)
            {
                downloaders[i] = (T)Activator.CreateInstance(typeof(T), true);
                downloaders[i].initProperties(i, _download[i], updateStatus);
                downloaders[i].JobFinished += afterJobFinished;
                downloaders[i].ErrorOccurred += EnqueueError;
            }
            jobStart = DateTime.Now;
            _consumerThread = new Thread(Consume);
            _consumerThread.Start();
        }
        public void EnqueueTask(object task)
        {
            lock (_tasklocker) _tasks.Enqueue(task);
            try {
                _consumer.Release();
            }
            catch (SemaphoreFullException e)
            {
                Utility.LogSimple(LogLevel.Debug, "huge over stock, 100 is not enough for semaphore lock");
            }
        }

        public void EnqueueError(object error)
        {
            lock (_errorlocker) _errorInfo.Enqueue(error);
            _errorHandler.Set();
        }
        private void Consume()
        {
            while (true)
            {
                object task = null;
                lock (_tasklocker)
                {
                    if (_tasks.Count > 0)
                    {
                        task = _tasks.Dequeue();
                    }
                }
                if (task != null)
                {
                    DoConsume(task);
                    if (jobCount == finishedJobCount && AllJobFinished != null)
                    {
                        AllJobFinished(finishedJobCount);
                    }
                }
                else
                {
                    _consumer.WaitOne();
                }
            }
        }
        public void start()
        {
            Run();
        }
    }
    public class MultiPicDownloadDispatcher<T> : Dispatcher<T> where T : WorkerBase
    {
        private System.Timers.Timer _timer = new System.Timers.Timer(500);
        private int secondEclipsed = 0;

        public bool isIdle = true;
        private static MultiPicDownloadDispatcher<T> _inst;
        public static MultiPicDownloadDispatcher<T> getInst(int childThreadNum)
        {
            if (_inst == null)
            {
                _inst = new MultiPicDownloadDispatcher<T>(childThreadNum);
            }
            return _inst;
        }
        private string panOid;
        private int zoom;
        private string udt = BaiDuMapManager.inst.streetudt;
        byte[][] resultBuffer;
        int[] partCounts = new int[] { 1, 2, 8, 32, 128 };
        private List<int> recoverPoints;
        bool[] indicators;
        byte[] finalPic;
        ManualResetEvent manlEvent = new ManualResetEvent(false);
        public MultiPicDownloadDispatcher(int childThreadNum)
            : base(childThreadNum)
        {
            /*if (childThreadNum == 4)
            {
                this.recoverPoints = new List<int>() { 5, 9, 12, 14, 15 };
            }
            else if (childThreadNum == 8)
            {
                this.recoverPoints = new List<int>() { 9, 17, 23, 27, 29 };
            }
            else if (childThreadNum == 16)
            {
                this.recoverPoints = new List<int>() { 13, 25, 33, 37, 40 };
            }*/
            this.recoverPoints = new List<int>() { 13, 25, 33, 37, 40 };
            this.AllJobFinished += (count) =>
            {
                Utility.LogSimple(LogLevel.Debug, "All job finished triggered: " + panOid + ", " + (zoom + 1));
                Bitmap map;
                Graphics destPic;
                byte[] picBytes;
                Bitmap part = null;
                MemoryStream output = new MemoryStream();
                map = new Bitmap((int)(System.Math.Pow(2.0, zoom) * 512), (int)(System.Math.Pow(2.0, zoom - 1) * 512));
                destPic = Graphics.FromImage(map);
                for (int tiltangle = 0; tiltangle < System.Math.Pow(2.0, zoom - 1); tiltangle++)
                {
                    for (int panangle = 0; panangle < System.Math.Pow(2.0, zoom); panangle++)
                    {
                        picBytes = resultBuffer[tiltangle * (int)Math.Pow(2, zoom) + panangle];
                        if (picBytes == null) {
                            //Utility.LogSimple(LogLevel.Debug, "Tile Not loaded, value null, Params: " + panOid + ", " + (zoom + 1) + "_" + tiltangle + "_" + panangle);
                            continue;
                        };
                        part = new Bitmap(new MemoryStream(picBytes));
                        destPic.DrawImage(part, panangle * 512, tiltangle * 512);
                        part.Dispose();
                    }
                }
                map.Save(output, System.Drawing.Imaging.ImageFormat.Jpeg);
                finalPic = output.ToArray();
                manlEvent.Set();
                Utility.LogSimple(LogLevel.Debug, "AllJobFinished and set called: " + panOid + ", " + (zoom + 1));
            };
            _timer.Elapsed += (s, a) =>
            {
                int len = indicators.Length;
                secondEclipsed += 1;
                if (secondEclipsed < recoverPoints[4])
                {
                    if (recoverPoints.Contains(secondEclipsed))
                    {
                        if (!allJobArrived)
                        {
                            Thread recoverThread = new Thread(() =>
                            {
                                for (int i = 0; i < len; i++)
                                {
                                    if (!indicators[i])
                                    {
                                        int[] p = decodeIndex(i, zoom);
                                        Utility.LogSimple(LogLevel.Debug, "start up recover thread");
                                        RunJob(panOid + "," + udt + "," + zoom + "," + p[1] + "," + p[0]);
                                        //Utility.LogSimple(LogLevel.Debug, "Tile Recovered, Params: " + panOid + ", " + (zoom + 1) + "_" + p[0] + "_" + p[1]);
                                    }
                                }
                            });
                            recoverThread.Start();
                        }
                        else
                        {
                            Utility.LogSimple(LogLevel.Debug, "All job arrived triggered for level : " + (zoom + 1));
                            _timer.Stop();
                            if (secondEclipsed == 1.5)
                            {
                                //Utility.LogSimple(LogLevel.Debug, "OK, no need to recover: " + panOid + ", " + (zoom + 1));
                            }
                            else
                            {
                                //Utility.LogSimple(LogLevel.Debug, "Recoverred in time and timer stop called: " + panOid + ", " + (zoom + 1));
                            }
                            return;
                        }
                    }
                }
                else
                {
                    _timer.Stop();
                    Utility.LogSimple(LogLevel.Debug, "Time out and timer stop called: " + panOid + ", " + (zoom + 1) + ", finished count: " + finishedJobCount + ", total job count:" + jobCount);
                    if (!isIdle)
                    {
                        isIdle = true;
                        setTotalJobCount(9999);
                        finalPic = new byte[0];
                        manlEvent.Set();
                        Utility.LogSimple(LogLevel.Debug, "Time out and set called: " + panOid + ", " + (zoom + 1));
                    }
                }
            };
        }
        protected override void afterJobFinished(object param)
        {
            if (param != null)
            {
                PicResult r = param as PicResult;
                if (indicators[r.row * (int)Math.Pow(2, r.zoom) + r.col])
                {
                    //Utility.LogSimple(LogLevel.Debug, "Repeated Tile abandoned, Params: " + panOid + ", " + (zoom + 1) + "_" + r.row + "_" + r.col);
                    return;
                }
                EnqueueTask(param);
                onWorkFinished(param);
                finishedJobCount++;
                if (jobCount == finishedJobCount)
                {
                    allJobArrived = true;
                }
            }
        }
        public void setPicInfo(string pid, int level)
        {
            panOid = pid;
            zoom = level;
            resultBuffer = new byte[partCounts[level]][];
            indicators = new bool[partCounts[level]];
        }

        public override void onWorkFinished(object param)
        {
        }
        private int encodeIndex(int row, int col, int zoom)
        {
            return row * (int)Math.Pow(2, zoom) + col;
        }
        private int[] decodeIndex(int index, int zoom)
        {
            int colWidth = (int)Math.Pow(2, zoom);
            int col = index % colWidth;
            int row = index / colWidth;
            return new int[] { row, col };
        }
        public override object DoConsume(object product)
        {
            PicResult r = product as PicResult;
            if (r.pic != null)
            {
                //Utility.LogSimple(LogLevel.Debug, "Buffer setted, Params: " + panOid + ", " + (zoom + 1) + "_" + r.row + "_" + r.col + ", " + r.pic.Length);
                resultBuffer[r.row * (int)Math.Pow(2, r.zoom) + r.col] = r.pic;
                indicators[r.row * (int)Math.Pow(2, r.zoom) + r.col] = true;
            }
            return null;
        }
        public override void Run()
        {
            isIdle = false;
            Utility.LogSimple(LogLevel.Debug, "Start Download Spot Pic, Params: " + panOid + ", " + (zoom + 1));
            setTotalJobCount(indicators.Length);
            for (int row = 0; row < System.Math.Pow(2.0, zoom - 1); row++)
            {
                for (int col = 0; col < System.Math.Pow(2.0, zoom); col++)
                {
                    RunJob(panOid + "," + udt + "," + zoom + "," + col + "," + row);
                }
            }
            secondEclipsed = 0;
            _timer.Start();
            //Utility.LogSimple(LogLevel.Debug, "Timer started");
        }
        public byte[] getResult()
        {
            this.manlEvent.WaitOne();
            isIdle = true;
            this.manlEvent.Reset();
            return finalPic;
        }
    }
    public class DownloadWorker : WorkerBase
    {
        public override object WorkContent()
        {
            string[] paramters = message.Split(',');
            return download(paramters[0], paramters[1], int.Parse(paramters[2]), paramters[3], paramters[4]);
        }
        protected byte[] HttpGetTileBytes(string uri)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.Accept = "*/*";
            request.KeepAlive = true;
            request.Method = "GET";
            request.Proxy = null;//==no proxy
            request.Timeout = 1000;
            try {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    return Util.Utility.StreamToBytes(response.GetResponseStream());
                }
            }
            catch
            {
                return null;
            }
        }
        public PicResult download(string panOid, string udt, int zoom, string col, string row)
        {
            string baseUrl = "http://pcsv1.map.bdimg.com/?qt=pdata&sid={0}&pos={1}_{2}&z={4}&udt={3}";
            string url = string.Format(baseUrl, panOid, row, col, udt, zoom + 1);
            byte[] picBytes = HttpGetTileBytes(url);
            //Utility.LogSimple(LogLevel.Debug, "Tile downloaded, Params: " + panOid + ", " + (zoom + 1) + "_" + row + "_" + col + ", size:" + picBytes.Length);
            if (picBytes == null || picBytes.Length < 1800) return null;
            //百度的最小512*512图片大小1819
            return new PicResult()
            {
                pic = picBytes,
                row = int.Parse(row),
                col = int.Parse(col),
                zoom = zoom
            };
        }

    }
    public class PicResult
    {
        public byte[] pic;
        public int row;
        public int col;
        public int zoom;
    }
}
