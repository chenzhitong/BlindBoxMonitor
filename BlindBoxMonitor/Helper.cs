using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BlindBoxMonitor
{
    public static class Helper
    {
        public static string PostWebRequest(string postUrl, string paramData)
        {
            try
            {
                byte[] byteArray = Encoding.UTF8.GetBytes(paramData);
                WebRequest webReq = WebRequest.Create(postUrl);
                webReq.Method = "POST";
                webReq.ContentType = "application/json";
                using (Stream newStream = webReq.GetRequestStream())
                {
                    newStream.Write(byteArray, 0, byteArray.Length);
                }
                using WebResponse response = webReq.GetResponse();
                using StreamReader sr = new(response.GetResponseStream(), Encoding.UTF8);
                return sr.ReadToEnd();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return "";
        }
    }
}
