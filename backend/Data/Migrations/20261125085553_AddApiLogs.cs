using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPlanner.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApiLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MethodName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TraceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    ElapsedMs = table.Column<double>(type: "float", nullable: false),
                    RequestJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiLogs", x => x.Id);
                    table.CheckConstraint("CK_ApiLogs_RequestJson", "ISJSON([RequestJson]) = 1");
                    table.CheckConstraint("CK_ApiLogs_ResponseJson", "ISJSON([ResponseJson]) = 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiLogs_MethodName_CreatedAt",
                table: "ApiLogs",
                columns: new[] { "MethodName", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiLogs");
        }
    }
}
