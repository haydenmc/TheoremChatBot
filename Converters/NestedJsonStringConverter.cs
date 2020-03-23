using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Theorem.Models.Mattermost;
using Theorem.Models.Mattermost.EventData;

namespace Theorem.Converters
{
    public class NestedJsonStringConverter : JsonConverter
    {
        public new bool CanWrite = false;
        
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer)
        {
            var jsonString = reader.Value as string;
            return JsonConvert.DeserializeObject(jsonString, objectType);
        }
    }
}