using BitcoinCore.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace BitcoinCore
{
    internal class CrashLog
    {
        private static readonly HttpClient client = new HttpClient();

        public static async Task SendLog(string message)
        {
            
        }

    }
}
