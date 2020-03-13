using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TheoremSlackBot.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<string>(nullable: false),
                    ProviderInstance = table.Column<string>(nullable: false),
                    ChannelId = table.Column<string>(nullable: false),
                    Provider = table.Column<int>(nullable: false),
                    AuthorId = table.Column<string>(nullable: true),
                    Body = table.Column<string>(nullable: true),
                    TimeSent = table.Column<DateTimeOffset>(nullable: false),
                    ThreadingId = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => new { x.Id, x.ProviderInstance, x.ChannelId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_AuthorId",
                table: "ChatMessages",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ChannelId",
                table: "ChatMessages",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_Id",
                table: "ChatMessages",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_Provider",
                table: "ChatMessages",
                column: "Provider");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ProviderInstance",
                table: "ChatMessages",
                column: "ProviderInstance");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ThreadingId",
                table: "ChatMessages",
                column: "ThreadingId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_TimeSent",
                table: "ChatMessages",
                column: "TimeSent");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatMessages");
        }
    }
}
