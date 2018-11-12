using System;
using System.Threading.Tasks;
using log4net;
using Microsoft.AspNetCore.Mvc;
using NEL_OnlineDebuger_API.RPC;
using NEL_Wallet_API.lib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NEL_OnlineDebuger_API.Controllers
{
    [Route("api/[controller]")]
    public class MainnetController : Controller
    {
        //Api api = new Api("mainnet");
        Api api = Api.getMainApi();
        private long logExeTimeMax = 15; // 运行最大请求耗时15秒
        private ILog log = LogManager.GetLogger(Startup.repository.Name, typeof(TestnetController));

        [HttpGet]
        public JsonResult Get(string @jsonrpc, string @method, string @params, long @id)
        {
            DateTime start = DateTime.Now;
            JsonResult res = null;
            JsonRPCrequest req = null;
            try
            {
                req = new JsonRPCrequest
                {
                    jsonrpc = @jsonrpc,
                    method = @method,
                    @params = JsonConvert.DeserializeObject<object[]>(JsonConvert.SerializeObject(JArray.Parse(@params))),
                    id = @id
                };

                string ipAddr = Request.HttpContext.Connection.RemoteIpAddress.ToString();
                res = Json(api.getRes(req, ipAddr));

                // 超时记录
                if (DateTime.Now.Subtract(start).TotalSeconds > logExeTimeMax)
                {
                    log.Info(logHelper.logInfoFormat(req, res, start));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("errMsg:{0},errStack:{1}", e.Message, e.StackTrace);
                JsonPRCresponse_Error resE = new JsonPRCresponse_Error(0, -100, "Parameter Error", e.Message);
                res = Json(resE);

                // 错误记录
                log.Info(logHelper.logInfoFormat(req, res, start));
            }


            return res;
        }

        [HttpPost]
        public async Task<JsonResult> Post()
        {
            DateTime start = DateTime.Now;
            JsonResult res = null;
            JsonRPCrequest req = null;
            try
            {
                var ctype = HttpContext.Request.ContentType;
                LitServer.FormData form = null;
                if (ctype == "application/x-www-form-urlencoded" ||
                     (ctype.IndexOf("multipart/form-data;") == 0))
                {
                    form = await LitServer.FormData.FromRequest(HttpContext.Request);
                    var _jsonrpc = form.mapParams["jsonrpc"];
                    var _id = long.Parse(form.mapParams["id"]);
                    var _method = form.mapParams["method"];
                    var _strparams = form.mapParams["params"];
                    var _params = JArray.Parse(_strparams);
                    req = new JsonRPCrequest
                    {
                        jsonrpc = _jsonrpc,
                        method = _method,
                        @params = JsonConvert.DeserializeObject<object[]>(JsonConvert.SerializeObject(_params)),
                        id = _id
                    };
                }
                else// if (ctype == "application/json") 其他所有请求方式都这样取好了
                {
                    var text = await LitServer.FormData.GetStringFromRequest(HttpContext.Request);
                    req = JsonConvert.DeserializeObject<JsonRPCrequest>(text);
                }

                string ipAddr = Request.HttpContext.Connection.RemoteIpAddress.ToString();
                res = Json(api.getRes(req, ipAddr));

                // 超时记录
                if (DateTime.Now.Subtract(start).TotalSeconds > logExeTimeMax)
                {
                    log.Info(logHelper.logInfoFormat(req, res, start));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("errMsg:{0},errStack:{1}", e.Message, e.StackTrace);
                JsonPRCresponse_Error resE = new JsonPRCresponse_Error(0, -100, "Parameter Error", e.Message);
                res = Json(resE);

                // 错误记录
                log.Info(logHelper.logInfoFormat(req, res, start));
            }

            return res;

        }
    }
}