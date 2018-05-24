using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Services;
using System.Web.Script.Serialization;
using System.Data;
using System.Data.SQLite;

namespace GIWS
{
    public static class AppEX
    {
        public static string ToJSON(this object obj)
        {
            return new JavaScriptSerializer().Serialize(obj);
        }

        public static string ToMD5(this string str)
        {
            return System.Web.Security.FormsAuthentication.HashPasswordForStoringInConfigFile(str, "md5").ToLower();
        }

        public static string QuotedStr(this string str)
        {
            string vResult;
            vResult = str;
            for (int i = vResult.Length - 1; i >= 0; i--)
            {
                if (vResult[i].Equals('\''))
                {
                    vResult = vResult.Insert(i, "\'");
                }
            }
            vResult = "'" + vResult + "'";
            return vResult;
        }


        public static SQLiteParameter SetValue(this SQLiteParameter pa, object obj)
        {
            pa.Value = obj;
            return pa;
        }

    }

    public class AppDB
    {
        private const string EmptyRow = "记录为空";
        private const string DataPath = @"bin\GIERP.Web.Service.db3";

        private static string LastError;
        private static int NOERROR = 0;

        #region 数据驱动

        private static string SqlConStr()
        {
            LastError = String.Empty;
            SQLiteConnectionStringBuilder csb = new SQLiteConnectionStringBuilder();
            csb.DataSource = System.Web.HttpContext.Current.Server.MapPath(DataPath);
            return csb.ConnectionString;
        }

        private static bool SqlExec(string sql, params SQLiteParameter[] pas)
        {
            using (SQLiteConnection cnn = new SQLiteConnection(SqlConStr()))
            {
                using (SQLiteCommand cmd = new SQLiteCommand(sql, cnn))
                {
                    try
                    {
                        cmd.Parameters.AddRange(pas);
                        cmd.Connection.Open();
                        cmd.ExecuteNonQuery();
                        return true;
                    }
                    catch (Exception e)
                    {
                        LastError = e.Message;
                        return false;
                    }
                }
            }
        }

        private static void SqlExec_Free_Up_Space()
        {
            try
            {
                SqlExec("VACUUM");
            }
            catch
            {
            }
        }

        private static DataTable SqlOpen(string sql, params SQLiteParameter[] pas)
        {
            using (SQLiteConnection cnn = new SQLiteConnection(SqlConStr()))
            {
                using (SQLiteCommand cmd = new SQLiteCommand(sql, cnn))
                {
                    using (SQLiteDataAdapter dpt = new SQLiteDataAdapter(cmd))
                    {
                        try
                        {
                            cmd.Parameters.AddRange(pas);
                            DataSet ds = new DataSet();
                            cmd.Connection.Open();
                            dpt.Fill(ds);
                            return ds.Tables[0];
                        }
                        catch (Exception e)
                        {
                            LastError = e.Message;
                            return null;
                        }
                    }
                }
            }
        }

        private static object SqlOpen_First_Cell(string sql, params SQLiteParameter[] pas)
        {
            using (SQLiteConnection cnn = new SQLiteConnection(SqlConStr()))
            {
                using (SQLiteCommand cmd = new SQLiteCommand(sql, cnn))
                {
                    try
                    {
                        cmd.Parameters.AddRange(pas);
                        cmd.Connection.Open();
                        return cmd.ExecuteScalar();
                    }
                    catch (Exception e)
                    {
                        LastError = e.Message;
                        return null;
                    }
                }
            }
        }

        #endregion

        #region 数据结构

        private class WebMsg
        {
            private int tag = -1;
            private int row = -1;
            private DateTime btm = DateTime.Now;
            private string msg = String.Empty;
            private string dat = String.Empty;

            public WebMsg()
            {
                LastError = String.Empty;
            }

            public int TAG { get { return this.tag; } set { this.tag = value; } }
            public int ROW { get { return this.row; } set { this.row = value; } }
            public double ZZZ { get { return System.Math.Round(DateTime.Now.Subtract(btm).TotalMilliseconds, 2); } }
            public string MSG { get { return this.msg; } set { this.msg = value; } }
            public string DAT { get { return this.dat; } set { this.dat = value; } }

