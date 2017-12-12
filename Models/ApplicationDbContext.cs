using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Theorem.Models.Events;
using Theorem.Models.Slack;

namespace Theorem.Models
{
    public class ApplicationDbContext : DbContext
    {
        private string _connectionString { get; set; }
        
        public DbSet<SavedDataPair> SavedDataPairs { get; set; }
        public DbSet<UserModel> Users { get; set; }
        public DbSet<ChannelModel> Channels { get; set; }
        public DbSet<ChannelMemberModel> ChannelMembers { get; set; }
        public DbSet<EventModel> Events { get; set; }
        public DbSet<MessageEventModel> MessageEvents { get; set; }
        public DbSet<PresenceChangeEventModel> PresenceChangeEvents { get; set; }
        public DbSet<TypingEventModel> TypingEvents { get; set; }
        public DbSet<ChannelJoinedEventModel> ChannelJoinedEvents { get; set; }
        public DbSet<ChannelCreatedEventModel> ChannelCreatedEvents { get; set; }
        
        public ApplicationDbContext() : base() 
        {
            _connectionString = @"Server=(localdb)\mssqllocaldb;Database=Theorem;Trusted_Connection=True;";
        }
        
        public ApplicationDbContext(IConfigurationRoot configuration) : base()
        {
            _connectionString = configuration["Database:ConnectionString"];
        }

        /// <summary>
        /// Adds or updates the database copy of a Slack user given a Slack user model
        /// </summary>
        /// <param name="slackUser">The Slack user model</param>
        /// <returns>Updated database model of Slack user</returns>
        public UserModel AddOrUpdateDbUser(SlackUserModel slackUser)
        {
            var dbUser = Users.SingleOrDefault(u => u.SlackId == slackUser.Id);
            if (dbUser == null)
            {
                dbUser = new UserModel()
                {
                    Id = Guid.NewGuid()
                };
                Users.Add(dbUser);
            }
            dbUser.SlackId = slackUser.Id;
            dbUser.Name = slackUser.Name;
            dbUser.Deleted = slackUser.Deleted;
            if (slackUser.Profile != null)
            {
                dbUser.FirstName = slackUser.Profile.FirstName;
                dbUser.LastName = slackUser.Profile.LastName;
                dbUser.RealName = slackUser.Profile.RealName;
                dbUser.Email = slackUser.Profile.Email;
                dbUser.Skype = slackUser.Profile.Skype;
                dbUser.Phone = slackUser.Profile.Phone;
                dbUser.Image512 = slackUser.Profile.Image512;
            }
            dbUser.IsAdmin = slackUser.IsAdmin;
            dbUser.IsOwner = slackUser.IsOwner;
            dbUser.IsPrimaryOwner = slackUser.IsPrimaryOwner;
            dbUser.IsUnrestricted = slackUser.IsUnrestricted;
            dbUser.IsUltraUnrestricted = slackUser.IsUltraUnrestricted;
            dbUser.HasTwoFactorAuth = slackUser.HasTwoFactorAuth;
            dbUser.TwoFactorType = slackUser.TwoFactorType;
            dbUser.HasFiles = slackUser.HasFiles;
            SaveChanges();
            return dbUser;
        }

        /// <summary>
        /// Adds or updates the database copy of a Slack channel given a Slack channel model
        /// </summary>
        /// <param name="slackChannel">The Slack channel model</param>
        /// <returns>Updated database model of Slack channel</returns>
        public ChannelModel AddOrUpdateDbChannel(SlackChannelModel slackChannel)
        {
            var dbChannel = Channels
                .Include(c => c.ChannelMembers)
                .SingleOrDefault(c => c.SlackId == slackChannel.Id);
            if (dbChannel == null)
            {
                dbChannel = new ChannelModel()
                {
                    Id = Guid.NewGuid(),
                    ChannelMembers = new List<ChannelMemberModel>()
                };
                Channels.Add(dbChannel);
            }
            // Update fields
            dbChannel.SlackId = slackChannel.Id;
            dbChannel.Name = slackChannel.Name;
            dbChannel.IsChannel = slackChannel.IsChannel;
            dbChannel.TimeCreated = slackChannel.TimeCreated;
            dbChannel.CreatorSlackId = slackChannel.CreatorId;
            dbChannel.CreatorId = Users.Single(u => u.SlackId == slackChannel.CreatorId).Id;
            dbChannel.IsArchived = slackChannel.IsArchived;
            dbChannel.IsGeneral = slackChannel.IsGeneral;
            if (slackChannel.MemberIds != null)
            {
                var existingMembersToRemove = dbChannel.ChannelMembers.ToList();
                foreach (var memberId in slackChannel.MemberIds)
                {
                    var existingMember = existingMembersToRemove
                        .SingleOrDefault(m => m.ChannelId == dbChannel.Id
                            && m.Member.SlackId == memberId);
                    if (existingMember == null)
                    {
                        dbChannel.ChannelMembers.Add(new ChannelMemberModel()
                        {
                            ChannelId = dbChannel.Id,
                            MemberId = Users.Single(u => u.SlackId == memberId).Id
                        });
                    }
                    else
                    {
                        existingMembersToRemove.Remove(existingMember);
                        dbChannel.ChannelMembers.Add(existingMember);
                    }
                }
                ChannelMembers.RemoveRange(existingMembersToRemove);
            }
            dbChannel.Topic = slackChannel.Topic?.Value;
            dbChannel.Purpose = slackChannel.Purpose?.Value;
            dbChannel.IsMember = slackChannel.IsMember;
            dbChannel.TimeLastRead = slackChannel.TimeLastRead;
            SaveChanges();
            return dbChannel;
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
            modelBuilder.Entity<SavedDataPair>()
                .HasKey(k => new { k.Area, k.Key });

            // specify precision of decimal values for message event data
            modelBuilder.Entity<MessageEventModel>()
                .Property(m => m.SlackTimeSent)
                .HasColumnType("decimal(18,6)");
            modelBuilder.Entity<MessageEventModel>()
                .Property(m => m.SlackThreadId)
                .HasColumnType("decimal(18,6)");
        } 
    }
}