using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Theorem.Models;

namespace TheoremSlackBot.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    partial class ApplicationDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "1.0.0-rtm-21431")
                .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

            modelBuilder.Entity("Theorem.Models.ChannelMemberModel", b =>
                {
                    b.Property<Guid>("ChannelId");

                    b.Property<Guid>("MemberId");

                    b.HasKey("ChannelId", "MemberId");

                    b.HasIndex("ChannelId");

                    b.HasIndex("MemberId");

                    b.ToTable("ChannelMembers");
                });

            modelBuilder.Entity("Theorem.Models.ChannelModel", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<Guid>("CreatorId");

                    b.Property<string>("CreatorSlackId");

                    b.Property<bool>("IsArchived");

                    b.Property<bool>("IsChannel");

                    b.Property<bool>("IsGeneral");

                    b.Property<bool>("IsMember");

                    b.Property<string>("Name");

                    b.Property<string>("Purpose");

                    b.Property<string>("SlackId");

                    b.Property<DateTimeOffset>("TimeCreated");

                    b.Property<DateTimeOffset>("TimeLastRead");

                    b.Property<string>("Topic");

                    b.HasKey("Id");

                    b.HasIndex("CreatorId");

                    b.HasIndex("SlackId")
                        .IsUnique();

                    b.ToTable("Channels");
                });

            modelBuilder.Entity("Theorem.Models.Events.EventModel", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<Guid?>("ChannelId");

                    b.Property<string>("Discriminator")
                        .IsRequired();

                    b.Property<string>("SlackEventType");

                    b.Property<DateTimeOffset>("TimeReceived");

                    b.Property<Guid?>("UserId");

                    b.HasKey("Id");

                    b.HasIndex("ChannelId");

                    b.HasIndex("UserId");

                    b.ToTable("Events");

                    b.HasDiscriminator<string>("Discriminator").HasValue("EventModel");
                });

            modelBuilder.Entity("Theorem.Models.UserModel", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<bool>("Deleted");

                    b.Property<string>("Email");

                    b.Property<string>("FirstName");

                    b.Property<bool>("HasFiles");

                    b.Property<bool>("HasTwoFactorAuth");

                    b.Property<string>("Image512");

                    b.Property<bool>("IsAdmin");

                    b.Property<bool>("IsOwner");

                    b.Property<bool>("IsPrimaryOwner");

                    b.Property<bool>("IsUltraUnrestricted");

                    b.Property<bool>("IsUnrestricted");

                    b.Property<string>("LastName");

                    b.Property<string>("Name");

                    b.Property<string>("Phone");

                    b.Property<string>("RealName");

                    b.Property<string>("Skype");

                    b.Property<string>("SlackId");

                    b.Property<string>("TwoFactorType");

                    b.HasKey("Id");

                    b.HasIndex("SlackId")
                        .IsUnique();

                    b.ToTable("Users");
                });

            modelBuilder.Entity("Theorem.Models.Events.PresenceChangeEventModel", b =>
                {
                    b.HasBaseType("Theorem.Models.Events.EventModel");

                    b.Property<string>("Presence");

                    b.ToTable("PresenceChangeEventModel");

                    b.HasDiscriminator().HasValue("PresenceChangeEventModel");
                });

            modelBuilder.Entity("Theorem.Models.MessageEventModel", b =>
                {
                    b.HasBaseType("Theorem.Models.Events.EventModel");

                    b.Property<Guid?>("ChannelModelId");

                    b.Property<string>("TeamId");

                    b.Property<string>("Text");

                    b.Property<DateTime>("TimeSent");

                    b.Property<Guid?>("UserModelId");

                    b.HasIndex("ChannelModelId");

                    b.HasIndex("UserModelId");

                    b.ToTable("MessageEventModel");

                    b.HasDiscriminator().HasValue("MessageEventModel");
                });

            modelBuilder.Entity("Theorem.Models.TypingEventModel", b =>
                {
                    b.HasBaseType("Theorem.Models.Events.EventModel");


                    b.ToTable("TypingEventModel");

                    b.HasDiscriminator().HasValue("TypingEventModel");
                });

            modelBuilder.Entity("Theorem.Models.ChannelMemberModel", b =>
                {
                    b.HasOne("Theorem.Models.ChannelModel", "Channel")
                        .WithMany("ChannelMembers")
                        .HasForeignKey("ChannelId");

                    b.HasOne("Theorem.Models.UserModel", "Member")
                        .WithMany("Channels")
                        .HasForeignKey("MemberId");
                });

            modelBuilder.Entity("Theorem.Models.ChannelModel", b =>
                {
                    b.HasOne("Theorem.Models.UserModel", "Creator")
                        .WithMany()
                        .HasForeignKey("CreatorId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("Theorem.Models.Events.EventModel", b =>
                {
                    b.HasOne("Theorem.Models.ChannelModel", "Channel")
                        .WithMany()
                        .HasForeignKey("ChannelId");

                    b.HasOne("Theorem.Models.UserModel", "User")
                        .WithMany()
                        .HasForeignKey("UserId");
                });

            modelBuilder.Entity("Theorem.Models.MessageEventModel", b =>
                {
                    b.HasOne("Theorem.Models.ChannelModel")
                        .WithMany("Messages")
                        .HasForeignKey("ChannelModelId");

                    b.HasOne("Theorem.Models.UserModel")
                        .WithMany("Messages")
                        .HasForeignKey("UserModelId");
                });
        }
    }
}
