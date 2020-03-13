using Microsoft.EntityFrameworkCore;

namespace Theorem.Models
{
    public class TheoremDbContext : DbContext
    {
        public DbSet<ChatMessageModel> ChatMessages { get; set; }
        
        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite("Data Source=theorem.db");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Composite key to make sure we don't collide across services,
            // service instances, or channels (Slack only guarantees unique
            // IDs per channel).
            modelBuilder.
                Entity<ChatMessageModel>().
                HasKey(c => new { c.Id, c.ProviderInstance, c.ChannelId });

            // Index commonly queried fields
            modelBuilder.Entity<ChatMessageModel>().
                HasIndex(c => c.Id);
            modelBuilder.Entity<ChatMessageModel>().
                HasIndex(c => c.Provider);
            modelBuilder.Entity<ChatMessageModel>().
                HasIndex(c => c.ProviderInstance);
            modelBuilder.Entity<ChatMessageModel>().
                HasIndex(c => c.AuthorId);
            modelBuilder.Entity<ChatMessageModel>().
                HasIndex(c => c.ChannelId);
            modelBuilder.Entity<ChatMessageModel>().
                HasIndex(c => c.TimeSent);
            modelBuilder.Entity<ChatMessageModel>().
                HasIndex(c => c.ThreadingId);
        }
    }
}