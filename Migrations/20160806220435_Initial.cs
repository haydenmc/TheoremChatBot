using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TheoremSlackBot.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    Deleted = table.Column<bool>(nullable: false),
                    Email = table.Column<string>(nullable: true),
                    FirstName = table.Column<string>(nullable: true),
                    HasFiles = table.Column<bool>(nullable: false),
                    HasTwoFactorAuth = table.Column<bool>(nullable: false),
                    Image512 = table.Column<string>(nullable: true),
                    IsAdmin = table.Column<bool>(nullable: false),
                    IsOwner = table.Column<bool>(nullable: false),
                    IsPrimaryOwner = table.Column<bool>(nullable: false),
                    IsUltraUnrestricted = table.Column<bool>(nullable: false),
                    IsUnrestricted = table.Column<bool>(nullable: false),
                    LastName = table.Column<string>(nullable: true),
                    Name = table.Column<string>(nullable: true),
                    Phone = table.Column<string>(nullable: true),
                    RealName = table.Column<string>(nullable: true),
                    Skype = table.Column<string>(nullable: true),
                    SlackId = table.Column<string>(nullable: true),
                    TwoFactorType = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Channels",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    CreatorId = table.Column<Guid>(nullable: false),
                    CreatorSlackId = table.Column<string>(nullable: true),
                    IsArchived = table.Column<bool>(nullable: false),
                    IsChannel = table.Column<bool>(nullable: false),
                    IsGeneral = table.Column<bool>(nullable: false),
                    IsMember = table.Column<bool>(nullable: false),
                    Name = table.Column<string>(nullable: true),
                    Purpose = table.Column<string>(nullable: true),
                    SlackId = table.Column<string>(nullable: true),
                    TimeCreated = table.Column<DateTimeOffset>(nullable: false),
                    TimeLastRead = table.Column<DateTimeOffset>(nullable: false),
                    Topic = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Channels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Channels_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChannelMembers",
                columns: table => new
                {
                    ChannelId = table.Column<Guid>(nullable: false),
                    MemberId = table.Column<Guid>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelMembers", x => new { x.ChannelId, x.MemberId });
                    table.ForeignKey(
                        name: "FK_ChannelMembers_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChannelMembers_Users_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    ChannelId = table.Column<Guid>(nullable: true),
                    Discriminator = table.Column<string>(nullable: false),
                    SlackEventType = table.Column<string>(nullable: true),
                    TimeReceived = table.Column<DateTimeOffset>(nullable: false),
                    UserId = table.Column<Guid>(nullable: true),
                    Presence = table.Column<string>(nullable: true),
                    ChannelModelId = table.Column<Guid>(nullable: true),
                    TeamId = table.Column<string>(nullable: true),
                    Text = table.Column<string>(nullable: true),
                    TimeSent = table.Column<DateTime>(nullable: true),
                    UserModelId = table.Column<Guid>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Events_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Events_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Events_Channels_ChannelModelId",
                        column: x => x.ChannelModelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Events_Users_UserModelId",
                        column: x => x.UserModelId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMembers_ChannelId",
                table: "ChannelMembers",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMembers_MemberId",
                table: "ChannelMembers",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Channels_CreatorId",
                table: "Channels",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Channels_SlackId",
                table: "Channels",
                column: "SlackId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Events_ChannelId",
                table: "Events",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_UserId",
                table: "Events",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_ChannelModelId",
                table: "Events",
                column: "ChannelModelId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_UserModelId",
                table: "Events",
                column: "UserModelId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_SlackId",
                table: "Users",
                column: "SlackId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChannelMembers");

            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropTable(
                name: "Channels");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
