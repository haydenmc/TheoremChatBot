using System.Collections.Generic;
using Newtonsoft.Json;

namespace Theorem.Models.Slack
{
    public class SlackAttachmentModel
    {
        [JsonProperty("fallback")]
        public string Fallback { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("title_link")]
        public string TitleLink { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }
        
        [JsonProperty("fields")]
        public List<SlackAttachmentFieldModel> Fields { get; set; }

        [JsonProperty("image_url")]
        public string ImageUrl { get; set; }

        [JsonProperty("thumb_url")]
        public string ThumbUrl { get; set; }
    }
}