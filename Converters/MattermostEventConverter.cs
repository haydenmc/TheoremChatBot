using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Theorem.Models.Mattermost;
using Theorem.Models.Mattermost.EventData;

namespace Theorem.Converters
{
    public class MattermostEventConverter : JsonConverter
    {
        public new bool CanWrite = false;
        
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(IMattermostWebsocketMessageModel)
                .IsAssignableFrom(objectType);
        }

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer)
        {
            JObject obj = JObject.Load(reader);
            string eventType = (string)obj["event"];

            IMattermostWebsocketMessageModel messageModel;
            switch (eventType)
            {
                case "hello":
                    messageModel = 
                        new MattermostWebsocketMessageModel
                            <MattermostHelloEventDataModel>();
                    break;
                case "posted":
                    messageModel = 
                        new MattermostWebsocketMessageModel
                            <MattermostPostedEventDataModel>();
                    break;
                default:
                    // Log?
                    messageModel = 
                        new MattermostWebsocketMessageModel<dynamic>();
                    break;
            }
            serializer.Populate(obj.CreateReader(), messageModel);
            return messageModel;
        }
    }
}