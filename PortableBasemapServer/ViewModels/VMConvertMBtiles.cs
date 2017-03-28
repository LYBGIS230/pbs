using PBS.APP.Classes;
using PBS.DataSource;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Windows.Forms;
using System.Windows.Input;

namespace PBS.APP.ViewModels
{
    public class VMConvertMBtiles : INotifyPropertyChanged
    {
        public class MockDataSource
        {
            public ConvertStatus ConvertingStatus;
        }

        private double _percent = 0.0;

        public double TotalPercent
        {
            get { return _percent; }
            set
            {
                _percent = value;
                NotifyPropertyChanged(p => p.Percent);
                NotifyPropertyChanged(p => p.TotalPercent);
            }
        }
        private bool _buttonEnable = true;
        public bool IsMergeButtonEnable
        {
            get { return _buttonEnable; }
            set
            {
                _buttonEnable = value;
                NotifyPropertyChanged(p => p.IsMergeButtonEnable);
            }
        }

        public string Percent
        {
            get { return _percent.ToString() + "%"; }
        }
        public object _dataSource;
        public object Datasource
        {
            get { return _dataSource; }
            set
            {
                _dataSource = value;
                NotifyPropertyChanged(p => p.Datasource);
            }
        }
        public ConvertStatus ConvertingStatus;
        public VMConvertMBtiles()
        {
            CMDClickStartButton = new DelegateCommand(StartButtonClicked);
        }
        public event PropertyChangedEventHandler PropertyChanged;
        public ICommand CMDClickStartButton { get; private set; }
        private string _FilePath;
        private string _srcData;
        private void NotifyPropertyChanged<TValue>(Expression<Func<VMConvertMBtiles, TValue>> propertySelector)
        {
            if (PropertyChanged == null)
                return;

            var memberExpression = propertySelector.Body as MemberExpression;
            if (memberExpression == null)
                return;

            PropertyChanged(this, new PropertyChangedEventArgs(memberExpression.Member.Name));
        }
        /// <summary>
        /// binding to text of TilingScheme stack panel
        /// </summary>
        public string FileToCorrect
        {
            get { return _FilePath; }
            set
            {
                _FilePath = value;
                NotifyPropertyChanged(p => p._FilePath);
            }
        }
        public string DateToFetch
        {
            get { return _srcData; }
            set
            {
                _srcData = value;
                NotifyPropertyChanged(p => p._srcData);
            }
        }
        private void WriteTilesToSqlite(int level, int col, int row, byte[] data, SQLiteCommand cmd)
        {
            string guid = Guid.NewGuid().ToString();
            cmd.CommandText = "INSERT INTO images VALUES (@tile_data,@tile_id)";
            cmd.Parameters.AddWithValue("tile_data", data);
            cmd.Parameters.AddWithValue("tile_id", guid);
            cmd.ExecuteNonQuery();
            cmd.CommandText = "INSERT INTO map VALUES (@zoom_level,@tile_column,@tile_row,@tile_id)";
            cmd.Parameters.AddWithValue("zoom_level", level);
            cmd.Parameters.AddWithValue("tile_column", col);
            cmd.Parameters.AddWithValue("tile_row", row);
            cmd.Parameters.AddWithValue("tile_id", guid);
            cmd.ExecuteNonQuery();
        }
        private string mergeBounds(string boundsA, string boundsB)
        {
            string result = "";
            string minX, minY, maxX, maxY;
            string[] partsA = boundsA.Split(',');
            string[] partsB = boundsB.Split(',');
            if(partsA != null && partsA.Length == 4 && partsB != null && partsB.Length ==4)
            {
                minX = double.Parse(partsA[0]) < double.Parse(partsB[0]) ? partsA[0] : partsB[0];
                minY = double.Parse(partsA[1]) < double.Parse(partsB[1]) ? partsA[1] : partsB[1];
                maxX = double.Parse(partsA[2]) > double.Parse(partsB[2]) ? partsA[2] : partsB[2];
                maxY = double.Parse(partsA[3]) > double.Parse(partsB[3]) ? partsA[3] : partsB[3];
                result = minX + "," + minY + "," + maxX + "," + maxY;
            }
            return result;
        }
        private void StartButtonClicked(object parameters)
        {

            string param = parameters.ToString();
            if (param == "START")
            {
                DataSourceAdjustCoord transferSource = new DataSourceAdjustCoord(DateToFetch, FileToCorrect);
                Datasource = transferSource;
                ConvertingStatus = transferSource.ConvertingStatus;
                NotifyPropertyChanged(p => p.ConvertingStatus);
                (transferSource as DataSourceAdjustCoord).ConvertCompleted += (s1, a1) =>
                {
                    string str1 = App.Current.FindResource("msgAdjustComplete").ToString();
                    MessageBox.Show(str1, "", MessageBoxButtons.OK, MessageBoxIcon.Information);
                };

                BackgroundWorker bw = new BackgroundWorker();
                bw.DoWork += (s, a) =>
                {
                    transferSource.ConvertToMBTiles(FileToCorrect, "", "", "", new int[] { 11,12 }, null, false);
                };
                bw.RunWorkerAsync();
            }
            else if (param == "MERGE")
            {
                IsMergeButtonEnable = false;
                BackgroundWorker bw = new BackgroundWorker();
                bw.RunWorkerCompleted += (s, a) =>
                {
                    MessageBox.Show("转换完成", "", MessageBoxButtons.OK, MessageBoxIcon.Information);
                };
                bw.DoWork += (s, a) =>
                {
                    Datasource = new MockDataSource();
                    ConvertingStatus = new ConvertStatus();
                    ((MockDataSource)Datasource).ConvertingStatus = ConvertingStatus;
                    NotifyPropertyChanged(p => p.ConvertingStatus);

                    List<int> levels = new List<int>();
                    int recordCount = 0;
                    int totalCount = 0;
                    string boundA = "", boundB = "";
                    SQLiteConnection _outputconn = new SQLiteConnection("Data source = " + FileToCorrect);
                    _outputconn.Open();
                    SQLiteConnection _inputconn = new SQLiteConnection("Data source = " + DateToFetch);
                    _inputconn.Open();
                    ConvertingStatus.IsInProgress = true;
                    using (SQLiteTransaction writeTran = _outputconn.BeginTransaction())
                    {
                        SQLiteCommand readCommand = new SQLiteCommand(_inputconn);
                        SQLiteCommand writeCommand = new SQLiteCommand(_outputconn);
                        readCommand.CommandText = "select value from metadata where name = 'bounds'";
                        SQLiteDataReader reader = readCommand.ExecuteReader();
                        if (reader.Read())
                        {
                            boundA = reader.GetString(0);
                        }
                        reader.Close();

                        writeCommand.CommandText = "select value from metadata where name = 'bounds'";
                        reader = writeCommand.ExecuteReader();
                        if (reader.Read())
                        {
                            boundB = reader.GetString(0);
                        }
                        reader.Close();




                        readCommand.CommandText = "select count(*) from tiles";
                        reader = readCommand.ExecuteReader();
                        if (reader.Read())
                        {
                            totalCount = reader.GetInt32(0);
                        }
                        ConvertingStatus.TotalCount = totalCount;
                        reader.Close();
                        readCommand.CommandText = "select distinct(zoom_level) from tiles order by zoom_level";
                        reader = readCommand.ExecuteReader();
                        while (reader.Read())
                        {
                            levels.Add(reader.GetInt32(0));
                        }
                        reader.Close();


                        foreach (int level in levels)
                        {
                            readCommand.CommandText = "select count(*) from tiles where zoom_level = " + level;
                            reader = readCommand.ExecuteReader();
                            reader.Read();
                            int levelCount = reader.GetInt32(0);
                            int levelCompleted = 0;
                            ConvertingStatus.Level = level;
                            ConvertingStatus.LevelTotalCount = levelCount;
                            ConvertingStatus.LevelCompleteCount = ConvertingStatus.LevelErrorCount = 0;
                            reader.Close();
                            readCommand.CommandText = "select zoom_level, tile_column, tile_row, tile_data from tiles where zoom_level = " + level;
                            reader = readCommand.ExecuteReader();
                            while (reader.Read())
                            {
                                int col = reader.GetInt32(1);
                                int row = reader.GetInt32(2);
                                byte[] data = (byte[])reader.GetValue(3);
                                try
                                {
                                    WriteTilesToSqlite(level, col, row, data, writeCommand);
                                }
                                catch (Exception e)
                                {

                                }
                                finally
                                {
                                    levelCompleted++;
                                    recordCount++;
                                    ConvertingStatus.LevelCompleteCount = levelCompleted;
                                    ConvertingStatus.CompleteCount = recordCount;
                                    TotalPercent = ConvertingStatus.CompletePercent;
                                }
                            }
                            reader.Close();
                        }
                        writeCommand.CommandText = "update metadata set value = @bound where name = 'bounds'";
                        writeCommand.Parameters.AddWithValue("bound", mergeBounds(boundA, boundB));
                        writeCommand.ExecuteNonQuery();

                        writeTran.Commit();
                        writeTran.Dispose();
                    }
                    ConvertingStatus.IsInProgress = false;
                    _inputconn.Close();
                    _inputconn.Dispose();
                    _outputconn.Close();
                    _outputconn.Dispose();
                };
                bw.RunWorkerAsync();
            }
            else if (param == "START_GOOGLE")
            {

            }
        }
    }
}
