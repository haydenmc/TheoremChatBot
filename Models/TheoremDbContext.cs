using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Theorem.Models.Slack;
using Theorem.Models.Slack.Events;

namespace Theorem.Models
{
    public class TheoremDbContext : DbContext
    {
        public DbSet<ChatMessageModel> ChatMessages { get; set; }
        
        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite("Data Source=theorem.db");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        { } 
    }
}