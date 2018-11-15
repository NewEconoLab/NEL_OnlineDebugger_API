using NEL_OnlineDebuger_API.lib;
using NEL_Wallet_API.Controllers;
using NEL_Wallet_API.lib;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;

namespace NEL_OnlineDebuger_API.Service
{
    public class CommonService
    {
        public mongoHelper mh { set; get; }
        public string block_mongodbConnStr { get; set; }
        public string block_mongodbDatabase { get; set; }
        public string notify_mongodbConnStr { get; set; }
        public string notify_mongodbDatabase { get; set; }
        public string neoCliJsonRPCUrl { get; set; }
        public string txCallContractCol { get; set; } = "onlineDebuger_txCallContract";
        
        public JArray getNotifyByTxid(string txid)
        {
            txid = format(txid);
            string findStr = new JObject() { { "txid", txid } }.ToString();
            return mh.GetData(block_mongodbConnStr, block_mongodbDatabase, "ApplicationLogs", findStr);
        }
        public JArray getDumpInfoByTxid(string txid)
        {
            txid = format(txid);
            string findStr = new JObject() { {"txid",txid } }.ToString();
            return mh.GetData(block_mongodbConnStr, block_mongodbDatabase, "DumpInfos", findStr);
        }
        public JArray getTxCallContract(string address)
        {
            string findStr = new JObject() { {"address", address } }.ToString();
            string fieldStr = new JObject() { {"txid",1 },{ "createTime",1 }}.ToString();
            var res = mh.GetDataWithField(notify_mongodbConnStr, notify_mongodbDatabase, txCallContractCol, fieldStr, findStr);
            return res;
        }
        public JArray txCallContract(string address, string txhex)
        {
            var res = sendrawtransaction(neoCliJsonRPCUrl, txhex);
            if(!(bool)res["sendrawtransactionresult"])
            {
                return new JArray { res };
            }
            string txid = res["txid"].ToString();
            long nowtime = TimeHelper.GetTimeStamp();
            
            // 成功保存记录
            string newdata = new JObject() {
                {"address", address },
                {"txid", txid },
                {"createTime", nowtime }
            }.ToString();
            mh.InsertOneData(notify_mongodbConnStr, notify_mongodbDatabase, txCallContractCol, newdata);
            return new JArray() { res };
        }

        public JArray getUtxoBalance(string address, string assetid = "")
        {
            JObject findJo = new JObject() { { "addr", address },{ "used",""} };
            if(assetid != null && assetid != "")
            {
                findJo.Add("asset", format(assetid));
            }
            string findStr = findJo.ToString();
            string fieldStr = new JObject() {{ "asset", 1 },{ "value", 1 } }.ToString();
            var query = mh.GetDataWithField(block_mongodbConnStr, block_mongodbDatabase, "utxo", fieldStr, findStr);
            var res = query.GroupBy(p => p["asset"].ToString(), (k, g) => {
                return new JObject() {
                    { "assetid", k.ToString()},
                    {"balance", g.Sum(pg => decimal.Parse(pg["value"].ToString())) }
                };
            }).ToArray();
            return new JArray { res };
        }
        public JArray getblocktime(int index)
        {
            string findStr = new JObject() { {"index", index} }.ToString();
            string fieldStr = new JObject() { { "time", 1 } }.ToString();
            var query = mh.GetDataWithField(block_mongodbConnStr, block_mongodbDatabase, "block", fieldStr, findStr);
            if (query == null || query.Count == 0) return new JArray() { };
            return new JArray { new JObject() { { "blocktime", query[0]["time"] } } };
        }
        public JArray getblockcount()
        {
            long blockcount = long.Parse(mh.GetData(block_mongodbConnStr, block_mongodbDatabase, "system_counter", "{counter:'block'}")[0]["lastBlockindex"].ToString()) + 1;
            return new JArray { new JObject() { { "blockcount", blockcount } } };
        }
        
        public JArray sendrawtransaction(string txhex)
        {
            return new JArray { sendrawtransaction(neoCliJsonRPCUrl, txhex) };
        }

        public JArray invokescript(string script)
        {
            return new JArray { invokescript(neoCliJsonRPCUrl, script)};
        }

        private JObject sendrawtransaction(string neoCliJsonRPCUrl, string txSigned)
        {
            var resp = httpHelper.Post(neoCliJsonRPCUrl, "{'jsonrpc':'2.0','method':'sendrawtransaction','params':['" + txSigned + "'],'id':1}", System.Text.Encoding.UTF8, 1);

            JObject Jresult = new JObject();
            bool isSendSuccess = false;
            var res = JObject.Parse(resp);
            if (res["error"] != null && res["error"]["message"] != null)
            {
                isSendSuccess = false;
                Jresult.Add("errorMessage", res["error"]["message"]);
            }
            else
            {
                isSendSuccess = (bool)JObject.Parse(resp)["result"];
            }
            //bool isSendSuccess = (bool)JObject.Parse(resp)["result"];
            //JObject Jresult = new JObject();
            Jresult.Add("sendrawtransactionresult", isSendSuccess);
            if (isSendSuccess)
            {
                ThinNeo.Transaction lastTran = new ThinNeo.Transaction();
                lastTran.Deserialize(new MemoryStream(txSigned.HexString2Bytes()));
                string txid = lastTran.GetHash().ToString();
                Jresult.Add("txid", txid);
            }
            else
            {
                //上链失败则返回空txid
                Jresult.Add("txid", string.Empty);
            }

            return Jresult;
        }

        private JObject invokescript(string neoCliJsonRPCUrl, string script)
        {
            var resp = httpHelper.Post(neoCliJsonRPCUrl, "{'jsonrpc':'2.0','method':'invokescript','params':['" + script + "'],'id':1}", System.Text.Encoding.UTF8, 1);

            JObject resultJ = (JObject)JObject.Parse(resp)["result"];

            return resultJ;
        }

        public string format(string txid)
        {
            return txid.StartsWith("0x") ? txid : "0x" + txid;
        }
    }
}
