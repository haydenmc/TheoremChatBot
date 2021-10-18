using Newtonsoft.Json;

namespace Theorem.Models
{
    public class RhymeModel
    {
        [JsonProperty("flags")]
        public string Flags { get; set; }
        
        [JsonProperty("freq")]
        public int Frequency { get; set; }
        
        [JsonProperty("score")]
        public int Score { get; set; }
        
        [JsonProperty("syllables")]
        public int Syllables { get; set; }
        
        [JsonProperty("word")]
        public string Word { get; set; }
    }
}