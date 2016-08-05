using System;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Theorem.Models.Events
{
    public class PresenceChangeEventModel : EventModel
    {
        [JsonProperty("presence")]
        public string Presence { get; set;}
    }
}