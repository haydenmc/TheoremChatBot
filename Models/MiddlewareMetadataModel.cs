using System.ComponentModel.DataAnnotations.Schema;
using Theorem.ChatServices;

namespace Theorem.Models
{
    public class MiddlewareMetadataModel
    {
        public string Name { get; set; }

        public bool Enabled { get; set; }

        public bool Configured { get; set; }

        public int ExecutionOrderNumber { get; set; }

        public bool IsSummonable { get; set; }

        public string SummonVerb { get; set; }
    }
}