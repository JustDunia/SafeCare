using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeCare.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiveEmailFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReceiveEmailNotifications",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceiveEmailNotifications",
                table: "AspNetUsers");
        }
    }
}
