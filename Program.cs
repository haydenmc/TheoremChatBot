using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Theorem.Models;

namespace Theorem
{
    public class Program
    {
        public const string BaseApiUrl = "https://slack.com/api/";
        
        public IConfigurationRoot Configuration { get; set; }
        
        private string _apiToken
        {
            get
            {
                return Configuration["Slack:ApiToken"];
            }
        }
        
        private string _webSocketUrl { get; set; }
        
        private Dictionary<string, ChannelModel> _channels { get; set; }
        
        public static void Main(string[] args)
        {
            new Program().Start().Wait();
        }
        
        public async Task Start()
        {
            Configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.default.json")
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();
            using (var httpClient = new HttpClient())
            {
                httpClient.BaseAddress = new Uri(BaseApiUrl);
                var startResult = await httpClient.GetAsync($"rtm.start?token={_apiToken}");
                var startStringResult = await startResult.Content.ReadAsStringAsync();
                var startResponse = JsonConvert.DeserializeObject<StartResponseModel>(startStringResult);
                if (!startResult.IsSuccessStatusCode || !startResponse.Ok)
                {
                    throw new Exception("Failed to open connection via rtm.start."); //TODO: Better error handling.
                }
                
                // Set socket URL
                _webSocketUrl = startResponse.Url;
                _channels = startResponse.Channels.ToDictionary(c => c.Id);
                
                // Connect to websocket endpoint
                var webSocketClient = new ClientWebSocket();
                await webSocketClient.ConnectAsync(new Uri(_webSocketUrl), CancellationToken.None);
                await Task.WhenAll(Receive(webSocketClient), SayHello());
            }
        }
        
        public async Task SayHello()
        {
            await SendMessage(_channels.Values.SingleOrDefault(c => c.IsGeneral).Id, "Beep boop bop. I am a robot.");
        }
        
        private async Task Receive(ClientWebSocket webSocketClient)
        {
            while (webSocketClient.State == WebSocketState.Open)
            {
                byte[] buffer = new byte[1024];
                var result = await webSocketClient.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocketClient.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                }
                // while (!result.EndOfMessage)
                // {
                    var messageString = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Console.WriteLine(messageString);
                // }
            }
        }
        
        private async Task SendMessage(string channelId, string body)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.BaseAddress = new Uri(BaseApiUrl);
                var postData = new FormUrlEncodedContent(new[] { 
                    new KeyValuePair<string, string>("token", _apiToken), 
                    new KeyValuePair<string, string>("channel", channelId), 
                    new KeyValuePair<string, string>("text", body),
                    new KeyValuePair<string, string>("as_user", "true")
                }); 
                var result = await httpClient.PostAsync("chat.postMessage", postData);
                // TODO: Parse result, handle errors, retry, etc.
            }
        }
    }
}
