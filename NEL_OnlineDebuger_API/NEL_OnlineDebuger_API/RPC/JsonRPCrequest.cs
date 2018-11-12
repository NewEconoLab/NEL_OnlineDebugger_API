
namespace NEL_OnlineDebuger_API.RPC
{
    public class JsonRPCrequest
    {
        public string jsonrpc { get; set; }
        public string method { get; set; }
        public object[] @params { get; set; }
        public long id { get; set; }
    }
}
