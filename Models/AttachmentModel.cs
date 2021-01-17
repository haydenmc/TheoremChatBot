namespace Theorem.Models
{
    public enum AttachmentKind
    {
        Unknown = 0,
        Image,
    }
    
    public class AttachmentModel
    {
        public virtual AttachmentKind Kind { get; set; }

        public virtual string Name { get; set; }

        public virtual string Uri { get; set; }
    }
}