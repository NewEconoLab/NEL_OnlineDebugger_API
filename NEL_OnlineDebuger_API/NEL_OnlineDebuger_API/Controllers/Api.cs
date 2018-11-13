using System;
using Newtonsoft.Json.Linq;
using NEL_OnlineDebuger_API.RPC;
using NEL_OnlineDebuger_API.lib;
using NEL_Wallet_API.lib;
using NEL_Wallet_API.Service;
using System.Threading.Tasks;
using NEL_OnlineDebuger_API.Service;
using NEL_Wallet_API.Controllers;

namespace NEL_OnlineDebuger_API.Controllers
{
    public class Api
    {
        private string netnode { get; set; }
        private ClaimGasService claimService;
        private ClaimGasTransaction claimTx4testnet;
        private CompileService compileService;

        private mongoHelper mh = new mongoHelper();

        private static Api testApi = new Api("testnet");
        private static Api mainApi = new Api("mainnet");
        public static Api getTestApi() { return testApi; }
        public static Api getMainApi() { return mainApi; }

        public Api(string node)
        {
            netnode = node;
            switch (netnode)
            {
                case "testnet":
                    compileService = new CompileService
                    {
                        mh = mh,
                        notify_mongodbConnStr = mh.notify_mongodbConnStr_testnet,
                        notify_mongodbDatabase = mh.notify_mongodbDatabase_testnet,
                        ossClient = new OssFileService("http://47.98.124.225:84/api/testnet/")
                    };
                    claimService = new ClaimGasService
                    {
                        assetid = mh.id_gas,
                        accountInfo = AccountInfo.getAccountInfoFromWif(mh.prikeywif_testnet),
                        mh = mh,
                        notify_mongodbConnStr = mh.notify_mongodbConnStr_testnet,
                        notify_mongodbDatabase = mh.notify_mongodbDatabase_testnet,
                        gasClaimCol = mh.gasClaimCol_testnet,
                        maxClaimAmount = int.Parse(mh.maxClaimAmount_testnet),
                    };
                    claimTx4testnet = new ClaimGasTransaction
                    {
                        nelJsonRpcUrl = mh.nelJsonRPCUrl_testnet,
                        assetid = mh.id_gas,
                        accountInfo = AccountInfo.getAccountInfoFromWif(mh.prikeywif_testnet),
                        mh = mh,
                        notify_mongodbConnStr = mh.notify_mongodbConnStr_testnet,
                        notify_mongodbDatabase = mh.notify_mongodbDatabase_testnet,
                        gasClaimCol = mh.gasClaimCol_testnet,
                        block_mongodbConnStr = mh.block_mongodbConnStr_testnet,
                        block_mongodbDatabase = mh.block_mongodbDatabase_testnet,
                        batchSendInterval = int.Parse(mh.batchSendInterval_testnet),
                        checkTxInterval = int.Parse(mh.checkTxInterval_testnet),
                        checkTxCount = int.Parse(mh.checkTxCount_testnet)
                    };
                    // 暂时放在这里，后续考虑单独整出来
                    new Task(() => claimTx4testnet.claimGasLoop()).Start();
                    break;
                case "mainnet":
                    
                    
                    break;
            }
        }

        public object getRes(JsonRPCrequest req, string reqAddr)
        {
            JArray result = null;
            try
            {
                switch (req.method)
                {
                    case "saveCompileFile":
                        result = compileService.saveContractFile(req.@params[0].ToString(), req.@params[1].ToString());
                        break;
                    case "getCompileFile":
                        result = compileService.getCompileFile(req.@params[0].ToString());
                        break;
                    case "compile":
                        result = compileService.compileFile(req.@params[0].ToString(), req.@params[1].ToString());
                        break;
                    // 查询是否可以申领Gas
                    case "hasclaimgas":
                        result = claimService.hasClaimGas(req.@params[0].ToString());
                        break;
                    // 申领Gas(即向客户地址转账，默认1gas
                    case "claimgas":
                        if (req.@params.Length < 2)
                        {
                            result = claimService.claimGas(req.@params[0].ToString());
                        }
                        else
                        {
                            result = claimService.claimGas(req.@params[0].ToString(), Convert.ToDecimal(req.@params[1]));
                        }
                        break;
                    case "":
                        break;

                    // test
                    case "getnodetype":
                        result = new JArray { new JObject { { "nodeType", netnode } } };
                        break;
                }
                if (result.Count == 0)
                {
                    JsonPRCresponse_Error resE = new JsonPRCresponse_Error(req.id, -1, "No Data", "Data does not exist");
                    return resE;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("errMsg:{0},errStack:{1}", e.Message, e.StackTrace);
                JsonPRCresponse_Error resE = new JsonPRCresponse_Error(req.id, -100, "Parameter Error", e.Message);
                return resE;
            }

            JsonPRCresponse res = new JsonPRCresponse();
            res.jsonrpc = req.jsonrpc;
            res.id = req.id;
            res.result = result;

            return res;
        }
    }
}

