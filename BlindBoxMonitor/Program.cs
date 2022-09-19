using Neo;
using Neo.Wallets;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace BlindBoxMonitor
{
    class Program
    {
        static readonly string URL = "http://localhost:10332";

        static readonly string ContractHash = "0xcd10d9f697230b04d9ebb8594a1ffe18fa95d9ad";

        static readonly string OwnerScriptHash = "0x4578060c29f4c03f1e16c84312429d991952c94c";

        static bool IsOpened = false;

        static void Main(string[] args)
        {
            var tokensOf = TokensOf(OwnerScriptHash);

            Console.WriteLine($"查询到NFT数量：{tokensOf.Count}");
            var t = false;
            var index = 0;
            while (!t)
            {
                try
                {
                    Console.WriteLine("请输入发放批次（数字）");
                    index = Convert.ToInt32(Console.ReadLine());
                    t = true;
                }
                catch (Exception)
                {
                    Console.WriteLine("输入错误");
                }
            }

            foreach (var item in File.ReadAllLines($"address{index}.txt"))
            {
                var address = item.Split('\t')[0];
                var count = Convert.ToInt32(item.Split('\t')[1]);
                for (int i = 0; i < count; i++)
                {
                    var token = tokensOf.FirstOrDefault(p => p.StartsWith("Blind Box"));
                    if (token != null)
                    {
                        var hash = Transfer(address, token);
                        tokensOf.Remove(token);
                        var log = $"转账：{address}\t{token}\thttps://explorer.onegate.space/transactionInfo/{hash}";
                        Console.WriteLine(log);
                        Log(index, log);
                    }
                    else
                    {
                        Console.WriteLine("盲盒不足");
                    }
                }
            }

            Console.WriteLine(DateTime.Now.ToString());
            Console.WriteLine($"TotalSupply: {GetTotalSupply()}");
            Console.WriteLine($"盲盒已售出: {GetTotalMint(0, 0)} （含空投）");
            for (int i = 0; i < 9; i++)
            {
                Console.WriteLine($"碎片{(char)('A' + i)}已开出数量: {GetTotalMint(1, i)}");
            }
            Console.WriteLine($"其中编号<=500的碎片数量: {GetCounter()}");
            Console.WriteLine($"N 卡牌合成数量: {GetTotalMint(2, 0)}");
            Console.WriteLine($"E 卡牌合成数量: {GetTotalMint(2, 1)}");
            Console.WriteLine($"O 卡牌合成数量: {GetTotalMint(2, 2)}");

            Console.WriteLine($"可用于空投的盲盒数量: {tokensOf.Count}");
            Console.WriteLine("end");
            Console.ReadLine();

            //Console.ReadLine();
        }

        private static void Log(int index, string text)
        {
            File.AppendAllText($"log{index}.txt", text + "\r\n");
        }

        private static string GetCounter()
        {
            var body = "{\"jsonrpc\": \"2.0\",\"id\": 1,\"method\": \"getstorage\",\"params\": [\"" + ContractHash + "\",\"EWNvdW50ZXI=\"]}";
            var jsonResponse = Helper.PostWebRequest(URL, body);
            var counter = JObject.Parse(jsonResponse)["result"].ToString();
            return new BigInteger(Convert.FromBase64String(counter)).ToString();
        }

        private static List<string> TokensOf(string owner)
        {
            var body = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"invokefunction\",\"params\":[\"" + ContractHash + "\",\"tokensOf\",[{\"type\":\"Hash160\",\"value\":\"" + owner + "\"}]]}";
            var jsonResponse = Helper.PostWebRequest(URL, body);
            var response = JObject.Parse(jsonResponse);

            var sid = response["result"]["session"].ToString();
            var iid = response["result"]["stack"][0]["id"].ToString();
            return TraverseIterator(sid, iid);
        }
        private static List<string> TraverseIterator(string sid, string iid)
        {
            var body = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"traverseiterator\",\"params\":[\"" + sid + "\",\"" + iid + "\",10000]}";
            var jsonResponse = Helper.PostWebRequest(URL, body);
            var response = JObject.Parse(jsonResponse);

            var list = new List<string>();
            foreach (var item in response["result"])
            {
                var tokenId = Encoding.Default.GetString(Convert.FromBase64String(item["value"].ToString()));
                list.Add(tokenId);
            }
            return list.OrderBy(p => p).ToList();
        }

        private static string GetTotalSupply()
        {
            var body = "{\"jsonrpc\": \"2.0\",\"id\": 1,\"method\": \"invokefunction\",\"params\": [\"" + ContractHash + "\",\"totalSupply\",[]]}";
            var jsonResponse = Helper.PostWebRequest(URL, body);
            return JObject.Parse(jsonResponse)["result"]["stack"][0]["value"].ToString();
        }

        private static string GetTotalMint(int firstType, int secondType)
        {
            var body = "{\"jsonrpc\": \"2.0\",\"id\": 1,\"method\": \"invokefunction\",\"params\": [\"" + ContractHash + "\",\"totalMint\",[{\"type\": \"Integer\",\"value\": \"" + firstType + "\"},{\"type\":\"Integer\",\"value\": \"" + secondType + "\"}]]}";
            var jsonResponse = Helper.PostWebRequest(URL, body);
            return JObject.Parse(jsonResponse)["result"]["stack"][0]["value"].ToString();
        }

        private static string Transfer(string toAddress, string tokenId)
        {
            OpenWallet();
            try
            {
                var scriptHash = toAddress.ToScriptHash(0x35).ToString();
                tokenId = Convert.ToBase64String(Encoding.Default.GetBytes(tokenId));

                var body = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"invokefunction\",\"params\":[\"" + ContractHash + "\",\"transfer\",[{\"type\":\"Hash160\",\"value\":\"" + scriptHash + "\"},{\"type\":\"ByteArray\",\"value\":\"" + tokenId + "\"},{\"type\":\"Integer\",\"value\":1}],[{\"account\":\"" + OwnerScriptHash + "\",\"scopes\":\"CalledByEntry\"}]]}";

                var jsonResponse = Helper.PostWebRequest(URL, body);
                if (bool.Parse(JObject.Parse(jsonResponse)["result"]["stack"][0]["value"].ToString()))
                {
                    var tx = JObject.Parse(jsonResponse)["result"]["tx"].ToString();
                    return SendRawTransaction(tx);
                }
                else
                {
                    return JObject.Parse(jsonResponse)["result"]["exception"].ToString();
                }
            }
            catch (Exception)
            {
                return "转账失败";
            }
        }

        private static void OpenWallet()
        {
            if (!IsOpened)
            {
                var body = "{\"jsonrpc\":\"2.0\",\"method\":\"openwallet\",\"params\":[\"a.json\",\"1\"],\"id\":1}";
                Helper.PostWebRequest(URL, body);
                IsOpened = true;
            }
        }

        private static string SendRawTransaction(string raw)
        {
            var body = "{\"jsonrpc\":\"2.0\",\"method\":\"sendrawtransaction\",\"params\":[\"" + raw + "\"],\"id\":1}";
            var jsonResponse = Helper.PostWebRequest(URL, body);
            var hash = JObject.Parse(jsonResponse)["result"]["hash"].ToString();
            return hash;
        }

        public static string Base64StringToAddress(string base64)
        {
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(base64.Trim());
                if (bytes.Length != 20) throw new FormatException();
            }
            catch (Exception)
            {
                throw new FormatException();
            }
            return new UInt160(bytes).ToAddress(0x35);
        }
    }
}
