using NEL_Wallet_API.lib;
using Newtonsoft.Json.Linq;

namespace NEL_OnlineDebuger_API.Service.compile
{
    public class CompileFileService
    {
        private string serviceUrl = "";

        public CompileFileService(string url)
        {
            serviceUrl = url.EndsWith("/") ? url : url + "/";
        }

        public bool compileFile(string filename, string filetext, /*out string avmtext, out string abitext, out string maptext,*/ out string hash, out string code, out string message)
        {
            MyJson.JsonNode_Object req = new MyJson.JsonNode_Object();
            req.Add("jsonrpc", new MyJson.JsonNode_ValueString("2.0"));
            req.Add("method", new MyJson.JsonNode_ValueString("compileContractFile"));
            req.Add("params", new MyJson.JsonNode_Array { new MyJson.JsonNode_ValueString(filetext) });
            req.Add("id", new MyJson.JsonNode_ValueString("1"));
            string data = req.ToString();
            string resStr = RestHelper.RestPost(serviceUrl, data);

            JObject resJo = (JObject)((JArray)JObject.Parse(resStr)["result"])[0];
            hash = resJo["data"]["hash"].ToString();
            code = resJo["code"].ToString();
            message = resJo["message"].ToString();

            if (resJo["code"].ToString() == "0000")
            {
                //avmtext = resJo["data"]["avm"].ToString();
                //abitext = resJo["data"]["abi"].ToString();
                //maptext = resJo["data"]["map"].ToString();
                return true;
            }
            //avmtext = null;
            //abitext = null;
            //maptext = null;
            return false;
        }
    }
}
