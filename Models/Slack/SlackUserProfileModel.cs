using Newtonsoft.Json;

namespace Theorem.Models.Slack
{
    public class UserProfileModel
    {
        [JsonProperty("first_name")]
        public string FirstName { get; set; }
        
        [JsonProperty("last_name")]
        public string LastName { get; set; }
        
        [JsonProperty("real_name")]
        public string RealName { get; set; }
        
        [JsonProperty("email")]
        public string Email { get; set; }
        
        [JsonProperty("skype")]
        public string Skype { get; set; }
        
        [JsonProperty("phone")]
        public string Phone { get; set; }
        
        [JsonProperty("image_512")]
        public string Image512 { get; set; }
    }
}