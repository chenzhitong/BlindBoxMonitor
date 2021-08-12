using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BlindBoxMonitor
{
    class Program
    {
        static readonly string URL = "http://localhost:10332";

        static readonly string ContractHash = "0x2bcc9c9ad6626396f507f088c5ae06ebf6fa5efa";

        static readonly string OwnerScriptHash = "0x4578060c29f4c03f1e16c84312429d991952c94c";

        static void Main(string[] args)
        {
            Console.WriteLine("盲盒合约监控");

            Console.WriteLine("前 100 个 NFT:");
            foreach (var item in Tokens())
            {
                var tokenIdBase64 = Convert.ToBase64String(Encoding.Default.GetBytes(item));
                Console.WriteLine(item + "\t" + tokenIdBase64);
                //Console.WriteLine("转账：" + Transfer("0x96d5942028891de8e5d866f504b36ff5ae13ab63", tokenIdBase64));
                
            }

            Console.WriteLine($"总发行数量: {GetTotalSupply()}");
            Console.WriteLine($"盲盒已售出: {GetTotalMint(0, 0)}");
            for (int i = 0; i < 9; i++)
            {
                Console.WriteLine($"碎片{(char)('A' + i)}已开出数量: {GetTotalMint(1, i)}");
            }
            Console.WriteLine($"金卡牌合成数量: {GetTotalMint(2, 0)}");
            Console.WriteLine($"银卡牌合成数量: {GetTotalMint(2, 1)}");
            Console.WriteLine($"铜卡牌合成数量: {GetTotalMint(2, 2)}");

            //Console.WriteLine("打开所有的盲盒");
            //for (int i = 31; i <= 60; i++)
            //{
            //    var tokenId = Convert.ToBase64String(Encoding.Default.GetBytes($"Blind Box {i}"));
            //    var hash = UnBoxing(tokenId);
            //    Console.WriteLine("开盲盒 Blind Box" + i + "\t" + hash);
            //}

            //var hash = AirDrop("0x96d5942028891de8e5d866f504b36ff5ae13ab63", 2);
            //Console.WriteLine("空投盲盒：" + hash);


            Console.ReadLine();
        }

        private static List<string> Tokens()
        {
            var body = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"invokefunction\",\"params\":[\"" + ContractHash + "\",\"tokens\",[]]}";
            var jsonResponse = Helper.PostWebRequest(URL, body);
            var response = JObject.Parse(jsonResponse);

            var list = new List<string>();
            foreach (var item in response["result"]["stack"][0]["iterator"])
            {
                var tokenId = Encoding.Default.GetString(Convert.FromBase64String(item["value"].ToString()));
                list.Add(tokenId);
            }
            return list.OrderBy(p => p.Length).ThenBy(p => p).ToList();
        }

        private static List<string> TokensOf()
        {
            var body = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"invokefunction\",\"params\":[\"" + ContractHash + "\",\"tokensOf\",[{\"type\":\"Hash160\",\"value\":\"" + OwnerScriptHash + "\"}]]}";
            var jsonResponse = Helper.PostWebRequest(URL, body);
            var response = JObject.Parse(jsonResponse);

            var list = new List<string>();
            foreach (var item in response["result"]["stack"][0]["iterator"])
            {
                var tokenId = Encoding.Default.GetString(Convert.FromBase64String(item["value"].ToString()));
                list.Add(tokenId);
            }
            return list.OrderBy(p => p.Length).ThenBy(p => p).ToList();
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

        private static string AirDrop(string toAddress, int amount)
        {
            OpenWallet();

            var body = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"invokefunction\",\"params\":[\"" + ContractHash + "\",\"airdrop\",[{\"type\":\"Hash160\",\"value\":\"" + toAddress + "\"},{\"type\":\"Integer\",\"value\":\"" + amount + "\"}],[{\"account\":\"" + OwnerScriptHash + "\",\"scopes\":\"CalledByEntry\",\"allowedcontracts\":[],\"allowedgroups\":[]}]]}";

            var jsonResponse = Helper.PostWebRequest(URL, body);
            if (bool.Parse(JObject.Parse(jsonResponse)["result"]["stack"][0]["value"].ToString()))
            {
                var tx = JObject.Parse(jsonResponse)["result"]["tx"].ToString();
                return SendRawTransaction(tx);
            }
            else
            {
                return JObject.Parse(jsonResponse)["result"].ToString();
            }
        }

        private static string Transfer(string toAddress, string tokenId)
        {
            OpenWallet();

            var body = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"invokefunction\",\"params\":[\"" + ContractHash + "\",\"transfer\",[{\"type\":\"Hash160\",\"value\":\"" + toAddress + "\"},{\"type\":\"ByteArray\",\"value\":\"" + tokenId + "\"},{\"type\":\"Integer\",\"value\":1}],[{\"account\":\"" + OwnerScriptHash + "\",\"scopes\":\"CalledByEntry\"}]]}";

            var jsonResponse = Helper.PostWebRequest(URL, body);
            if (bool.Parse(JObject.Parse(jsonResponse)["result"]["stack"][0]["value"].ToString()))
            {
                var tx = JObject.Parse(jsonResponse)["result"]["tx"].ToString();
                return SendRawTransaction(tx);
            }
            else
            {
                return JObject.Parse(jsonResponse)["result"].ToString();
            }
        }

        private static void OpenWallet()
        {
            var body = "{\"jsonrpc\":\"2.0\",\"method\":\"openwallet\",\"params\":[\"a.json\",\"1\"],\"id\":1}";
            Helper.PostWebRequest(URL, body);
        }

        private static string SendRawTransaction(string raw)
        {
            var body = "{\"jsonrpc\":\"2.0\",\"method\":\"sendrawtransaction\",\"params\":[\"" + raw + "\"],\"id\":1}";
            var jsonResponse = Helper.PostWebRequest(URL, body);
            var hash = JObject.Parse(jsonResponse)["result"]["hash"].ToString();
            return hash;
        }

        private static string UnBoxing(string tokenId)
        {
            OpenWallet();

            var body = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"invokefunction\",\"params\":[\"" + ContractHash + "\",\"unBoxing\",[{\"type\":\"ByteArray\",\"value\":\"" + tokenId + "\"}],[{\"account\":\"" + OwnerScriptHash + "\",\"scopes\":\"CalledByEntry\",\"allowedcontracts\":[],\"allowedgroups\":[]}]]}";
            var jsonResponse = Helper.PostWebRequest(URL, body);

            if (JObject.Parse(jsonResponse)["result"]["state"].ToString() == "HALT")
            {
                var tx = JObject.Parse(jsonResponse)["result"]["tx"].ToString();
                return SendRawTransaction(tx);
            }
            else
            {
                return JObject.Parse(jsonResponse)["result"].ToString();
            }
        }
    }
}
