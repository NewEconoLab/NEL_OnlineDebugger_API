using NEL_OnlineDebuger_API.lib;
using NEL_OnlineDebuger_API.Service.compile;
using NEL_Wallet_API.Controllers;
using NEL_Wallet_API.lib;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;

namespace NEL_OnlineDebuger_API.Service
{
    /// <summary>
    ///  编译服务
    ///  
    /// 0. 申请测试CGas+查询余额等
    /// 1. 编译合约文件（param:address/filetext, return: hash）
    ///        * 编译
    ///        * 上传oss,temp_
    ///        * 无需保存操作记录，直接返回hash
    /// 2. 获取合约文件(param: address/hash, return: cs/avm/map/abi)
    /// 3. 构造发布合约(param: address/txhex, return: txid)
    /// 4. 根据地址获取已发布合约摘要(param: address, return:scripthashs)
    /// 5. 构造调用合约(param:address/txhex, return txid)
    /// 6. 根据txid获取调用合约日志(param: txid, return:dumpInfo)
    /// 
    /// </summary>
    public class CompileServiceNew
    {
        public mongoHelper mh { set; get; }
        public string debug_mongodbConnStr { get; set; }
        public string debug_mongodbDatabase { get; set; }
        public string compilefileCol { get; set; } = "onlineDebuger_compilefile";
        public string deployfileCol { get; set; } = "onlineDebuger_deployfile";
        public OssFileService ossClient { get; set; }
        public CompileFileService debugger { get; set; }
        public string py_path { get; set; }
        public JObject cs_paths { get; set; }

        public JArray compileFile(string address, string filetext)
        {
            // 编译文件
            /*
            string avmtext = null;
            string abitext = null;
            string maptext = null;
            */
            string hash = null;
            string code = null;
            string message = null;
            bool flag = false;
            try
            {
                flag = debugger.compileFile(null, filetext, /*out avmtext, out abitext, out maptext,*/ out hash, out code, out message);
            } catch (Exception ex)
            {
                return new JArray() { new JObject() { { "code", "1001" }, { "message", "编译失败,失败提示:"+ex.Message }, { "hash", hash } } };
            }
            if(!flag)
            {
                return new JArray() { new JObject() { { "code", "1001" }, { "message", "编译失败,失败提示:" + message }, { "hash", hash } } };
            }
            return new JArray(){ new JObject() { { "code", "0000"}, { "message", "编译成功"}, { "hash", hash } } };
        }

