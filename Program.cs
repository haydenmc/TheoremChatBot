using System;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Theorem.Models;

namespace Theorem
{
    public class Program
    {
        public static void Main(string[] args)
        {
            new Program().Start().Wait();
        }
        
        public async Task Start()
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.BaseAddress = new Uri("https://slack.com/api/");
                var startResult = await httpClient.GetAsync("rtm.start?token=TOKENHERE");
                var startStringResult = await startResult.Content.ReadAsStringAsync();
                var startResponse = JsonConvert.DeserializeObject<StartResponseModel>(startStringResult);
                if (startResult.IsSuccessStatusCode && startResponse.Ok)
                {
                    Console.WriteLine(startResponse.Url);
                } else
                {
                    throw new Exception("Failed to open connection via rtm.start."); //TODO: Better error handling.
                }
            }
        }
    }
}
