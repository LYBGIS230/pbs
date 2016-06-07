using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SQLite;
using System.IO;
using PBS.Util;
using System.Net;

namespace PBS.Service
{
    public class CacheVersionProvider
    {
        protected static readonly object _locker = new object();
        public class MBVersion
        {
            public string name;
            public long start;
            public long end;
            public long version;
            public long threadCount;
            public string status;
            public double timeSpan;
            public long completeCount;
            public long totalCount;
            public long receivedBytes;
            public long wroteBytes;
            public double networkSpeed;
            public long wroteCounts;
            public MBVersion next;

        };
        public void mergeWithFile(string fileName)
        {
            cache.mergeWithFile(fileName);
        }
        public void RecordDownloadRecord(MBVersion version, string downloadRule)
        {
            lock (_locker)
            {
                cache.insertOneVersion(version, downloadRule);
            }
        }
        class MemoryCache
        {
            private long baseline_version;
            private long baseline_time;
            private MBVersion current;
            private int versionCount = 0;
            private MBVersion last;
            public MBVersion head = new MBVersion() { version = 0 };
            public override string ToString()
            {
                string result = "Last Version is: " + (last == null ? "null" : last.version.ToString()) + ". [\n";
                int seq = 0;
                MBVersion temp = head;
                while (temp != last)
                {
                    result += "{ \"seq\" : " + seq + ", \"version\" : " + temp.version + ", \"start\" : " + temp.start + ", \"end\" : " + temp.end + "},\n";
                    temp = temp.next;
                    seq = seq + 1;
                }
                result += "{ \"seq\" : " + seq + ", \"version\" : " + temp.version + ", \"start\" : " + temp.start + ", \"end\" : " + temp.end + "}\n]";
                return result;
            }
            public MBVersion getVersionFromTime(long timeStamp)
            {
                MBVersion result = null, temp;
                temp = head;
                if (last.start <= timeStamp && last.end >= timeStamp)
                {
                    return last;
                }
                while (temp != last)
                {
                    if (temp.start <= timeStamp && temp.end >= timeStamp)
                    {
                        result = temp;
                        break;
                    }
                    temp = temp.next;
                }
                return result;
            }
            public void exportToNewFile()
            {
                if (!File.Exists("merge.mbtiles"))
                {
                    SQLiteConnection.CreateFile("merge.mbtiles");
                    SQLiteConnection conn = new SQLiteConnection("Data source = merge.mbtiles");
                    conn.Open();
                    using (SQLiteTransaction transaction = conn.BeginTransaction())
                    {
                        using (SQLiteCommand cmd = new SQLiteCommand(conn))
                        {
                            cmd.CommandText = "CREATE TABLE TimeNameMap (filename TEXT, startTime INT8, endTime INT8, version INTEGER, threadCount INTEGER, convertStatus TEXT, timeElapsed REAL, tileCompleteCount INTEGER, tileTotalCount INTEGER, totalReceivedBytes INTEGER, totalWroteBytes INTEGER, netWorkSpeed REAL, totalWroteCount INTEGER)";
                            cmd.ExecuteNonQuery();
                            for (int i = 0; i < versionCount; i++)
                            {
                                cmd.CommandText = "INSERT INTO TimeNameMap VALUES (@filename, @startTime, @endTime, @version, @threadCount, @convertStatus, @timeElapsed, @tileCompleteCount, @tileTotalCount, @received, @wrote, @speed, @wroteCount)";
                                cmd.Parameters.AddWithValue("filename", current.name);
                                cmd.Parameters.AddWithValue("startTime", current.start);
                                cmd.Parameters.AddWithValue("endTime", current.end);
                                cmd.Parameters.AddWithValue("version", current.version);
                                cmd.Parameters.AddWithValue("threadCount", current.threadCount);
                                cmd.Parameters.AddWithValue("convertStatus", current.status);
                                cmd.Parameters.AddWithValue("timeElapsed", current.timeSpan);
                                cmd.Parameters.AddWithValue("tileCompleteCount", current.completeCount);
                                cmd.Parameters.AddWithValue("tileTotalCount", current.totalCount);
                                cmd.Parameters.AddWithValue("received", current.receivedBytes);
                                cmd.Parameters.AddWithValue("wrote", current.wroteBytes);
                                cmd.Parameters.AddWithValue("speed", current.networkSpeed);
                                cmd.Parameters.AddWithValue("wroteCount", current.wroteCounts);
                                current = current.next;
                                cmd.ExecuteNonQuery();
                            }
                        }
                        transaction.Commit();
                        transaction.Dispose();
                    }
                }
            }
            private void breakCircle()
            {
                MBVersion temp = new MBVersion() { version = 0 };
                temp.next = head;
                head = temp;
                last.next = null;
            }
            private void joinCircle()
            {
                last = head;
                while (last.next != null)
                {
                    last = last.next;
                }
                last.next = head.next;
                head = head.next;
                current = head;
            }
            public void setPredictBaseLine(long version, long time)
            {
                baseline_time = time;
                baseline_version = version;
            }
            delegate long[] arrangeArea(long allCand, long currentNum, long wholeStart, long wholeEnd);
            private void forcecastVersionTime(MBVersion previousVersion, MBVersion currentVersion, SQLiteConnection versionConn)
            {
                arrangeArea timeSplit = (gap, seq, start, end) =>
                {
                    long parts = gap - 1;
                    long timeTotal = end - start;
                    long avg = timeTotal / parts;
                    long another = timeTotal % parts;
                    long[] result = new long[2];
                    long startSeq = seq - 1, endSeq = seq;
                    result[0] = start + (avg + 1) * (startSeq < another ? startSeq : another) + avg * (startSeq < another ? 0 : startSeq - another);
                    result[1] = start + (avg + 1) * (endSeq < another ? endSeq : another) + avg * (endSeq < another ? 0 : endSeq - another) - (gap - seq == 1 ? 0 : 1);
                    return result;
                };
                long wholeStart = 0;
                long wholeEnd = 0;
                long allGap = 0;
                long currentGap = 0;
                long versionSpan = 0;
                MBVersion nextVersion = currentVersion.next;
                if (previousVersion != null && previousVersion.version != 0)
                {
                    if (nextVersion != null && nextVersion.version != 0)
                    {
                        allGap = nextVersion.version - previousVersion.version;
                        currentGap = currentVersion.version - previousVersion.version;
                        versionSpan = (nextVersion.start - previousVersion.start) / allGap;
                        wholeStart = previousVersion.start + versionSpan;
                        wholeEnd = nextVersion.start - 1;
                    }
                    else
                    {
                        allGap = currentVersion.version - previousVersion.version + 1;
                        currentGap = currentVersion.version - previousVersion.version;
                        wholeStart = previousVersion.start + 55 * 60000;
                        wholeEnd = wholeStart + 55 * 60000 * (currentVersion.version - previousVersion.version);
                    }
                }
                else if (nextVersion != null && nextVersion.version != 0)
                {
                    allGap = nextVersion.version - currentVersion.version + 1;
                    currentGap = 1;
                    wholeStart = nextVersion.start - 55 * 60000 * (nextVersion.version - currentVersion.version);
                    wholeEnd = nextVersion.start - 1;
                }
                else
                {
                    allGap = 2;
                    currentGap = 1;
                    wholeStart = baseline_time - (baseline_version - currentVersion.version) * 55 * 60000;
                    wholeEnd = wholeStart + 55 * 60000 - 1;
                }
                long[] sectionResult = timeSplit(allGap, currentGap, wholeStart, wholeEnd);
                currentVersion.start = sectionResult[0];
                currentVersion.end = sectionResult[1];
                string lastVersion = "";
                using (SQLiteCommand cmd = new SQLiteCommand(versionConn))
                {
                    cmd.CommandText = "UPDATE TimeNameMap SET startTime = @startTime, endTime = @endTime WHERE version = @version";
                    cmd.Parameters.AddWithValue("startTime", sectionResult[0]);
                    cmd.Parameters.AddWithValue("endTime", sectionResult[1]);
                    lastVersion = currentVersion.version.ToString();
                    cmd.Parameters.AddWithValue("version", lastVersion);
                    cmd.ExecuteNonQuery();
                }
                if (previousVersion.version == long.Parse(lastVersion) - 1)
                {
                    previousVersion.end = sectionResult[0] - 1;
                }
            }
            public void insertOneVersion(MBVersion toAdd, string downloadRule)
            {
                breakCircle();
                MBVersion previous = addOneVersion(toAdd);
                if (previous != null)
                {
                    using (SQLiteConnection conn = new SQLiteConnection("Data source = versions.db"))
                    {
                        conn.Open();
                        SQLiteTransaction recordTransaction = conn.BeginTransaction();
                        using (SQLiteCommand cmd = new SQLiteCommand(conn))
                        {
                            cmd.CommandText = "INSERT INTO TimeNameMap VALUES (@filename, @startTime, @endTime, @version, @threadCount, @convertStatus, @timeElapsed, @tileCompleteCount, @tileTotalCount, @totalReceivedBytes, @totalWroteBytes, @netWorkSpeed, @totalWroteCount)";
                            cmd.Parameters.AddWithValue("filename", toAdd.name);
                            cmd.Parameters.AddWithValue("startTime", toAdd.start);
                            cmd.Parameters.AddWithValue("endTime", toAdd.end);
                            cmd.Parameters.AddWithValue("version", toAdd.version);
                            cmd.Parameters.AddWithValue("threadCount", toAdd.threadCount);
                            cmd.Parameters.AddWithValue("convertStatus", toAdd.status);
                            cmd.Parameters.AddWithValue("timeElapsed", toAdd.timeSpan);
                            cmd.Parameters.AddWithValue("tileCompleteCount", toAdd.completeCount);
                            cmd.Parameters.AddWithValue("tileTotalCount", toAdd.totalCount);
                            cmd.Parameters.AddWithValue("totalReceivedBytes", toAdd.receivedBytes);
                            cmd.Parameters.AddWithValue("totalWroteBytes", toAdd.wroteBytes);
                            cmd.Parameters.AddWithValue("netWorkSpeed", toAdd.networkSpeed);
                            cmd.Parameters.AddWithValue("totalWroteCount", toAdd.wroteCounts);
                            cmd.ExecuteNonQuery();
                        }
                        if (BaiDuMapManager.inst.RunMode == "ONLINE" && downloadRule == "BYROUND")
                        {
                            forcecastVersionTime(previous, toAdd, conn);
                        }

                        fixPriorVersionEnd(previous, toAdd, conn);
                        fixLatestVersionEnd(conn);
                        recordTransaction.Commit();
                        recordTransaction.Dispose();
                        conn.Close();
                    }
                }
                joinCircle();
            }
            private void fixPriorVersionEnd(MBVersion previousVersion, MBVersion currentVersion, SQLiteConnection versionConn)
            {
                if (previousVersion.version == currentVersion.version - 1)
                {
                    previousVersion.end = currentVersion.start - 1;
                    using (SQLiteCommand cmd = new SQLiteCommand(versionConn))
                    {
                        cmd.CommandText = "UPDATE TimeNameMap SET endTime = @endTime WHERE version = @version";
                        cmd.Parameters.AddWithValue("endTime", previousVersion.end);
                        cmd.Parameters.AddWithValue("version", previousVersion.version.ToString());
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            private void fixLatestVersionEnd(SQLiteConnection versionConn)
            {
                last.end = last.start + 300 * 60000 - 1;
                using (SQLiteCommand cmd = new SQLiteCommand(versionConn))
                {
                    cmd.CommandText = "UPDATE TimeNameMap SET endTime = @endTime WHERE version = @version";
                    cmd.Parameters.AddWithValue("endTime", last.end);
                    cmd.Parameters.AddWithValue("version", last.version.ToString());
                    cmd.ExecuteNonQuery();
                }
            }
            public void mergeWithFile(string fileName)
            {
                SQLiteConnection conn2 = new SQLiteConnection("Data source = " + fileName);
                conn2.Open();
                breakCircle();
                loadDataFormDB(conn2);
                exportToNewFile();
                conn2.Close();
                conn2.Dispose();
            }
            public void clearInvalid(long earlisetTime)
            {
                breakCircle();
                MBVersion temp = head.next;
                while (temp != null)
                {
                    if (temp.end < earlisetTime)
                    {
                        head.next = temp.next;
                        temp.next = null;
                    }
                    temp = temp.next;
                }
                joinCircle();
                return;
            }
            private MBVersion addOneVersion(MBVersion toAdd)
            {
                MBVersion temp = head;
                while (temp.next != null)
                {
                    if (temp.next.version > toAdd.version) break;
                    temp = temp.next;
                }
                if (temp.version == toAdd.version) return null;
                else if (temp.version < toAdd.version)
                {
                    toAdd.next = temp.next;
                    temp.next = toAdd;
                    versionCount++;
                    if (toAdd.next == null)
                    {
                        last = toAdd;
                    }
                }
                return temp;
            }
            public void loadDataFormDB(SQLiteConnection connection)
            {
                using (SQLiteCommand cmd = new SQLiteCommand(connection))
                {
                    cmd.CommandText = "SELECT startTime, endTime, version, filename, threadCount, convertStatus, timeElapsed, tileCompleteCount, tileTotalCount, totalReceivedBytes, totalWroteBytes, netWorkSpeed, totalWroteCount FROM TimeNameMap order by version DESC";
                    SQLiteDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        current = new MBVersion()
                        {
                            start = reader.GetInt64(0),
                            end = reader.GetInt64(1),
                            version = reader.GetInt64(2),
                            name = reader.GetString(3),
                            threadCount = reader.GetInt64(4),
                            status = reader.GetString(5),
                            timeSpan = reader.GetDouble(6),
                            completeCount = reader.GetInt64(7),
                            totalCount = reader.GetInt64(8),
                            receivedBytes = reader.GetInt64(9),
                            wroteBytes = reader.GetInt64(10),
                            networkSpeed = reader.GetDouble(11),
                            wroteCounts = reader.GetInt64(12)
                        };
                        addOneVersion(current);
                    }
                    joinCircle();
                }
            }
            public void reloadDataFromDB()
            {
                breakCircle();
                MBVersion temp = head.next;
                while (temp != null)
                {
                    head.next = temp.next;
                    temp.next = null;
                    temp = head.next;
                }
                versionCount = 0;
                using (SQLiteConnection _conn = new SQLiteConnection("Data source = versions.db"))
                {
                    _conn.Open();
                    loadDataFormDB(_conn);
                    _conn.Close();
                }
            }
            public MemoryCache()
            {
                using (SQLiteConnection _conn = new SQLiteConnection("Data source = versions.db"))
                {
                    _conn.Open();
                    loadDataFormDB(_conn);
                }
            }
            public void AdjustTime()
            {
                using (SQLiteConnection conn = new SQLiteConnection("Data source = versions.db"))
                {
                    conn.Open();
                    SQLiteTransaction recordTransaction = conn.BeginTransaction();
                    using (SQLiteCommand cmd = new SQLiteCommand(conn))
                    {
                        cmd.CommandText = "UPDATE TimeNameMap SET startTime = startTime + @count * @interval, endTime = endTime + @count * @interval  WHERE version = @version";
                        cmd.Parameters.AddWithValue("version", current.version);
                        cmd.Parameters.AddWithValue("count", versionCount);
                        cmd.Parameters.AddWithValue("interval", BaiDuMapManager.inst.refreshInterval);
                        cmd.ExecuteNonQuery();
                    }
                    recordTransaction.Commit();
                    recordTransaction.Dispose();
                    current.start += versionCount * BaiDuMapManager.inst.refreshInterval;
                    current.end += versionCount * BaiDuMapManager.inst.refreshInterval;
                    Utility.Log(LogLevel.Info, null, "Adjusted version: " + current.version + ", versionCount is: " + versionCount);
                    current = current.next;
                }
            }
        }
        public static int currentVersion = 0;
        public static int arrangedVersion = 0;
        private string parameterFile = "versions.db";
        private MemoryCache cache;
        public void AdjustTime()
        {
            cache.AdjustTime();
        }
        public string getCurrentVersion()
        {
            string versionStr = "0";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(System.Web.HttpUtility.UrlDecode("http://spotshot.baidu.com/getVersion.php"));
            request.Method = "GET";
            request.ContentType = "text/html;charset=UTF-8";
            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                StreamReader reader = new System.IO.StreamReader(response.GetResponseStream(), Encoding.UTF8);
                versionStr = reader.ReadToEnd();
            }
            catch (Exception e)
            {
                Utility.Log(LogLevel.Error, e, "Get map version failed, set verison to zero!");
                return versionStr;
            }
            int startPosition = versionStr.IndexOf(":") + 2;
            return versionStr.Substring(startPosition, versionStr.Length - 2 - startPosition);
        }
        public void initVersionFormWeb()
        {
            currentVersion = Convert.ToInt32(getCurrentVersion());
            arrangedVersion = getLastDownload() + 1;
            if (cache != null)
            {
                cache.setPredictBaseLine(currentVersion, BaiDuMapManager.inst.ConvertDateTimeLong(DateTime.Now));
            }
        }
        public string getLastVersion()
        {
            string result = "";
            result += currentVersion;
            return result;
        }
        public string getFileNameFromVersion(int version)
        {
            string result = "";
            result += version.ToString();
            result += "-" + Guid.NewGuid().ToString() + ".mbtiles";
            return result;
        }
        public string getLastFileName()
        {
            string result = "";
            result += getLastVersion();
            result += "-" + Guid.NewGuid().ToString() + ".mbtiles";
            return result;
        }
        ~CacheVersionProvider()
        {
        }
        public string getCacheFile(string time)
        {
            MBVersion version = cache.getVersionFromTime(long.Parse(time));
            return version == null ? null : version.name;
        }
        public string getVersionFromTime(string time)
        {
            MBVersion version = cache.getVersionFromTime(long.Parse(time));
            return version == null ? null : version.version.ToString();
        }
        public string getCahcheFileFromTime(string time)
        {
            string result = null;
            string commandText = string.Format("SELECT filename FROM TimeNameMap WHERE startTime <= {0} AND endTime >= {0} ", time);
            using (SQLiteConnection conn = new SQLiteConnection("Data source = versions.db"))
            {
                conn.Open();
                using (SQLiteCommand sqlCmd = new SQLiteCommand(commandText, conn))
                {
                    object o = sqlCmd.ExecuteScalar();
                    string fileName = o as string;
                    result = fileName;
                }
            }
            return result;
        }
        public int getLastDownload()
        {
            int version = -1;
            using (SQLiteConnection conn = new SQLiteConnection("Data source = versions.db"))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(conn))
                {
                    SQLiteDataReader reader;
                    cmd.CommandText = "select count(*) from TimeNameMap";
                    reader = cmd.ExecuteReader();
                    reader.Read();
                    int versionCount = reader.GetInt32(0);
                    reader.Close();
                    if (versionCount > 0)
                    {
                        cmd.CommandText = "SELECT startTime, endTime, max(version) FROM TimeNameMap";
                        reader = cmd.ExecuteReader();
                        reader.Read();
                        version = reader.GetInt32(2);
                        reader.Close();
                    }
                    else
                    {
                        version = currentVersion > 0? currentVersion - 200 : 0;
                    }
                }
            }
            return version;
        }
        public string getLatestVersionFormDB(string startTime, string endTime)
        {
            int recordSerial = 0;
            string result = "{\"versions\" : ["; ;
            using (SQLiteConnection conn = new SQLiteConnection("Data source = versions.db"))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(conn))
                {
                    cmd.CommandText = string.Format(
                        "SELECT startTime, endTime, version FROM TimeNameMap where startTime >= {0} and startTime <= {1} or endTime >= {0} and endTime <= {1} order by startTime",
                        startTime,
                        endTime);
                    SQLiteDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        long start = reader.GetInt64(0);
                        long end = reader.GetInt64(1);
                        long version = reader.GetInt64(2);
                        if (recordSerial > 0)
                        {
                            result += ",";
                        }
                        result += "{\"Version\" :" + version;
                        result += ", \"start\" : " + start;
                        result += ", \"end\" : " + end + "}";
                        recordSerial++;
                    }
                    result += "], \"length\" :" + recordSerial + "}";
                }
            }
            return result;
        }
        public void resetVersions()
        {
            File.Delete(parameterFile);
            createParameterFile();
            cache.reloadDataFromDB();
            initVersionFormWeb();
        }
        private void createParameterFile()
        {
            SQLiteConnection.CreateFile(parameterFile);
            using (SQLiteConnection conn = new SQLiteConnection("Data source = " + parameterFile))
            {
                conn.Open();
                using (SQLiteTransaction transaction = conn.BeginTransaction())
                {
                    using (SQLiteCommand cmd = new SQLiteCommand(conn))
                    {
                        cmd.CommandText = "CREATE TABLE TimeNameMap (filename TEXT, startTime INT8, endTime INT8, version INTEGER, threadCount INTEGER, convertStatus TEXT, timeElapsed REAL, tileCompleteCount INTEGER, tileTotalCount INTEGER, totalReceivedBytes INTEGER, totalWroteBytes INTEGER, netWorkSpeed REAL, totalWroteCount INTEGER)";
                        cmd.ExecuteNonQuery();
                    }
                    transaction.Commit();
                    transaction.Dispose();
                }
            }
        }
        public CacheVersionProvider()
        {
            if (BaiDuMapManager.inst.RunMode == "ONLINE")
            {
                if (!File.Exists(parameterFile))
                {
                    createParameterFile();
                    initVersionFormWeb();
                }
                else
                {
                    initVersionFormWeb();
                    if (arrangedVersion > currentVersion + 1)
                    {
                        File.Delete(parameterFile);
                        createParameterFile();
                    }
                }
            }
            cache = new MemoryCache();
        }
    }
}
