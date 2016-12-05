using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PBS.Service;
using System.Data.SQLite;

namespace PBS.DataSource
{
    public class DataSourceMultiMBTiles : DataSourceBase
    {
        private String cachePath1 = "cache/gansu1.mbtiles";
        private String cachePath2 = "cache/gansu2.mbtiles";
        private SQLiteConnection _sqlConn1;
        private SQLiteConnection _sqlConn2;
        public DataSourceMultiMBTiles(String type, String subType)
        {
            _sqlConn1 = new SQLiteConnection("Data Source=" + cachePath1);
            _sqlConn1.Open();
            _sqlConn2 = new SQLiteConnection("Data Source=" + cachePath2);
            _sqlConn2.Open();
            this.Type = type;
            this.subType = subType;
            Initialize("N/A");
        }
        ~DataSourceMultiMBTiles()
        {
            if (_sqlConn1 != null)
                _sqlConn1.Close();
            _sqlConn1 = null;
            if (_sqlConn2 != null)
                _sqlConn2.Close();
            _sqlConn2 = null;
        }
        protected override void ReadTilingScheme(out TilingScheme tilingScheme)
        {
            tilingScheme = SchemaProvider.Inst.getSchema("OtherMap", this.subType, "default");
            this.TilingScheme = TilingSchemePostProcess(tilingScheme);
        }
        public override byte[] GetTileBytes(int level, int row, int col)
        {
            PBS.Util.Utility.LogSimple(PBS.Util.LogLevel.Debug, "P001, subType:" + this.subType + ", GetTileBytes called, level:" + level + ", row:" + row + ", col:" + col);
            if (String.Equals(this.subType, "GanSu"))
            {
                if (level <= 17)
                {
                    string commandText = string.Format("SELECT {0} FROM tiles WHERE tile_column={1} AND tile_row={2} AND zoom_level={3}", "tile_data", col, row, level);
                    PBS.Util.Utility.LogSimple(PBS.Util.LogLevel.Debug, "P001, " + commandText);
                    using (SQLiteCommand sqlCmd = new SQLiteCommand(commandText, _sqlConn1))
                    {
                        object o = sqlCmd.ExecuteScalar();//null can not directly convert to byte[], if so, will return "buffer can not be null" exception
                        if (o != null)
                        {
                            return (byte[])o;
                        }
                        PBS.Util.Utility.LogSimple(PBS.Util.LogLevel.Debug, "P001, SQL result is empty, returned null");
                        return null;
                    }
                }
                else if(level == 18)
                {
                    string commandText = string.Format("SELECT {0} FROM tiles WHERE tile_column={1} AND tile_row={2}", "tile_data", col, row);
                    PBS.Util.Utility.LogSimple(PBS.Util.LogLevel.Debug, "P001, " + commandText);
                    using (SQLiteCommand sqlCmd = new SQLiteCommand(commandText, _sqlConn2))
                    {
                        try
                        {
                            object o = sqlCmd.ExecuteScalar();//null can not directly convert to byte[], if so, will return "buffer can not be null" exception
                            if (o != null)
                            {
                                return (byte[])o;
                            }
                        }
                        catch (Exception e)
                        {
                            PBS.Util.Utility.LogSimple(PBS.Util.LogLevel.Error, "P001, " + e.StackTrace);
                        }
                        
                        PBS.Util.Utility.LogSimple(PBS.Util.LogLevel.Debug, "P001, SQL result is empty, returned null");
                        return null;
                    }
                }
            }
            return null;
        }
    }
}
