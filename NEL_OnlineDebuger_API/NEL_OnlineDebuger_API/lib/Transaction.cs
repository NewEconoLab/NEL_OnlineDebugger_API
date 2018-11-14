using NEL_Wallet_API.lib;
using Newtonsoft.Json.Linq;
using System.IO;

namespace NEL_OnlineDebuger_API.lib
{
    public class Transaction
    {
        public JObject sendrawtransaction(string neoCliJsonRPCUrl, string txSigned)
        {
            var resp = httpHelper.Post(neoCliJsonRPCUrl, "{'jsonrpc':'2.0','method':'sendrawtransaction','params':['" + txSigned + "'],'id':1}", System.Text.Encoding.UTF8, 1);

            JObject Jresult = new JObject();
            bool isSendSuccess = false;
            var res = JObject.Parse(resp);
            if (res["error"] != null && res["error"]["message"] != null)
            {
                isSendSuccess = false;
                Jresult.Add("errorMessage", res["error"]["message"]);
            } else
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
            else {
                //上链失败则返回空txid
                Jresult.Add("txid", string.Empty);
            }

            return Jresult;
        }
    }
}