        public JArray compileCsFile(string address,string filetext,string version)
        {
            if (version == "2.9.3")
                return compileFile(address, filetext);
            if (!cs_paths.ContainsKey(version))
                return new JArray() { new JObject() { { "code", "1001" }, { "message", "错误的版本号" } } };
            string hash = null;
            try
            {
                //加入一个随机数
                byte[] randombytes = new byte[10];
                using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(randombytes);
                }
                BigInteger randomNum = new BigInteger(randombytes);
                string tag = randomNum.ToString();
                Console.WriteLine();
                //创建合约文件
                string cs_path = (string)cs_paths[version];
                string contractFileName = string.Format("{0}/{1}.cs", cs_path, tag);
                System.IO.File.WriteAllText(contractFileName, filetext);
                //执行编译
                System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo();
                info.WorkingDirectory = cs_path;
                info.FileName = "dotnet";
                info.Arguments = string.Format("neon.dll {0}", contractFileName);
                info.CreateNoWindow = true;
                info.RedirectStandardOutput = true;
                info.RedirectStandardError = true;
                info.RedirectStandardInput = true;
                info.UseShellExecute = false;
                System.Diagnostics.Process proces = System.Diagnostics.Process.Start(info);
                proces.WaitForExit();
                //string outstr = proces.StandardOutput.ReadToEnd();
                string errstr = proces.StandardError.ReadLine();
                if(!string.IsNullOrEmpty(errstr))
                    return new JArray() { new JObject() { { "code", "1001" }, { "message", errstr }} };
                string nefFileName = string.Format("{0}/{1}.nef", cs_path, tag);
                string mapFileName = string.Format("{0}/{1}.map.json", cs_path, tag);
                string abiFileName = string.Format("{0}/{1}.abi.json", cs_path, tag);
                string manifestFileName = string.Format("{0}/{1}.manifest.json", cs_path, tag);
                string str_avm = string.Empty;
                string str_abi = string.Empty;
                string str_map = string.Empty;
                string str_manifest = string.Empty;
                if (System.IO.File.Exists(nefFileName) && System.IO.File.Exists(abiFileName) && System.IO.File.Exists(manifestFileName))
                {
                    byte[] avm = System.IO.File.ReadAllBytes(nefFileName);
                    str_avm = ThinNeo.Helper.Bytes2HexString(avm);

                    str_abi = System.IO.File.ReadAllText(abiFileName);
                    JObject jo = JObject.Parse(str_abi);
                    hash = jo["hash"].ToString();

                    str_manifest = System.IO.File.ReadAllText(manifestFileName);
                }
                else
                {
                    System.IO.File.Delete(contractFileName);
                    return new JArray() { new JObject() { { "code", "1001" }, { "message", "编译失败,失败提示:没有生成对应的avm文件" }, { "hash", hash } } };
                }
                if (System.IO.File.Exists(mapFileName))
                {
                    str_map = System.IO.File.ReadAllText(mapFileName);
                }

                //生成的文件上传到oss
                ossClient.OssFileUpload(string.Format("{0}.cs", hash), filetext);
                ossClient.OssFileUpload(string.Format("{0}.nef", hash), str_avm);
                ossClient.OssFileUpload(string.Format("{0}.abi.json", hash), str_abi);
                ossClient.OssFileUpload(string.Format("{0}.map.json", hash), str_map);
                ossClient.OssFileUpload(string.Format("{0}.manifest.json", hash), str_manifest);

                //把生成的文件删除
                System.IO.File.Delete(contractFileName);
                System.IO.File.Delete(nefFileName);
                System.IO.File.Delete(mapFileName);
                System.IO.File.Delete(abiFileName);
                System.IO.File.Delete(manifestFileName);
            }
            catch (Exception ex)
            {
                return new JArray() { new JObject() { { "code", "1001" }, { "message", "编译失败,失败提示:" + ex.Message }, { "hash", hash } } };
            }
            return new JArray() { new JObject() { { "code", "0000" }, { "message", "编译成功" }, { "hash", hash } } }; 
        }

