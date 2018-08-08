using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DF_FaceTracking.cs.File
{
    
    class StateTable
    {
        //SQLite3表内容：id(char)，state(int), createtime(datetime), updatetime(datetime), token(varchar);

        private static SQLiteConnection sqlc = null;
        private static String dbName = ConfigReader.GetConfigValue("dbName");
        private static String dbPath= ConfigReader.GetConfigValue("dbPath");

        //表名称
        public static readonly String TABLE_VIDEO = "videorecord";
        public static readonly String TABLE_PICTURE = "picturerecord";

        //状态表
        public static readonly int STATE_CREATE = 0;
        public static readonly int STATE_TRANSCODE = 1;
        public static readonly int STATE_UPLOAD = 2;
        public static readonly int STATE_COMPLETE = 3;

        public struct RecordInfo
        {
            public String ID;
            public String token;
        }

        public static void createState(String tableName, String ID, String token)
        {
            checkConnection();
            String time = DateTime.Now.ToLocalTime().ToString();
            String sql = "insert into " + tableName + " values ('" + ID + "'," + STATE_CREATE + ",'" + time + "','" + time + "','" + token + "')";
            SQLiteCommand cmd = new SQLiteCommand(sql, sqlc);
            cmd.ExecuteNonQuery();
        }

        public static void updateState(String tableName, String ID, int state)
        {
            checkConnection();
            String time = DateTime.Now.ToLocalTime().ToString();
            String sql = "update "+ tableName + " set state=" + state + ", updatetime='" + time + "' where id='" + ID + "'";
            SQLiteCommand cmd = new SQLiteCommand(sql, sqlc);
            cmd.ExecuteNonQuery();
        }

        public static ArrayList getStateLists(String tableName, int state)
        {
            checkConnection();
            ArrayList al = new ArrayList();
            String sql = "select id, token from " + tableName+ " where state=" + state;
            SQLiteCommand command = new SQLiteCommand(sql, sqlc);
            SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            { 
                RecordInfo iteminfo;
                iteminfo.ID = reader["id"].ToString();
                iteminfo.token = reader["token"].ToString();
                al.Add(iteminfo);
            }
            return al;
        }
        private static void checkConnection()
        {
            if (sqlc == null)
                sqlc = new SQLiteConnection("Data Source=" + dbPath + dbName);
            sqlc.Open();
            
        }
    }
}

