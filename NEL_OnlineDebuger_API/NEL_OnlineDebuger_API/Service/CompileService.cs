using NEL_OnlineDebuger_API.lib;
using NEL_Wallet_API.Controllers;
using Newtonsoft.Json.Linq;
using System;

namespace NEL_OnlineDebuger_API.Service
{
    public class CompileService
    {
        public mongoHelper mh { set; get; }
        public string notify_mongodbConnStr { get; set; }
        public string notify_mongodbDatabase { get; set; }
        public string compilefileCol { get; set; } = "onlineDebuger_compilefile";
        public OssFileService ossClient { get; set; }
        public CompileDebugger debugger = new CompileDebugger();

        public JArray compileFile(string address, string filetext)
        {
            // 编译文件
            byte[] avmtext = null;
            string abitext = null;
            string maptext = null;
            string hash = null;
            bool flag = false;
            try
            {
                flag = debugger.compile(null, filetext, out avmtext, out abitext, out maptext, out hash);
            } catch (Exception ex)
            {
                return new JArray() { new JObject() { { "code", "1001" }, { "message", ex.Message }, { "result", new JObject() { { "hash", hash } } } } };
            }
            
            // 入临时库
            string findStr = new JObject() { {"addr", address }, { "hash", hash } }.ToString();
            if (mh.GetDataCount(notify_mongodbConnStr, notify_mongodbDatabase, compilefileCol, findStr) == 0)
            {
                long nowtime = TimeHelper.GetTimeStamp();
                string newdata = new JObject() {
                    { "addr",address },
                    { "hash", hash},
                    { "state", "0" }, // 
                    { "cs",filetext },
                    { "avm", avmtext},
                    { "abi", abitext},
                    { "map", maptext },
                    { "error", "" },
                    { "createTime",  nowtime},
                    { "lastUpdateTime", nowtime },
                }.ToString();
                mh.InsertOneData(notify_mongodbConnStr, notify_mongodbDatabase, compilefileCol, newdata);
            }
            return new JArray(){ new JObject() { { "code", "0000"}, { "message", "编译成功"}, {"result", new JObject() { {"hash", hash } } } }};
        }

        public JArray deployContract()
        {
            // 部署合约
            return null;
        }

        public JArray getCompileResult(string filehash)
        {
            string findStr = new JObject() { {"filehash", filehash } }.ToString();
            var query = mh.GetData(notify_mongodbConnStr, notify_mongodbDatabase, compilefileCol, findStr);
            if(query == null || query.Count == 0)
            {
                return new JArray() { new JObject() { { "code", "1001" }, { "message", "无效文件哈希" },{ "result","[]"} } };
            }
            string state = query[0]["state"].ToString();
            string error = query[0]["error"].ToString();
            if (state == "0")
            {
                return new JArray() { new JObject() { { "code", "1002" }, { "message", "等待编译" }, { "result", "[]" } } };
            }
            if (state == "1")
            {
                return new JArray() { new JObject() { { "code", "0000" }, { "message", "编译成功" }, { "result", getCompileFile(filehash) } } };
            }
            
            return new JArray() { new JObject() { { "code", "1003" }, { "message", error }, { "result", "[]" } } }; // 编译失败
        }
        
        public JArray saveContractFile(string address, string hash)
        {
            // 保存合约文件
            string findStr = new JObject() { { "address", address},{"hash", hash }, { "state", "0" } }.ToString();
            var query = mh.GetData(notify_mongodbConnStr, notify_mongodbDatabase, compilefileCol, findStr);
            if(query == null || query.Count == 0)
            {
                return new JArray() { new JObject() { { "code", "1001" }, { "message", "没有找到文件" }, { "result", "[]" } } };
            }

            string cs = query[0]["cs"].ToString();
            string avm = query[0]["avm"].ToString();
            string abi = query[0]["abi"].ToString();
            string map = query[0]["map"].ToString();
            ossClient.OssFileUpload(hash + ".cs", avm);
            ossClient.OssFileUpload(hash + ".avm", avm);
            ossClient.OssFileUpload(hash + ".abi.json", abi);
            ossClient.OssFileUpload(hash + ".map.json", map);
            return new JArray() { new JObject() { { "code", "0000" }, { "message", "保存成功" }, { "result", "[]" } } };
        }
        public JArray getCompileFile(string hash)
        {
            // 获取合约文件
            string pathScript = "";
            string str_avm = ossClient.OssFileDownLoad(System.IO.Path.Combine(pathScript, hash + ".avm"));
            string str_cs = ossClient.OssFileDownLoad(System.IO.Path.Combine(pathScript, hash + ".cs"));
            var JO_map = (MyJson.IJsonNode)MyJson.Parse(ossClient.OssFileDownLoad(System.IO.Path.Combine(pathScript, hash + ".map.json")));
            var JO_abi = (MyJson.IJsonNode)MyJson.Parse(ossClient.OssFileDownLoad(System.IO.Path.Combine(pathScript, hash + ".abi.json")));
            JObject JO_result = new JObject();
            JO_result["avm"] = str_avm;
            JO_result["cs"] = str_cs;
            JO_result["map"] = JO_map.ToString();
            JO_result["abi"] = JO_abi.ToString();
            return new JArray() { JO_result };
        }
    }
}
