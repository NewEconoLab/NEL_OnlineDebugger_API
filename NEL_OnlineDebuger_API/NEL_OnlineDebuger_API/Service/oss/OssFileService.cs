using NEL_Wallet_API.lib;

namespace NEL_Wallet_API.Controllers
{
    public class OssFileService
    {
        private string serviceUrl = "http://localhost:52083/api/testnet";
        private string uploadPath = "oss/upload";
        private string downloadPath = "oss/download";
        private string storePath = "oss/store";

        public OssFileService(string url)
        {
            serviceUrl = url.EndsWith("/") ? url : url + "/";
        }
        public string OssFileUpload(string fileName, string fileContent)
        {
            //string data = "{'fileName':'"+fileName+"','fileContent':'"+fileContent+"'}";
            MyJson.JsonNode_Object req = new MyJson.JsonNode_Object();
            req.Add("fileName", new MyJson.JsonNode_ValueString(fileName));
            req.Add("fileContent", new MyJson.JsonNode_ValueString(fileContent));
            string data = req.ToString();
            OssRes res = getRes(RestHelper.RestPost(serviceUrl + uploadPath, data));
            return res.data;
        }
        public string OssFileDownLoad(string fileName)
        {
            //string data = "{'fileName':'" + fileName + "'}";
            MyJson.JsonNode_Object req = new MyJson.JsonNode_Object();
            req.Add("fileName", new MyJson.JsonNode_ValueString(fileName));
            string data = req.ToString();
            OssRes res = getRes(RestHelper.RestPost(serviceUrl + downloadPath, data));
            return res.data ;
        }

        public string OssFileStore(string fileName)
        {
            //string data = "{'fileName':'" + fileName + "'}";
            MyJson.JsonNode_Object req = new MyJson.JsonNode_Object();
            req.Add("fileName", new MyJson.JsonNode_ValueString(fileName));
            string data = req.ToString();
            OssRes res = getRes(RestHelper.RestPost(serviceUrl + storePath, data));
            return res.data;
        }

        private OssRes getRes(string resp)
        {
            Newtonsoft.Json.Linq.JObject res = Newtonsoft.Json.Linq.JObject.Parse(resp);
            return new OssRes
            {
                code = res["code"].ToString(),
                errMsg = res["errMsg"].ToString(),
                data = res["data"].ToString()
            };

            /*
            MyJson.IJsonNode res = MyJson.Parse(resp);
            return new OssRes
            {
                code = res.Get("code").AsString(),
                errMsg = res.Get("errMsg").AsString(),
                data = res.Get("data").AsString()
            };*/
        }

        private class OssRes
        {
            public string code { get; set; }
            public string errMsg { get; set; }
            public string data { get; set; }
        }
    }
}
