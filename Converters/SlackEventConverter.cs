using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Theorem.Models;
using Theorem.Models.Events;

namespace Theorem.Converters
{
    public class SlackEventConverter : JsonCreationConverter<SlackEventModel>
    {
        public new bool CanWrite = false;
        
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        protected override Type GetType(Type objectType, JObject jObject)
        {
            var type = (string)jObject.Property("type");
            switch (type)
            {
                case "message":
                    return typeof(MessageEventModel);
                case "presence_change":
                    return typeof(PresenceChangeEventModel);
                case "user_typing":
                    return typeof(TypingEventModel);
                case "channel_joined":
                    return typeof(ChannelJoinedEventModel);
                case "channel_created":
                    return typeof(ChannelCreatedEventModel);
            }

            return typeof(SlackEventModel);
        }
    }
}