using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Theorem.Models.Events;

namespace Theorem.Models
{
    public class ApplicationDbContext : DbContext
    {
        private string _connectionString { get; set; }
        
        public DbSet<UserModel> Users { get; set; }
        public DbSet<ChannelModel> Channels { get; set; }
        public DbSet<ChannelMemberModel> ChannelMembers { get; set; }
        public DbSet<EventModel> Events { get; set; }
        public DbSet<MessageEventModel> MessageEvents { get; set; }
        public DbSet<PresenceChangeEventModel> PresenceChangeEvents { get; set; }
        public DbSet<TypingEventModel> TypingEvents { get; set; }
        
        public ApplicationDbContext() : base() 
        {
            _connectionString = @"Server=(localdb)\mssqllocaldb;Database=Theorem;Trusted_Connection=True;";
        }
        
        public ApplicationDbContext(IConfigurationRoot configuration) : base()
        {
            _connectionString = configuration["Database:ConnectionString"];
        }
        
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(_connectionString);
        }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserModel>().HasIndex(u => u.SlackId).IsUnique(true);
            modelBuilder.Entity<ChannelModel>().HasIndex(c => c.SlackId).IsUnique(true);
            modelBuilder.Entity<ChannelMemberModel>()
                .HasKey(c => new { c.ChannelId, c.MemberId });
            modelBuilder.Entity<ChannelMemberModel>()
                .HasOne(c => c.Channel)
                .WithMany(c => c.ChannelMembers)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<ChannelMemberModel>()
                .HasOne(c => c.Member)
                .WithMany(c => c.Channels)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}