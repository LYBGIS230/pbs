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
        int childCount = 5;
        private EventWaitHandle[] _download;
        private int jobCount;
        private int finishedJobCount;
        protected bool allJobArrived;
        string[] _messages;
        WorkerBase[] downloaders;
        bool[] downloaderStatus;

        readonly object _tasklocker = new object();
        Queue<object> _tasks = new Queue<object>();
        private EventWaitHandle _consumer = new AutoResetEvent(false);
        private Thread _consumerThread;

        readonly object _errorlocker = new object();
        Queue<object> _errorInfo = new Queue<object>();
        private EventWaitHandle _errorHandler = new AutoResetEvent(false);
        private Thread _errorRecordThread;
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
                    currentIndex = 0;
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
        private void afterJobFinished(object param)
        {
            if (param != null)
            {
                finishedJobCount++;
                if (jobCount == finishedJobCount)
                {
                    allJobArrived = true;
                }
                EnqueueTask(param);
                onWorkFinished(param);
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

            _errorRecordThread = new Thread(RecordError);
            _errorRecordThread.Start();
        }
        public void EnqueueTask(object task)
        {
            lock (_tasklocker) _tasks.Enqueue(task);
            _consumer.Set();
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

        private void DoRecordError(object e)
        {
        }

        private void RecordError()
        {
            while (true)
            {
                object error = null;
                lock (_errorlocker)
                {
                    if (_errorInfo.Count > 0)
                    {
                        error = _errorInfo.Dequeue();
                    }
                }
                if (error != null)
                {
                    DoRecordError(error);
                }
                else
                {
                    _errorHandler.WaitOne();
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
        private System.Timers.Timer _timer = new System.Timers.Timer(1500);
        private double secondEclipsed = 0.0;

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
        bool[] indicators;
        byte[] finalPic;
        ManualResetEvent manlEvent = new ManualResetEvent(false);
        public MultiPicDownloadDispatcher(int childThreadNum)
            : base(childThreadNum)
        {
            this.AllJobFinished += (count) =>
            {
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
                        if (picBytes == null) continue;
                        part = new Bitmap(new MemoryStream(picBytes));
                        destPic.DrawImage(part, panangle * 512, tiltangle * 512);
                        part.Dispose();
                    }
                }
                map.Save(output, System.Drawing.Imaging.ImageFormat.Jpeg);
                finalPic = output.ToArray();
                manlEvent.Set();
                Utility.LogSimple(LogLevel.Debug,  "AllJobFinished and set called");
            };
            _timer.Elapsed += (s, a) =>
            {
                int len = indicators.Length;
                secondEclipsed += 1.5;
                if (secondEclipsed < 3.5)
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
                                    RunJob(panOid + "," + udt + "," + zoom + "," + p[1] + "," + p[0]);
                                }
                            }
                        });
                        recoverThread.Start();
                    }
                    else
                    {
                        _timer.Stop();
                        if (secondEclipsed == 1.5)
                        {
                            Utility.LogSimple(LogLevel.Debug, "OK, no need to recover");
                        }
                        else
                        {
                            Utility.LogSimple(LogLevel.Debug, "Recoverred in time and timer stop called");
                        }
                        return;
                    }
                }
                else
                {
                    _timer.Stop();
                    Utility.LogSimple(LogLevel.Debug, "Time out and timer stop called");
                    if (!isIdle)
                    {
                        isIdle = true;
                        setTotalJobCount(9999);
                        finalPic = new byte[0];
                        manlEvent.Set();
                        Utility.LogSimple(LogLevel.Debug, "Time out and set called");
                    }
                }
            };
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
                resultBuffer[r.row * (int)Math.Pow(2, r.zoom) + r.col] = r.pic;
                indicators[r.row * (int)Math.Pow(2, r.zoom) + r.col] = true;
            }
            return null;
        }
        public override void Run()
        {
            isIdle = false;
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
            Utility.LogSimple(LogLevel.Debug, "Timer started");
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
            request.Timeout = 20000;
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
            if (picBytes == null) return null;
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
