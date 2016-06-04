using Newtonsoft.Json;

namespace Theorem.Models.Slack
{
    public class SlackUserModel
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        
        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("deleted")]
        public bool Deleted { get; set; }
        
        [JsonProperty("profile")]
        public UserProfileModel Profile { get; set; }
        
        [JsonProperty("is_admin")]
        public bool IsAdmin { get; set; }
        
        [JsonProperty("is_owner")]
        public bool IsOwner { get; set; }
        
        [JsonProperty("is_primary_owner")]
        public bool IsPrimaryOwner { get; set; }
        
        [JsonProperty("is_unrestricted")]
        public bool IsUnrestricted { get; set; }
        
        [JsonProperty("is_ultra_unrestricted")]
        public bool IsUltraUnrestricted { get; set; }
        
        [JsonProperty("has_2fa")]
        public bool HasTwoFactorAuth { get; set; }
        
        [JsonProperty("two_factor_type")]
        public string TwoFactorType { get; set; }
        
        [JsonProperty("has_files")]
        public bool HasFiles { get; set; }
        
        public UserModel ToUserModel()
        {
            return new UserModel()
            {
                SlackId                 = Id,
                Name                    = Name,
                Deleted                 = Deleted,
                FirstName               = Profile?.FirstName,
                LastName                = Profile?.LastName,
                RealName                = Profile?.RealName,
                Email                   = Profile?.Email,
                Skype                   = Profile?.Skype,
                Phone                   = Profile?.Phone,
                Image512                = Profile?.Image512,
                IsAdmin                 = IsAdmin,
                IsOwner                 = IsOwner,
                IsPrimaryOwner          = IsPrimaryOwner,
                IsUnrestricted          = IsUnrestricted,
                IsUltraUnrestricted     = IsUltraUnrestricted,
                HasTwoFactorAuth        = HasTwoFactorAuth,
                TwoFactorType           = TwoFactorType,
                HasFiles                = HasFiles
            };
        }
    }
}