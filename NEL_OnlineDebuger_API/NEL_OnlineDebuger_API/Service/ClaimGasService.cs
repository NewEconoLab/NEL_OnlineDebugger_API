using NEL_OnlineDebuger_API.lib;
using NEL_Wallet_API.Controllers;
using NEL_Wallet_API.lib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NEL_Wallet_API.Service
{
    public class ClaimGasService
    {
        public const long ONE_DAY_SECONDS = 24 * 60 * 60;
        //public string nelJsonRpcUrl { get; set; }
        public string assetid { get; set; }
        public AccountInfo accountInfo { get; set; }
        public mongoHelper mh { set; get; }
        public string debug_mongodbConnStr { get; set; }
        public string debug_mongodbDatabase { get; set; }
        public string block_mongodbConnStr { get; set; }
        public string block_mongodbDatabase { get; set; }

        public string gasClaimCol { get; set; } = "neo3dev_gasClaimCol";
        public int maxClaimAmount { get; set; }


        /// <summary>
        /// 
        /// 0000 成功(表示发送交易成功)
        /// 3001 交易发送失败
        /// 3002 余额不足
        /// 
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public JArray claimGas(string address, decimal amount = 1)
        {
            if (amount > maxClaimAmount || amount <= 0)
            {
                // 超过最大金额
                return new JArray() { overLimitAmount() };
            }
            long nowtime = TimeHelper.GetTimeStamp();
            string filter = new JObject() { { "address", address} }.ToString();
            JArray res = mh.GetData(debug_mongodbConnStr, debug_mongodbDatabase, gasClaimCol, filter);
            if(res == null || res.Count() == 0)
            {
                // 从未申请直接入库
                mh.InsertOneData(debug_mongodbConnStr, debug_mongodbDatabase, gasClaimCol, new JObject() { {"address", address }, { "amount", amount }, { "lasttime", nowtime }, { "state", "1" }, { "times", 1 }, { "txid", "" } }.ToString());
                return new JArray() { txWait() };
            }
            // 隔天重复申请更新库
            long lasttime = long.Parse(res[0]["lasttime"].ToString());

            long times = res[0]["times"] == null ? 1:long.Parse(res[0]["times"].ToString());
            if (nowtime - lasttime > ONE_DAY_SECONDS)
            {
                mh.ReplaceData(debug_mongodbConnStr, debug_mongodbDatabase, gasClaimCol, filter, new JObject() { { "address", address }, { "amount", amount }, { "lasttime", nowtime }, { "state", "1" }, { "times", times+1 }, { "txid", "" } }.ToString());
                return new JArray() { txWait() };
            }
            return new JArray() { hasClaimGas() };
        }
        

        /// <summary>
        /// 查询该地址是否可以申领GAS
        /// 0 可领取： 从未领取 + 24h后再次领取
        /// 1 派发中
        /// 2 已领取
        /// 
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public JArray hasClaimGas(string address)
        {
            //Boolean flag = true;
            JArray res = mh.GetDataWithField(debug_mongodbConnStr, debug_mongodbDatabase, gasClaimCol, new JObject() { { "lasttime", 1 }, { "state", 1 } }.ToString(), new JObject() { { "address", address } }.ToString());
            if (res == null || res.Count() == 0)
            {
                // 可领取：从未领取
                return new JArray() { canClaimState() };

            }
            long lasttime = long.Parse(res[0]["lasttime"].ToString());
            long nowtime = TimeHelper.GetTimeStamp();
            if (lasttime < nowtime - ONE_DAY_SECONDS)
            {
                // 可领取：24h后重复领取
                return new JArray() { canClaimState() };
            }

            if(res[0]["state"] == null)
            {
                return new JArray() { hasClaimState() };
            }
            string state = res[0]["state"].ToString();
            if (state == "1")
            {
                // 派发中
                return new JArray() { dealingState() };
            }
            else
            {
                // 已领取
                return new JArray() { hasClaimState() };

            }
        }



        private JObject hasClaimGas()
        {
            return new JObject() { { "code", "3003" }, { "codeMessage", "已经领取" }, { "txid", "" } };
        }
        private JObject overLimitAmount()
        {
            return new JObject() { { "code", "3004" }, { "codeMessage", "超过限制金额" }, { "txid", "" } };
        }
        private JObject txWait()
        {
            return new JObject() { { "code", "3000" }, { "codeMessage", "交易待发送" }, { "txid", "" } };
        }
        private JObject canClaimState()
        {
            return new JObject() { { "code", "3010" }, { "codeMessage", "可领取" }, { "txid", "" } };
        }
        private JObject dealingState()
        {
            return new JObject() { { "code", "3011" }, { "codeMessage", "排队中" }, { "txid", "" } };
        }
        private JObject hasClaimState()
        {
            return new JObject() { { "code", "3012" }, { "codeMessage", "已领取" }, { "txid", "" } };
        }

    }
    public class TransactionHandler
    {
        public string nelJsonRpcUrl { get; set; }
        public string assetid { get; set; }
        public AccountInfo accountInfo { get; set; }
        private JObject res;
        
        public JObject getResult()
        {
            while(res == null)
            {
                Thread.Sleep(100);
            }
            return res;
        }
        
        public async void applyGas(string address, decimal amount = 1, Dictionary<string, List<Utxo>> dir = null)
        {
            res = await asyncApplyGas(new string[] { address }.ToList(), amount, dir);
        }
        public async void applyGas(List<string> addresses, decimal amount = 1, Dictionary<string, List<Utxo>> dir = null)
        {
            res = await asyncApplyGas(addresses, amount, dir);
        }
        private async Task<JObject> asyncApplyGas(List<string> targetAddress, decimal amount, Dictionary<string, List<Utxo>> dir)
        {
           
            // 转换私钥
            byte[] prikey = accountInfo.prikey;
            byte[] pubkey = accountInfo.pubkey;
            string address = accountInfo.address;

            // 获取余额
            string id_gas = assetid;
            //Dictionary<string, List<Utxo>> dir2 = await TransHelper.GetBalanceByAddress(nelJsonRpcUrl, address);
            if(dir == null || dir[id_gas] == null)
            {
                // 余额不足
                return insufficientBalance();
            }
            List<Utxo> balanceUtxo = dir[id_gas];
            if (balanceUtxo.Sum(p => p.value) < amount * targetAddress.Count())
            {
                // 余额不足
                return insufficientBalance();
            }

            // 构造并发送交易
            ThinNeo.Transaction tran = TransHelper.makeTran(dir[id_gas], targetAddress, new ThinNeo.Hash256(id_gas), amount);
            tran.type = ThinNeo.TransactionType.ContractTransaction;
            byte[] msg = tran.GetMessage();
            byte[] signdata = ThinNeo.Helper.Sign(msg, prikey);
            tran.AddWitness(signdata, pubkey, address);
            string txid = tran.GetHash().ToString();
            byte[] data = tran.GetRawData();
            string rawdata = ThinNeo.Helper.Bytes2HexString(data);
            byte[] postdata;
            try
            {
            string url = TransHelper.MakeRpcUrlPost(nelJsonRpcUrl, "sendrawtransaction", out postdata, new MyJson.JsonNode_ValueString(rawdata));
            var result = await TransHelper.HttpPost(url, postdata);
            Console.WriteLine("result:" + result);
            if (JObject.Parse(result)["result"] == null)
            {
                return txFail(txid);
            }
            JObject res = (JObject)(((JArray)(JObject.Parse(result)["result"]))[0]);
            string flag = res["sendrawtransactionresult"].ToString();
            if (flag != "True" && res["txid"].ToString() != txid)
            {
                return txFail(txid);
            }
            return txSucc(txid);
            } catch (Exception )
            {
                return txFail(txid);
            }
        }
        
        private JObject txSucc(string txid)
        {
            return new JObject() { { "code", "0000" }, { "codeMessage", "交易发送成功" }, { "txid", txid } };
        }
        private JObject txFail(string txid)
        {
            return new JObject() { { "code", "3001" }, { "codeMessage", "交易发送失败" }, { "txid", txid } };
        }
        private JObject insufficientBalance()
        {
            return new JObject() { { "code", "3002" }, { "codeMessage", "余额不足" }, { "txid", "" } };
        }
    }
}
