using DF_FaceTracking.cs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DF_FaceTracking.cs.File
{
    class UploadFile
    {
        
        private static String uploadUrl = ConfigReader.GetConfigValue("upload_Url");

        //这两个目前不用
        private static String username = ConfigReader.GetConfigValue("upload_username");
        private static String password = ConfigReader.GetConfigValue("upload_password");

        //private static String testToken = "1d44662ee7e0452a9d594841799d00ac";
        public static bool Upload(String filename, String filepath, String token)
        {
            String newUrl = uploadUrl + "?filename=" + filename + "&token=" + token;
            using (WebClient client = new WebClient())
            {
                try
                {
                    String response = Encoding.Default.GetString(client.UploadFile(newUrl, filepath));
                    if (response.Contains("success"))//上传成功
                        return true;
                    else//上传重名文件
                        return false;
                }
                catch(Exception e)
                {
                    WriteLog.WriteError(e.ToString());
                    return false;
                }        
            }
        }


        //这个方法目前也不用
        //UploadFile.upload(@"http://192.168.0.153:8080/file/upload", @"C:\Users\73582\Desktop\Demo\PreviewDemo\video\T12.mp4","T15.mp4");
        public static bool upload(string address, string fileNamePath, string saveName)
        {
            string param = "filename=" +  saveName + "&file=";
            byte[] paramArray = System.Text.Encoding.Default.GetBytes(param);

            FileStream fs = new FileStream(fileNamePath, FileMode.Open, FileAccess.Read);
            BinaryReader r = new BinaryReader(fs);
            byte[] fileArray = r.ReadBytes((int)fs.Length);

            List<byte> byteSource = new List<byte>();
            byteSource.AddRange(paramArray);
            byteSource.AddRange(fileArray);
            byte[] postArray = byteSource.ToArray();

            // 根据uri创建HttpWebRequest对象  
            HttpWebRequest httpReq = (HttpWebRequest)WebRequest.Create(new Uri(address));
            httpReq.Method = "POST";
            //httpReq.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", username, password))));
            httpReq.Credentials = CredentialCache.DefaultCredentials;
            httpReq.ContentType = "multipart/form-data";
            httpReq.AllowWriteStreamBuffering = false;
            httpReq.Timeout = 300000;
            httpReq.ContentLength = postArray.Length;

            Stream postStream = httpReq.GetRequestStream();
            try
            {

                postStream.Write(postArray, 0, postArray.Length);
                //postStream.Flush();
                postStream.Close();
                HttpWebResponse webRespon = (HttpWebResponse)httpReq.GetResponse();
           
                if (webRespon.StatusCode == HttpStatusCode.Created || webRespon.StatusCode == HttpStatusCode.NoContent)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e2)

            {
                Console.Write(e2);
                return false;
            }
            finally
            {
                fs.Close();
                r.Close();
            }
        }
        /*
        public static async Task<System.IO.Stream> Upload(string url, string filepath, string filename)
        {
            // Convert each of the three inputs into HttpContent objects
            FileStream fileStream = new FileStream(filepath, FileMode.Open, FileAccess.Read);
            HttpContent stringContent = new StringContent(filename);
            // examples of converting both Stream and byte [] to HttpContent objects
            // representing input type file
            HttpContent fileStreamContent = new StreamContent(fileStream);
            //HttpContent bytesContent = new ByteArrayContent(fileBytes);

            // Submit the form using HttpClient and 
            // create form data as Multipart (enctype="multipart/form-data")

            using (var client = new HttpClient())
      
            using (var formData = new MultipartFormDataContent())
            {
                // Add the HttpContent objects to the form data

                // <input type="text" name="filename" />
                formData.Add(stringContent, "filename");
                // <input type="file" name="file1" />
                formData.Add(fileStreamContent, "file");
                // <input type="file" name="file2" />
                //formData.Add(bytesContent, "file2", "file2");

                // Invoke the request to the server

                // equivalent to pressing the submit button on
                // a form with attributes (action="{url}" method="post")
                var response = await client.PostAsync(url, formData);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }
                return await response.Content.ReadAsStreamAsync();
            }
        }
        */
    }



    //带进度条
    /*
      private int Upload_Request(string address, string fileNamePath, string saveName, ProgressBar progressBar)  
        {  
            int returnValue = 0;  
  
            // 要上传的文件  
            FileStream fs = new FileStream(fileNamePath, FileMode.Open, FileAccess.Read);  
            BinaryReader r = new BinaryReader(fs);  
  
            //时间戳  
            string strBoundary = "----------" + DateTime.Now.Ticks.ToString("x");  
            byte[] boundaryBytes = Encoding.ASCII.GetBytes("/r/n--" + strBoundary + "/r/n");  
  
            //请求头部信息  
            StringBuilder sb = new StringBuilder();  
            sb.Append("--");  
            sb.Append(strBoundary);  
            sb.Append("/r/n");  
            sb.Append("Content-Disposition: form-data; name=/"");  
            sb.Append("file");  
            sb.Append("/"; filename=/"");  
            sb.Append(saveName);  
            sb.Append("/"");  
            sb.Append("/r/n");  
            sb.Append("Content-Type: ");  
            sb.Append("application/octet-stream");  
            sb.Append("/r/n");  
            sb.Append("/r/n");  
            string strPostHeader = sb.ToString();  
            byte[] postHeaderBytes = Encoding.UTF8.GetBytes(strPostHeader);  
  
            // 根据uri创建HttpWebRequest对象  
            HttpWebRequest httpReq = (HttpWebRequest)WebRequest.Create(new Uri(address));  
            httpReq.Method = "POST";  
  
            //对发送的数据不使用缓存  
            httpReq.AllowWriteStreamBuffering = false;  
  
            //设置获得响应的超时时间（300秒）  
            httpReq.Timeout = 300000;  
            httpReq.ContentType = "multipart/form-data; boundary=" + strBoundary;  
            long length = fs.Length + postHeaderBytes.Length + boundaryBytes.Length;  
            long fileLength = fs.Length;  
            httpReq.ContentLength = length;  
            try  
            {  
                progressBar.Maximum = int.MaxValue;  
                progressBar.Minimum = 0;  
                progressBar.Value = 0;  
  
                //每次上传4k  
                int bufferLength = 4096;  
                byte[] buffer = new byte[bufferLength];  
  
                //已上传的字节数  
                long offset = 0;  
  
                //开始上传时间  
                DateTime startTime = DateTime.Now;  
                int size = r.Read(buffer, 0, bufferLength);  
                Stream postStream = httpReq.GetRequestStream();  
  
                //发送请求头部消息  
                postStream.Write(postHeaderBytes, 0, postHeaderBytes.Length);  
                while (size > 0)  
                {  
                    postStream.Write(buffer, 0, size);  
                    offset += size;  
                    progressBar.Value = (int)(offset * (int.MaxValue / length));  
                    TimeSpan span = DateTime.Now - startTime;  
                    double second = span.TotalSeconds;  
                    lblTime.Text = "已用时：" + second.ToString("F2") + "秒";  
                    if (second > 0.001)  
                    {  
                        lblSpeed.Text = " 平均速度：" + (offset / 1024 / second).ToString("0.00") + "KB/秒";  
                    }  
                    else  
                    {  
                        lblSpeed.Text = " 正在连接…";  
                    }  
                    lblState.Text = "已上传：" + (offset * 100.0 / length).ToString("F2") + "%";  
                    lblSize.Text = (offset / 1048576.0).ToString("F2") + "M/" + (fileLength / 1048576.0).ToString("F2") + "M";  
                    Application.DoEvents();  
                    size = r.Read(buffer, 0, bufferLength);  
                }  
                //添加尾部的时间戳  
                postStream.Write(boundaryBytes, 0, boundaryBytes.Length);  
                postStream.Close();  
  
                //获取服务器端的响应  
                WebResponse webRespon = httpReq.GetResponse();  
                Stream s = webRespon.GetResponseStream();  
                StreamReader sr = new StreamReader(s);  
  
                //读取服务器端返回的消息  
                String sReturnString = sr.ReadLine();  
                s.Close();  
                sr.Close();  
                if (sReturnString == "Success")  
                {  
                    returnValue = 1;  
                }  
                else if (sReturnString == "Error")  
                {  
                    returnValue = 0;  
                }  
  
            }  
            catch  
            {  
                returnValue = 0;  
            }  
            finally  
            {  
                fs.Close();  
                r.Close();  
            }  
  
            return returnValue;  
        }  
     */
}
