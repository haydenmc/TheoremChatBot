using System.ComponentModel.DataAnnotations;

namespace Theorem.Models
{
    public class SavedDataPair
    {
        [MaxLength(128)]
        public string Area { get; set; }

        [MaxLength(128)]
        public string Key { get; set; }

        public string Value { get; set; }
    }
}