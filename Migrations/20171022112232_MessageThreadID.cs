using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TheoremSlackBot.Migrations
{
    public partial class MessageThreadID : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn("TimeSent", "Events");
            migrationBuilder.AddColumn<decimal>(
                name: "SlackThreadId",
                table: "Events",
                nullable: true,
                type: "decimal(18,6)"
            );
            migrationBuilder.AddColumn<decimal>(
                name: "SlackTimeSent",
                table: "Events",
                nullable: true,
                type: "decimal(18,6)"
            );
            migrationBuilder.RenameColumn("TeamId", "Events", "SlackTeamId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "TimeSent",
                table: "Events",
                nullable: true
            );
            migrationBuilder.DropColumn("SlackThreadId", "Events");
            migrationBuilder.DropColumn("SlackTimeSent", "Events");
            migrationBuilder.RenameColumn("SlackTeamId", "Events", "TeamId");
        }
    }
}
