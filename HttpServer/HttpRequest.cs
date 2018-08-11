using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DF_FaceTracking.cs.HttpServer
{
    public class HttpRequest : BaseHeader
    {
        /// <summary>
        /// URL参数
        /// </summary>
        public Dictionary<string, string> Params { get; private set; }

        /// <summary>
        /// HTTP请求方式
        /// </summary>
        public string Method { get; private set; }

        /// <summary>
        /// HTTP(S)地址
        /// </summary>
        public string URL { get; set; }

        /// <summary>
        /// HTTP协议版本
        /// </summary>
        public string ProtocolVersion { get; set; }

        /// <summary>
        /// 定义缓冲区
        /// </summary>
        private const int MAX_SIZE = 1024 * 1024 * 2;
        private byte[] bytes = new byte[MAX_SIZE];

        public ILogger Logger { get; set; }

        private Stream handler;

        public HttpRequest(Stream stream)
        {
            this.handler = stream;
            var data = GetRequestData(handler);
            var rows = Regex.Split(data, Environment.NewLine);

            //Request URL & Method & Version
            var first = Regex.Split(rows[0], @"(\s+)")
                .Where(e => e.Trim() != string.Empty)
                .ToArray();
            if (first.Length > 0) this.Method = first[0];
            if (first.Length > 1) this.URL = Uri.UnescapeDataString(first[1]).Split('?')[0];
            if (first.Length > 2) this.ProtocolVersion = first[2];

            //Request Headers
            this.Headers = GetRequestHeaders(rows);

            //Request "GET"
            if (this.Method == "GET")
            {
                this.Body = GetRequestBody(rows);
                var isUrlencoded = Uri.UnescapeDataString(first[1]).Contains('?');
                if (isUrlencoded) this.Params = GetRequestParameters(Uri.UnescapeDataString(first[1]).Split('?')[1]);
            }

            //Request "POST"
            if (this.Method == "POST")
            {
                //post请求的参数取自body
                this.Body = GetRequestBody(rows);
                Console.WriteLine(string.Format("body {0}",this.Body));
                var contentType = GetHeader(RequestHeaders.ContentType);
                var isUrlencoded = contentType == @"application/x-www-form-urlencoded";
                if (isUrlencoded)
                {
                    if (!this.Body.Equals(""))
                    {
                        string bodyContent = this.Body;
                        string fileContent = "";
                        string token = "";
                        //确保Json字符串完整，如果不完整，那么就舍弃该次
                        if (bodyContent.Contains("}"))
                        {
                            try
                            {
                                JObject jo = (JObject)JsonConvert.DeserializeObject(bodyContent);
                                fileContent = jo["fileContent"].ToString();
                                token = jo["token"].ToString();
                                first[1] += ("&fileContent=" + fileContent);
                                first[1] += ("&token=" + token);
                            }
                            catch (Exception e)
                            {
                                WriteLog.WriteError(e.ToString());
                                throw;
                            }
                        }
                        else {
                            first[1] += ("&fileContent=" + "undefined");
                            first[1] += ("&token=" + "undefined");
                        }

                    }
                    this.Params = GetRequestParameters(Uri.UnescapeDataString(first[1]).Split('?')[1]);
                } 
            }
        }

        public Stream GetRequestStream()
        {
            return this.handler;
        }

        public string GetHeader(RequestHeaders header)
        {
            return GetHeaderByKey(header);
        }

        public string GetHeader(string fieldName)
        {
            return GetHeaderByKey(fieldName);
        }

        public void SetHeader(RequestHeaders header, string value)
        {
            SetHeaderByKey(header, value);
        }

        public void SetHeader(string fieldName, string value)
        {
            SetHeaderByKey(fieldName, value);
        }

        private string GetRequestData(Stream stream)
        {
            var length = 0;
            var data = string.Empty;
            //read heahers
            do
            {
                data +=  (char)stream.ReadByte();
            } while (!data.EndsWith("\r\n\r\n"));
            //read body
            string contentLenHeader = "Content-Length: ";
            if(!data.Contains(contentLenHeader))
            {
                return data;
            }
            int startIndex = data.IndexOf(contentLenHeader)+contentLenHeader.Length;
            int endIndex = data.IndexOf('\r',startIndex);
            int contentLen = int.Parse( data.Substring(startIndex, endIndex-startIndex));
            if (contentLen == 0) return data;
            byte[] buffer = new byte[contentLen];
            int currentIndex = 0;
            do
            {
                currentIndex += stream.Read(buffer, currentIndex, contentLen - currentIndex);
            } while (currentIndex < contentLen);
            data+= Encoding.UTF8.GetString(buffer, 0, contentLen);
            return data;
        }

        private string GetRequestBody(IEnumerable<string> rows)
        {
            var target = rows.Select((v, i) => new { Value = v, Index = i }).FirstOrDefault(e => e.Value.Trim() == string.Empty);
            if (target == null) return null;
            var range = Enumerable.Range(target.Index + 1, rows.Count() - target.Index - 1);
            return string.Join(Environment.NewLine, range.Select(e => rows.ElementAt(e)).ToArray());
        }

        private Dictionary<string, string> GetRequestHeaders(IEnumerable<string> rows)
        {
            if (rows == null || rows.Count() <= 0) return null;
            var target = rows.Select((v, i) => new { Value = v, Index = i }).FirstOrDefault(e => e.Value.Trim() == string.Empty);
            var length = target == null ? rows.Count() - 1 : target.Index;
            if (length <= 1) return null;
            var range = Enumerable.Range(1, length - 1);
            return range.Select(e => rows.ElementAt(e)).ToDictionary(e => e.Split(':')[0], e => e.Split(':')[1].Trim());
        }

        private Dictionary<string, string> GetRequestParameters(string row)
        {
            if (string.IsNullOrEmpty(row)) return null;
            var kvs = Regex.Split(row, "&");
            if (kvs == null || kvs.Count() <= 0) return null;

            return kvs.ToDictionary(e => Regex.Split(e, "=")[0], e => Regex.Split(e, "=")[1]);
        }
    }
}
