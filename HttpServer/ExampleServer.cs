using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DF_FaceTracking.cs.File;
using System.IO;
using System.Reflection;

namespace DF_FaceTracking.cs.HttpServer
{
    class ExampleServer : HttpServer
    {/// <summary>
     /// 构造函数
     /// </summary>
     /// <param name="ipAddress">IP地址</param>
     /// <param name="port">端口号</param>
        public ExampleServer(string ipAddress, int port)
            : base(ipAddress, port)
        {

        }
        private PXCMSession session;
        private MainForm mainForm;
        //用于判断get请求的次数,状态0代表未发送get请求，1表示发送了一次get请求，2表示启动了客户端，3表示启动了客户端并且关闭了摄像头
        int state = 0;
        string CurState = "";
        StreamWriter sw_interaction;
        Stream interactionFeature;
        //文件名
        private string realName;
        public override void OnPost(HttpRequest request, HttpResponse response)
        {
            //获取客户端传递的参数
            string data = request.Params["type"];
            //构造响应报文
            response.Content_Encoding = "utf-8";
            response.StatusCode = "200";
            response.Headers = new Dictionary<string, string>();
            response.SetHeader(ResponseHeaders.Allow, "*");
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Credentials", "true");
            response.Headers.Add("Access-Control-Allow-Methods", "*");
            response.Headers.Add("Access-Control-Allow-Headers", "*");
            response.Headers.Add("Access-Control-Expose-Headers", "*");
            response.Content_Type = "application/json";
            byte[] buffer = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { success = "true", msg = "提交成功" }));
            if (data.Equals("start") && mainForm != null)
            {
                realName = request.Params["fileName"];
                mainForm.StartClick(realName);
                CurState = "Start";
                //发送响应
                buffer = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { success = "true", msg = "摄像头启动成功" }));
                response.SetContent(buffer);
                response.Send();
            }
            else if (data.Equals("stop") && mainForm != null && CurState.Equals("Start"))
            {
                state = 3;
                CurState = "Stop";
                mainForm.StopClick();
                //发送响应
                string fileContent = request.Params["fileContent"];
                string token = request.Params["token"];
                this.Log(string.Format("fileContent {0} token {1}", fileContent, token));
                if (fileContent.Equals("undefined") && token.Equals("undefined"))
                {
                    buffer = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { success = "true", msg = "摄像头关闭成功", flag = "false" }));
                    response.SetContent(buffer);
                    response.Send();
                }
                else {
                    if (realName != null && fileContent != null)
                    {
                        string FileNameSuffix = "study_" + realName;
                        string interactionSuffix = "interaction_" + realName;
                        interactionFeature = new FileStream(string.Format("{0}{1}", Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "\\" + interactionSuffix + ".txt"), FileMode.Append);
                        sw_interaction = new StreamWriter(interactionFeature);
                        sw_interaction.BaseStream.Seek(0, SeekOrigin.End);
                        try
                        {
                            sw_interaction.Write(fileContent);
                        }
                        catch (Exception e)
                        {
                            WriteLog.WriteError(e.ToString());
                            throw;
                        }
                        finally
                        {
                            sw_interaction.Close();
                            interactionFeature.Close();
                        }
                        if (UploadFile.Upload(FileNameSuffix, Environment.CurrentDirectory + "\\" + FileNameSuffix + ".txt", token) == true)
                        {
                            WriteLog.WriteError(FileNameSuffix + "上传成功");
                        }
                        else
                        {
                            WriteLog.WriteError(FileNameSuffix + "上传失败");
                        }
                        if (UploadFile.Upload(interactionSuffix, Environment.CurrentDirectory + "\\" + interactionSuffix + ".txt", token) == true)
                        {
                            WriteLog.WriteError(interactionSuffix + "上传成功");
                        }
                        else
                        {
                            WriteLog.WriteError(interactionSuffix + "上传失败");
                        }
                        buffer = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { success = "true", msg = "摄像头关闭成功", flag = "true" }));
                        response.SetContent(buffer);
                        response.Send();
                    }
                }
                
            }
            else {
                buffer = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { success = "false", msg = "提交失败" }));
                response.SetContent(buffer);
                response.Send();
            }
        }
        //系统一定要先执行get请求
        public override void OnGet(HttpRequest request, HttpResponse response)
        {
            response.StatusCode = "200";
            response.Headers = new Dictionary<string, string>();
            response.SetHeader(ResponseHeaders.Allow, "*");
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Credentials","true");
            response.Headers.Add("Access-Control-Allow-Methods", "*");
            response.Headers.Add("Access-Control-Allow-Headers", "*");
            response.Headers.Add("Access-Control-Expose-Headers", "*");
            response.Content_Type = "application/json";
            response.Encoding = Encoding.UTF8;
            byte[] buffer = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { success = "true", msg = "打开成功" }));
            response.SetContent(buffer);
            response.Body = JsonConvert.SerializeObject(new { success = "true", msg = "打开成功" });
            response.Headers.Add("Content-Length",""+buffer.Length);
            //文件名由前端传进来,默认文件名合法
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            session = PXCMSession.CreateInstance();
            //if (session!=null&&mainForm == null) {
            //    mainForm = new MainForm(session);
            //    state = 1;
            //    mainForm.response = response;
            //}
            //if (session != null&&(state!=2&&state!=3))
            //{
            //    state = 2;
            //   // Application.Run(mainForm);
            //    mainForm.Invoke(new Action(() => mainForm.Show()));
            //    session.Dispose();
            //}
            if (mainForm != null) {
                if(mainForm.IsHandleCreated){
                    mainForm.Invoke(new Action(() => {
                        mainForm.Visible = false;
                    } ));
                }
            }
            mainForm = new MainForm(session);
            mainForm.response = response;
            if (session != null) {
                mainForm.StartPosition = FormStartPosition.CenterScreen;
                Application.Run(mainForm);
                session.Dispose();
            }
        }
    }
}
