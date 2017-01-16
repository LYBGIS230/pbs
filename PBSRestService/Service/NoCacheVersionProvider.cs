using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;

namespace PBS.Service
{
    class NoCacheVersionProvider : IDisposable
    {
        private string filepath;
        private SQLiteConnection connection;
        private SQLiteCommand cmd;

        public NoCacheVersionProvider(string filepath)
        {
            this.filepath = filepath;
            connection = new SQLiteConnection("Data source = " + filepath);
            connection.Open();
            cmd = new SQLiteCommand(connection);
            cmd.CommandText = "SELECT filename from TimeNameMap where startTime < @timestamp order by startTime desc";
        }

        public string GetTileFile(long timestamp)
        {
            cmd.Parameters.AddWithValue("timestamp", timestamp);
            SQLiteDataReader reader = cmd.ExecuteReader();
            if(reader.Read())
            {
                return reader.GetString(0);
            }
            return null;
        }

        public void Dispose()
        {
            connection.Close();
            connection.Dispose();
        }
    }
}
