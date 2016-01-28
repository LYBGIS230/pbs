using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PBS.Service;
using System.Drawing;
using System.IO;
using System.Data.SQLite;
using PBS.Util;
using System.Threading;

namespace PBS.DataSource
{
    public class DataSourceAdjustCoord : DataSourceCustomOnlineMaps
    {
        private Object _lockObj = new Object();
        private SQLiteConnection _outputconn;
        private SQLiteConnection _sourceConn;
        private string _sourceFile;
        public event ConvertEventHandler ConvertCompleted;
        public DataSourceAdjustCoord(string path)
        {
            ConvertingStatus = new ConvertStatus();
            _sourceFile = path;
            int lastPointIndex = path.LastIndexOf(".");
            string pre = path.Substring(0, lastPointIndex);
            string post = path.Substring(lastPointIndex, path.Length - lastPointIndex);
            _outputFile = pre + "_CC" + post;          
            _sourceConn = new SQLiteConnection("Data Source=" + path);
            _sourceConn.Open();
            ConvertingStatus = new ConvertStatus();
        }
        public string OutputFileName
        {
            get
            {
                return _outputFile;
            }
        }
        private byte[] getPNG(int level, int row, int col, object otherParam)
        {
            string commandText = string.Format("SELECT {0} FROM tiles WHERE tile_column={1} AND tile_row={2} AND zoom_level={3}", "tile_data", col, row, level);
            using (SQLiteCommand sqlCmd = new SQLiteCommand(commandText, _sourceConn))
            {
                object o = sqlCmd.ExecuteScalar();//null can not directly convert to byte[], if so, will return "buffer can not be null" exception
                if (o != null)
                {
                    return (byte[])o;
                }
                return null;
            }
        }
        private void getScope(int level, out int startR, out int startC, out int endR, out int endC)
        {
            string commandText = string.Format("SELECT rowStart,rowEnd,colStart,colEnd FROM scope WHERE zoom_level={0}", level);
            using (SQLiteCommand sqlCmd = new SQLiteCommand(commandText, _outputconn))
            {
                SQLiteDataReader reader = sqlCmd.ExecuteReader();
                reader.Read();
                startR = reader.GetInt32(0);
                endR = reader.GetInt32(1);
                startC = reader.GetInt32(2);
                endC = reader.GetInt32(3);
            }
        }
        private int getTotalCount()
        {
            int count = 0;
            string countStr;
            string commandText = "SELECT value FROM metadata WHERE name = 'TotalTileCount'";
            using (SQLiteCommand sqlCmd = new SQLiteCommand(commandText, _outputconn))
            {
                SQLiteDataReader reader = sqlCmd.ExecuteReader();
                reader.Read();
                countStr = reader.GetString(0);
            }
            if (countStr != null)
            {
                count = int.Parse(countStr);
            }
            return count;
        }
        public override byte[] GetTileBytes(int level, int row1, int col1)
        {
            byte[] rawPng = BaiDuMapManager.inst.getBaiduTile(this, level, row1, col1, null, getPNG);
            byte[] compressedPng = BaiDuMapManager.inst.compressPNG(rawPng);
            return compressedPng;
        }
        protected override void WriteTilesToSqlite(Dictionary<string, byte[]> dict)
        {
            int level = 0;
            lock (_lockObj)
            {
                //Utility.Log(LogLevel.Info, null, "Thread:" + Thread.CurrentThread.ManagedThreadId + " endted at " + DateTime.Now.Ticks);

                SQLiteTransaction transaction = _outputconn.BeginTransaction();
                try
                {
                    using (SQLiteCommand cmd = new SQLiteCommand(_outputconn))
                    {
                        foreach (KeyValuePair<string, byte[]> kvp in dict)
                        {
                            level = int.Parse(kvp.Key.Split(new char[] { '/' })[0]);
                            int col = int.Parse(kvp.Key.Split(new char[] { '/' })[1]);
                            int row = int.Parse(kvp.Key.Split(new char[] { '/' })[2]);
                            string guid = Guid.NewGuid().ToString();
                            cmd.CommandText = "INSERT INTO images VALUES (@tile_data,@tile_id)";
                            cmd.Parameters.AddWithValue("tile_data", kvp.Value);
                            cmd.Parameters.AddWithValue("tile_id", guid);
                            cmd.ExecuteNonQuery();
                            cmd.CommandText = "INSERT INTO map VALUES (@zoom_level,@tile_column,@tile_row,@tile_id)";
                            cmd.Parameters.AddWithValue("zoom_level", level);
                            cmd.Parameters.AddWithValue("tile_column", col);
                            cmd.Parameters.AddWithValue("tile_row", row);
                            cmd.Parameters.AddWithValue("tile_id", guid);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    transaction.Commit();
                }
                catch
                {
                    Utility.Log(LogLevel.Error, null, "Level: " + level + " write tiles to sqlite failed");
                }
            }
        }
        public override void ConvertToMBTiles(string outputPath, string name, string description, string attribution, int[] levels, Geometry geometry, bool doCompact)
        {
            _convertingStatus.IsInProgress = true;
            try
            {
                _outputconn = new SQLiteConnection("Data source = " + base._outputFile);
                _outputconn.Open();
                int startR, startC, endR, endC;
                _convertingStatus.TotalCount = getTotalCount();
                for (int i = 0; i < levels.Length; i++)
                {
                    getScope(levels[i], out startR, out startC, out endR, out endC);
                    _convertingStatus.Level = levels[i];
                    _convertingStatus.LevelTotalCount = (endR - startR + 1) * (endC - startC + 1);
                    _convertingStatus.LevelCompleteCount = _convertingStatus.LevelErrorCount = 0;
                    _levelCompleteCount = _levelErrorCount = 0;
                    SaveOneLevelTilesToMBTiles(levels[i], startR, startC, endR, endC);
                    Thread.Sleep(500);
                }
                if (ConvertCompleted != null)
                {
                    ConvertCompleted(this, new ConvertEventArgs(ConvertingStatus.IsCompletedSuccessfully));
                }
            }
            finally
            {
                _outputconn.Close();
                _outputconn.Dispose();
                _sourceConn.Close();
                _sourceConn.Dispose();
                _convertingStatus.IsInProgress = false;
            }
        }
    }
}
