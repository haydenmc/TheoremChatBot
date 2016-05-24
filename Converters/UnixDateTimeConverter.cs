using System;
using Newtonsoft.Json;

namespace Theorem.Converters
{
    public class UnixDateTimeConverter : Newtonsoft.Json.JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(DateTime);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.Value.GetType() == typeof(long))
            {
                return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds((long)reader.Value);
            }
            else if (reader.Value.GetType() == typeof(double))
            {
                return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds((double)reader.Value);
            }
            else if (reader.Value.GetType() == typeof(int))
            {
                return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds((int)reader.Value);
            }
            else if (reader.Value.GetType() == typeof(string))
            {
                if (((string)reader.Value).Contains("."))
                {
                    return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(double.Parse((string)reader.Value));
                }
                else
                {
                    return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(long.Parse((string)reader.Value));
                }
            }
            throw new InvalidCastException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}