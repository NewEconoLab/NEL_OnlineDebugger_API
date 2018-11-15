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
        private CommonService commonService;

        private mongoHelper mh = new mongoHelper();

        private static Api testApi = new Api("testnet");
        public static Api getTestApi() { return testApi; }

        public Api(string node)
        {
            netnode = node;
            switch (netnode)
            {
                case "testnet":
                    commonService = new CommonService
                    {
                        mh = mh,
                        block_mongodbConnStr = mh.block_mongodbConnStr_testnet,
                        block_mongodbDatabase = mh.block_mongodbDatabase_testnet,
                        notify_mongodbConnStr = mh.notify_mongodbConnStr_testnet,
                        notify_mongodbDatabase = mh.notify_mongodbDatabase_testnet,
                        neoCliJsonRPCUrl = mh.neoCliJsonRPCUrl_testnet,
                    };
                    compileService = new CompileService
                    {
                        mh = mh,
                        notify_mongodbConnStr = mh.notify_mongodbConnStr_testnet,
                        notify_mongodbDatabase = mh.notify_mongodbDatabase_testnet,
                        ossClient = new OssFileService(mh.nelOssRPCUrl_testnet)
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
                    // 根据交易id获取通知数据
                    case "getNotifyByTxid":
                        result = commonService.getNotifyByTxid(req.@params[0].ToString());
                        break;
                    // 根据交易id获取执行结果
                    case "getDumpInfoByTxid":
                        result = commonService.getDumpInfoByTxid(req.@params[0].ToString());
                        break;
                    // 根据地址获取交易id和提交时间
                    case "getTxCallContract":
                        result = commonService.getTxCallContract(req.@params[0].ToString());
                        break;
                    // 转发交易并存储结果
                    case "txCallContract":
                        result = commonService.txCallContract(req.@params[0].ToString(), req.@params[1].ToString());
                        break;
                    // 
                    case "sendrawtransaction":
                        result = commonService.sendrawtransaction(req.@params[0].ToString());
                        break;

                    // 根据哈希获取合约信息
                    case "getContractDeployInfoByHash":
                        result = compileService.getContractDeployInfoByHash(req.@params[0].ToString());
                        break;
                    // 根据哈希获取合约文件
                    case "getContractCodeByHash":
                        result = compileService.downloadCompileFile(req.@params[0].ToString());
                        break;
                    // 根据地址获取合约摘要
                    case "getContractRemarkByAddress":
                        result = compileService.getContractRemarkByAddress(req.@params[0].ToString());
                        break;
                    // 3. 保存合约
                    case "storageContractFile":
                        result = compileService.saveContract(
                            req.@params[0].ToString(),
                            req.@params[1].ToString(),
                            req.@params[2].ToString(),
                            req.@params[3].ToString(),
                            req.@params[4].ToString(),
                            req.@params[5].ToString(),
                            req.@params[6].ToString(),
                            req.@params[7].ToString(),
                            req.@params[8].ToString(),
                            req.@params[9].ToString(),
                            req.@params[10].ToString()
                            );
                        break;
                    case "saveCompileFile":
                        result = compileService.uploadContractFile(req.@params[0].ToString(), req.@params[1].ToString());
                        break;
                    case "getCompileFile":
                        result = compileService.downloadCompileFile(req.@params[0].ToString());
                        break;

                    // 2. 编译文件
                    case "compileContractFile":
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

                    // 获取余额
                    case "getUtxoBalance":
                        if (req.@params.Length < 2)
                        {
                            result = commonService.getUtxoBalance(req.@params[0].ToString());
                        } else
                        {
                            result = commonService.getUtxoBalance(req.@params[0].ToString(), req.@params[1].ToString());
                        }
                        break;
                    // 获取区块时间
                    case "getblocktime":
                        result = commonService.getblocktime(int.Parse(req.@params[0].ToString()));
                        break;
                    // 获取区块高度
                    case "getblockcount":
                        result = commonService.getblockcount();
                        break;
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