            public void ResultOK(int rowcount = -1)
            {
                this.tag = 0;
                this.row = rowcount;
            }
            public void ResultNO(string errormsg = "")
            {
                this.tag = -1;
                this.row = -1;
                this.msg = LastError;
                if (!String.IsNullOrEmpty(errormsg))
                {
                    this.msg = errormsg;
                }
            }
        }

        private class AppMsg
        {
            public string UID { get; set; }
            public string PWD { get; set; }
            public string DAT { get; set; }
        }

        private class UserInfo
        {
            public string UserCode { get; set; }
            public string UserName { get; set; }
            public string UserPass { get; set; }
        }

        #endregion


        #region 关键函数

        private static T StrToJSON<T>(string str)
        {
            return new JavaScriptSerializer().Deserialize<T>(str);
        }

        private static T StrToJSONObject<T>(string str)
        {
            JavaScriptSerializer jss = new JavaScriptSerializer();
            return jss.ConvertToType<T>(jss.DeserializeObject(str));
        }

        private static bool LoadFromAppMsg(string appmsg, ref string appdat)
        {
            bool rc = false;
            AppMsg msg = new AppMsg();
            try
            {
                msg = StrToJSONObject<AppMsg>(appmsg);
                {
                    if (UserInfoChk(msg.UID, msg.PWD))
                    {
                        appdat = msg.DAT;
                        rc = true;
                    }
                }
            }
            catch (Exception e)
            {
                LastError = "无效的字符串";
            }
            return rc;
        }

        #endregion



        #region 用户数据

        private static bool UserInfoChk(string username, string userpass)
        {
            try
            {
                string sql = "select count(*) from userinfo where (username=@username) and (userpass=@userpass);";
                SQLiteParameter[] pas ={
                                       new SQLiteParameter("@username", SqlDbType.NText).SetValue(username.Trim()),
                                       new SQLiteParameter("@userpass", SqlDbType.NText).SetValue(userpass.Trim())
                                  };
                if (Convert.ToInt32(SqlOpen_First_Cell(sql, pas)) >= 1)
                {
                    return true;
                }
                else
                {
                    LastError = "无效的用户记录或登录密码错误";
                    return false;
                }
            }
            catch (Exception e)
            {
                LastError = e.Message;
                return false;
            }
        }

        public static string UserInfoDat(string appmsg)
        {
            WebMsg msg = new WebMsg();
            DataTable tb = new DataTable();
            tb = SqlOpen("select * from userinfo;");
            if ((tb == null) || (tb.Rows.Count == 0))
            {
                msg.ResultNO(EmptyRow);
            }
            else
            {
                List<UserInfo> list = new List<UserInfo>();
                foreach (DataRow dr in tb.Rows)
                {
                    UserInfo i = new UserInfo();
                    i.UserCode = dr["UserCode"].ToString();
                    i.UserName = dr["UserName"].ToString();
                    list.Add(i);
                }
                msg.DAT = list.ToJSON();
                msg.ResultOK(list.Count);

            }
            return msg.ToJSON();
        }

        public static string UserInfoNew(string appmsg)
        {
            WebMsg msg = new WebMsg();
            return msg.ToJSON();
        }

        public static string UserInfoDel(string appmsg)
        {
            WebMsg msg = new WebMsg();
            StringBuilder usercodelist = new StringBuilder();
            string appdat = String.Empty;
            if (!LoadFromAppMsg(appmsg, ref appdat))
            {
                msg.ResultNO();
                return msg.ToJSON();
            }

            bool isfirst = true;
            List<UserInfo> ls = new List<UserInfo>();
            ls = StrToJSON<List<UserInfo>>(appdat);
            foreach (UserInfo i in ls)
            {
                if (isfirst)
                {
                    isfirst = false;
                }
                else
                {
                    usercodelist.Append(",");
                }
                usercodelist.Append(i.UserCode.QuotedStr());
            }

            string sql = "delete from userinfo where (usercode in (@usercodelist));";
            SQLiteParameter[] dat = {
                                        new SQLiteParameter("@usercodelist", SqlDbType.NText).SetValue(usercodelist.ToString())
                                    };
            if (SqlExec(sql, dat))
            {
                msg.ResultOK();
                SqlExec_Free_Up_Space();
            }
            else
            {
                msg.ResultNO();
            }







            return msg.ToJSON();
        }


        #endregion

        #region



        #endregion

        #region



        #endregion

        #region



        #endregion

        #region



        #endregion
    }
}