        public JArray compilePythonFile(string address, string filetext)
        {
            string hash = null;
            try
            {
                //加入一个随机数
                byte[] randombytes = new byte[10];
                using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(randombytes);
                }
                BigInteger randomNum = new BigInteger(randombytes);
                string tag = address + randomNum;
                //创建合约文件
                string contractFileName = string.Format("{0}/{1}.py",py_path, tag);
                System.IO.File.WriteAllText(contractFileName,filetext);
                //创建启动编译合约的文件
                string runpy = string.Format("from boa.compiler import Compiler\nCompiler.load_and_save('{0}')", contractFileName);
                string runFileName = string.Format("{0}/{1}_run.py",py_path, tag);
                System.IO.File.WriteAllText(runFileName, runpy);
                //执行编译
                System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo();
                info.WorkingDirectory = py_path;
                info.FileName = "python3";
                info.Arguments = runFileName;
                info.CreateNoWindow = true;
                info.RedirectStandardOutput = true;
                info.RedirectStandardError = true;
                info.RedirectStandardInput = true;
                info.UseShellExecute = false;
                System.Diagnostics.Process proces = System.Diagnostics.Process.Start(info);
                proces.WaitForExit();
                string outstr = proces.StandardOutput.ReadToEnd();
                string avmFileName = string.Format("{0}/{1}.avm", py_path, tag);
                string mapFileName = string.Format("{0}/{1}.map.json", py_path, tag);
                string abiFileName = string.Format("{0}/{1}.abi.json", py_path, tag);
                string str_avm = string.Empty;
                string str_abi = string.Empty;
                string str_map = string.Empty;
                if (System.IO.File.Exists(avmFileName))
                {
                    byte[] avm = System.IO.File.ReadAllBytes(avmFileName);
                    hash = ThinNeo.Helper.Bytes2HexString(((byte[])ThinNeo.Helper.GetScriptHashFromScript(avm)).Reverse().ToArray());
                    hash = format(hash);
                    str_avm = ThinNeo.Helper.Bytes2HexString(avm);
                }
                else
                {
                    return new JArray() { new JObject() { { "code", "1001" }, { "message", "编译失败,失败提示:没有生成对应的avm文件" }, { "hash", hash } } };
                }
                if (System.IO.File.Exists(mapFileName))
                {
                    str_map = System.IO.File.ReadAllText(mapFileName);
                }
                if (System.IO.File.Exists(abiFileName))
                {
                    str_abi = System.IO.File.ReadAllText(abiFileName);
                }
                else
                {
                    return new JArray() { new JObject() { { "code", "1001" }, { "message", "编译失败,失败提示:没有生成对应的avm文件" }, { "hash", hash } } };
                }
                //生成的文件上传到oss
                ossClient.OssFileUpload(string.Format("{0}.py", hash), filetext);
                ossClient.OssFileUpload(string.Format("{0}.avm", hash), str_avm);
                ossClient.OssFileUpload(string.Format("{0}.abi.json", hash), str_abi);
                ossClient.OssFileUpload(string.Format("{0}.map.json", hash), str_map);

                //把生成的文件删除
                System.IO.File.Delete(contractFileName);
                System.IO.File.Delete(runFileName);
                System.IO.File.Delete(avmFileName);
                System.IO.File.Delete(mapFileName);
                System.IO.File.Delete(abiFileName);
            }
            catch (Exception ex)
            {
                return new JArray() { new JObject() { { "code", "1001" }, { "message", "编译失败,失败提示:" + ex.Message }, { "hash", hash } } };
            }
            return new JArray() { new JObject() { { "code", "0000" }, { "message", "编译成功" }, { "hash", hash } } };
        }

        public JArray getCompilerVersions()
        {
            JArray ja = new JArray();
            foreach (var c in cs_paths)
            {
                ja.Add(new JValue(c.Key));
            }
            return ja;
        }

        public JArray getContractCodeByHash(string address, string hash, string type=".all")
        {
            hash = format(hash);
            // 获取他人编译的合约文件
            if(address == "")
            {
                return downloadCompileFile(hash, type);
            }
            // 获取自己编译的合约文件.cs/.avm/.abi.json/.map.json
            string findStr = new JObject() { { "address", address }, { "scripthash", hash } }.ToString();
            if(mh.GetDataCount(debug_mongodbConnStr, debug_mongodbDatabase, deployfileCol, findStr) == 0)
            {
                hash = "temp_" + hash;
            }
            return downloadCompileFile(hash, type);
        }

        public JArray downloadCompileFile(string hash, string type=".all")
        {
            hash = format(hash);
            type = type.ToLower();
            if(type==".cs" || type == "cs") {
                return new JArray { new JObject() { { "cs", ossClient.OssFileDownLoad(System.IO.Path.Combine("", hash + ".cs")) } } };
            }
            if (type == ".avm" || type == "avm")
            {
                return new JArray { new JObject() { { "avm", ossClient.OssFileDownLoad(System.IO.Path.Combine("", hash + ".avm")) } } };
            }
            if (type == ".abi" || type == "abi")
            {
                return new JArray { new JObject() { { "abi", ossClient.OssFileDownLoad(System.IO.Path.Combine("", hash + ".abi.json")) } } };
            }
            if (type == ".map" || type == "map")
            {
                return new JArray { new JObject() { { "map", ossClient.OssFileDownLoad(System.IO.Path.Combine("", hash + ".map.json")) } } };
            }
            if (type == ".py" || type == "py")
            {
                return new JArray { new JObject() { { "py", ossClient.OssFileDownLoad(System.IO.Path.Combine("", hash + ".py")) } } };
            }
            if (type == ".nef" || type == "nef")
            {
                return new JArray { new JObject() { { "nef", ossClient.OssFileDownLoad(System.IO.Path.Combine("", hash + ".nef")) } } };
            }
            if (type == ".manifest" || type == "manifest")
            {
                return new JArray { new JObject() { { "nef", ossClient.OssFileDownLoad(System.IO.Path.Combine("", hash + ".manifest.json")) } } };
            }
            // 获取合约文件
            string pathScript = "";
            string str_avm = ossClient.OssFileDownLoad(System.IO.Path.Combine(pathScript, hash + ".avm"));
            string str_cs = ossClient.OssFileDownLoad(System.IO.Path.Combine(pathScript, hash + ".cs"));
            string str_py = ossClient.OssFileDownLoad(System.IO.Path.Combine(pathScript, hash + ".py"));
            string str_nef = ossClient.OssFileDownLoad(System.IO.Path.Combine(pathScript, hash + ".nef"));
            var JO_map = (MyJson.IJsonNode)MyJson.Parse(ossClient.OssFileDownLoad(System.IO.Path.Combine(pathScript, hash + ".map.json")));
            var JO_abi = (MyJson.IJsonNode)MyJson.Parse(ossClient.OssFileDownLoad(System.IO.Path.Combine(pathScript, hash + ".abi.json")));
            var JO_manifest = (MyJson.IJsonNode)MyJson.Parse(ossClient.OssFileDownLoad(System.IO.Path.Combine(pathScript, hash + ".manifest.json")));
            JObject JO_result = new JObject();
            JO_result["avm"] = str_avm;
            JO_result["cs"] = str_cs;
            JO_result["py"] = str_py;
            JO_result["nef"] = str_nef;
            JO_result["map"] = JO_map?.ToString();
            JO_result["abi"] = JO_abi?.ToString();
            JO_result["manifest"] = JO_manifest?.ToString();
            return new JArray() { JO_result };
        }

        public void storeContractFile(string hash,string language)
        {
            if(language == "py")//py
                ossClient.OssFileStore(hash + ".py");
            else
                ossClient.OssFileStore(hash + ".cs");
            ossClient.OssFileStore(hash + ".avm");
            ossClient.OssFileStore(hash + ".abi.json");
            ossClient.OssFileStore(hash + ".map.json");
        }
        
        public JArray saveContract(string address, string scripthash, string name, string version, string author, string email, string desc, string acceptablePayment, string createStorage, string dynamicCall, string txid,string language)
        {
            //address + hash + name + version + author + email + desc + 可接受付款 + 创建存储区 + 动态调用 + txid
            scripthash = format(scripthash);
            txid = format(txid);
            // 保存部署信息
            long nowtime = TimeHelper.GetTimeStamp();
            string newdata = new JObject() {
                {"address", address },
                {"scripthash", scripthash },
                {"name", name },
                {"version", version },
                {"author", author },
                {"email", email },
                {"desc", desc },
                {"acceptablePayment", acceptablePayment },
                {"createStorage", createStorage },
                {"dynamicCall", dynamicCall },
                {"txid", txid },
                {"createTime", nowtime },
                {"lastUpdateTime", nowtime },
                {"language",language}
            }.ToString();
            string findStr = new JObject() { { "scripthash", scripthash} }.ToString();
            if(mh.GetDataCount(debug_mongodbConnStr, debug_mongodbDatabase, deployfileCol, findStr) > 0)
            {
                return new JArray() { new JObject() { { "code", "0000" }, { "message", "重复保存" } } };
            }
            mh.InsertOneData(debug_mongodbConnStr, debug_mongodbDatabase, deployfileCol, newdata);
            // 保存编译文件
            storeContractFile(scripthash, language);
            return new JArray() { new JObject() { { "code", "0000" }, { "message", "保存成功" } } };
        }

        public JArray getContractRemarkByAddress(string address, int pageNum = 1, int pageSize = 20)
        {
            string findStr = new JObject() { {"address", address } }.ToString();
            string fieldStr = MongoFieldHelper.toReturn(new string[] {"scripthash", "name" , "language" }).ToString();
            string sortStr = new JObject() { {"createTime", -1 } }.ToString();
            return mh.GetDataPagesWithField(debug_mongodbConnStr, debug_mongodbDatabase, deployfileCol, fieldStr, pageSize, pageNum, sortStr, findStr);
        }

        public JArray getContractDeployInfoByHash(string scripthash)
        {
            scripthash = format(scripthash);
            string findStr = new JObject() { { "scripthash", scripthash } }.ToString();
            return mh.GetData(debug_mongodbConnStr, debug_mongodbDatabase, deployfileCol, findStr);
        }

        public string format(string txid)
        {
            if (txid.StartsWith("temp_")) return txid;
            return txid.StartsWith("0x") ? txid : "0x" + txid;
        }
        
    }
}
