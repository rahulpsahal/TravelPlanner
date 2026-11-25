using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPlanner.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApiLogRequestId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RequestId",
                table: "ApiLogs",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.CreateIndex(
                name: "IX_ApiLogs_RequestId",
                table: "ApiLogs",
                column: "RequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApiLogs_RequestId",
                table: "ApiLogs");

            migrationBuilder.DropColumn(
                name: "RequestId",
                table: "ApiLogs");
        }
    }
}
