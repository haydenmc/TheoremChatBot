using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Theorem.Models.Events;

namespace Theorem.Models
{
    public class ApplicationDbContext : DbContext
    {
        private string _connectionString { get; set; }
        
        public DbSet<EventModel> Events { get; set; }
        public DbSet<MessageEventModel> MessageEvents { get; set; }
        public DbSet<PresenceChangeEventModel> PresenceChangeEvents { get; set; }
        
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
    }
}