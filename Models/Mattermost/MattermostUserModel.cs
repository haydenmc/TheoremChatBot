using System;
using Newtonsoft.Json;
using Theorem.ChatServices;

namespace Theorem.Models.Mattermost
{
    public class MattermostUserModel : 
        IProvideUserModel
    {
        // {
        //     "id":"umowrqwsqbrw7bfjhkokmcep7a",
        //     "create_at":1582358305926,
        //     "update_at":1583890355339,
        //     "delete_at":0,
        //     "username":"hamc",
        //     "auth_data":"",
        //     "auth_service":"",
        //     "email":"hayden@outlook.com",
        //     "email_verified":true,
        //     "nickname":"",
        //     "first_name":"Hayden",
        //     "last_name":"McAfee",
        //     "position":"",
        //     "roles":"system_user system_admin",
        //     "notify_props":{
        //         "androidKeywords":"@hamc",
        //         "channel":"true",
        //         "comments":"never",
        //         "desktop":"mention",
        //         "desktop_sound":"true",
        //         "email":"false",
        //         "first_name":"true",
        //         "mention_keys":"@hamc,hamc",
        //         "newReplyValue":"never",
        //         "push":"mention",
        //         "push_status":"away",
        //         "user_id":"umowrqwsqbrw7bfjhkokmcep7a"
        //     },
        //     "last_password_update":1582428738776,
        //     "last_picture_update":1582568484637,
        //     "locale":"en",
        //     "timezone":{
        //         "automaticTimezone":"America/Los_Angeles",
        //         "manualTimezone":"","useAutomaticTimezone":"true"
        //     }
        // }
        
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("nickname")]
        public string Nickname { get; set; }

        [JsonProperty("first_name")]
        public string FirstName { get; set; }

        [JsonProperty("last_name")]
        public string LastName { get; set; }

        public UserModel ToUserModel(IChatServiceConnection chatServiceConnection)
        {
            return new UserModel()
            {
                Id = Id,
                Provider = ChatServiceKind.Mattermost,
                Name = Username,
                DisplayName = String.Join(" ", new string[] { FirstName, LastName }),
                FromChatServiceConnection = chatServiceConnection
            };
        }
    }
}