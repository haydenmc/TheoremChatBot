using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Theorem.Converters;

namespace Theorem.Models
{
    public class ChannelModel
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        
        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("is_channel")]
        public bool IsChannel { get; set; }
        
        [JsonProperty("created")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime TimeCreated { get; set; }
        
        [JsonProperty("creator")]
        public string CreatorId { get; set; }
        
        [JsonProperty("is_archived")]
        public bool IsArchived { get; set; }
        
        [JsonProperty("is_general")]
        public bool IsGeneral { get; set; }
        
        [JsonProperty("members")]
        public List<string> MemberIds { get; set; }
        
        [JsonProperty("topic")]
        public ChannelValueModel Topic { get; set; }
        
        [JsonProperty("purpose")]
        public ChannelValueModel Purpose { get; set; }
        
        [JsonProperty("is_member")]
        public bool IsMember { get; set; }
        
        [JsonProperty("last_read")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime TimeLastRead { get; set; }
        
        [JsonProperty("latest")]
        public MessageModel LatestMessage { get; set; }
        
        [JsonProperty("unread_count")]
        public int UnreadCount { get; set; }
        
        [JsonProperty("unread_count_display")]
        public int UnreadCountDisplay { get; set; }
    }
}