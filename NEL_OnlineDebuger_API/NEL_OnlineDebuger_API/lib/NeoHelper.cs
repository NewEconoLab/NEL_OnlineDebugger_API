using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using ThinNeo;
using ThinNeo.Cryptography.Cryptography;

namespace NEL_OnlineDebuger_API.lib
{
    public static class NeoHelper
    {
        public static string address2pubkeyHash(this string address)
        {
            var hash = Helper.GetPublicKeyHashFromAddress(address);
            return hash.ToString();
        }
        public static string pubkeyhash2address(this string hash)
        {
            return Helper.GetAddressFromScriptHash(new Hash160(hash));
        }
        public static string address2pubkeyHashN(this string address)
        {
            var hash = GetPublicKeyHashFromAddressN(address);
            return hash.ToString();
        }

        private static ThreadLocal<SHA256> sha256ThreadLocal = new ThreadLocal<SHA256>(() => SHA256.Create());
        private static SHA256 getSha256() { return sha256ThreadLocal.Value; }
        public static Hash160 GetPublicKeyHashFromAddressN(this string address)
        {
            var alldata = Base58.Decode(address);
            if (alldata.Length != 25)
                throw new Exception("error length.");
            var data = alldata.Take(alldata.Length - 4).ToArray();
            if (data[0] != 0x35)
                throw new Exception("not a address");
            SHA256 sha256 = getSha256();
            var hash = sha256.ComputeHash(data);
            hash = sha256.ComputeHash(hash);
            var hashbts = hash.Take(4).ToArray();
            var datahashbts = alldata.Skip(alldata.Length - 4).ToArray();
            if (hashbts.SequenceEqual(datahashbts) == false)
                throw new Exception("not match hash");
            var pkhash = data.Skip(1).ToArray();
            return new Hash160(pkhash);
        }
    }
}
