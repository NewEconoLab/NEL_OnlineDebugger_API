using NEL_OnlineDebuger_API.lib;
using NEL_Wallet_API.Controllers;
using NEL_Wallet_API.lib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace NEL_Wallet_API.Service
{
    public class ClaimGasTransaction
    {
        public const long ONE_DAY_SECONDS = 24 * 60 * 60;
        public string nelJsonRpcUrl { get; set; }
        public string assetid { get; set; }
        public AccountInfo accountInfo { get; set; }
        public string block_mongodbConnStr { get; set; }
        public string block_mongodbDatabase { get; set; }
        public mongoHelper mh { set; get; }
        public string notify_mongodbConnStr { get; set; }
        public string notify_mongodbDatabase { get; set; }
        public string gasClaimCol { get; set; }

        public int batchSendInterval { get; set; } = 10; /*默认10分钟*/
        public int checkTxInterval { get; set; }
        public int checkTxCount { get; set; }

        public void claimGasLoop()
        {
            while (true)
            {
                try
                {
                    sleep();
                    long nowtime = TimeHelper.GetTimeStamp();
                    string filter = new JObject() { { "lasttime", new JObject() { { "$gt", nowtime - ONE_DAY_SECONDS } } }, { "state", "1" } }.ToString();
                    string fieldStr = new JObject() { { "address", 1 }, { "amount", 1 } }.ToString();
                    string sortStr = new JObject() { { "lasttime", 1 } }.ToString();
                    JArray res = mh.GetDataPagesWithField(notify_mongodbConnStr, notify_mongodbDatabase, gasClaimCol, fieldStr, 33/*默认33个*/, 1, sortStr, filter);
                    if (res == null || res.Count() == 0) continue;

                    // 一笔交易多输出
                    mergeClaimGasTx(res.Select(p => p["address"].ToString()).ToList(), long.Parse(res[0]["amount"].ToString()));
                }
                catch(Exception e)
                {
                    Console.WriteLine("claimGasLoopErrMsg:"+e.Message);
                    Console.WriteLine("claimGasLoopErrStack:" + e.StackTrace);
                }
            }
        }
        private void sleep()
        {
            for(int i=0; i<60; ++i)
            {
                Thread.Sleep(batchSendInterval*1000);
            }
        }
        private void mergeClaimGasTx(List<string> address, decimal amount)
        {
            TransactionHandler handler = new TransactionHandler();
            handler.nelJsonRpcUrl = nelJsonRpcUrl;
            handler.assetid = assetid;
            handler.accountInfo = accountInfo;
            //handler.applyGas(address, amount);
            handler.applyGas(address, amount, getBalance(accountInfo.address));
            JObject rr = handler.getResult();
            string code = rr["code"].ToString();
            string txid = rr["txid"].ToString();
            if (code == "0000")// || code == "3001")
            {
                // 若入链成功，则更新状态
                // if (checkTxHasInBlock(txid))
                {
                    string findStr = MongoFieldHelper.toFilter(address.ToArray(), "address").ToString();
                    string updateData = new JObject() { { "$set", new JObject() { { "state", "2" }, { "txid", txid } } } }.ToString();
                    mh.UpdateData(notify_mongodbConnStr, notify_mongodbDatabase, gasClaimCol, updateData, findStr);
                }
            }
        }

        private bool checkTxHasInBlock(string txid)
        {
            bool flag = false;
            long curHeight = getBlockCount();
            do
            {
                if (hasTx(txid))
                {
                    flag = true;
                    break;
                }
                Thread.Sleep(checkTxInterval);
            } while (getBlockCount() <= curHeight + checkTxCount);
            return flag;
        }
        private long getBlockCount()
        {
            return mh.GetDataCount(block_mongodbConnStr, block_mongodbDatabase, "block");
        }
        private bool hasTx(string txid)
        {
            return mh.GetDataCount(block_mongodbConnStr, block_mongodbDatabase, "tx", new JObject() { { "txid", txid } }.ToString()) > 0;
        }

        private Dictionary<string, List<Utxo>> getBalance(string address)
        {
            string findFliter = "{addr:'" + address + "',used:''}";
            JArray result = mh.GetData(block_mongodbConnStr, block_mongodbDatabase, "utxo", findFliter);
            if(result == null || result.Count == 0)
            {
                return null;
            }
            Utxo[] utxoArr = result.Select(p => new Utxo(
                p["addr"].ToString(),
                new ThinNeo.Hash256(p["txid"].ToString()),
                p["asset"].ToString(),
                decimal.Parse(p["value"].ToString(), NumberStyles.Float),
                int.Parse(p["n"].ToString())
                )).ToArray();
            Dictionary<string, List<Utxo>> res = new Dictionary<string, List<Utxo>>();
            foreach(Utxo utxo in utxoArr)
            {
                string assetid = utxo.asset;
                if (res.ContainsKey(assetid))
                {
                    res[assetid].Add(utxo);
                }
                else
                {
                    List<Utxo> list = new List<Utxo>();
                    list.Add(utxo);
                    res[assetid] = list;
                }
            }
            return res;
        }
    }
}
