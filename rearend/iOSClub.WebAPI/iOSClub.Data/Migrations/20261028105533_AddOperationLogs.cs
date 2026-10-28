using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace iOSClub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperationLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OperatorId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    OperatorName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OperatorRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ControllerName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ActionName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Target = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ChangeContent = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    HttpStatus = table.Column<int>(type: "integer", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    TraceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationLogs_CreatedAt",
                table: "OperationLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OperationLogs_Module_CreatedAt",
                table: "OperationLogs",
                columns: new[] { "Module", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationLogs_OperatorId_CreatedAt",
                table: "OperationLogs",
                columns: new[] { "OperatorId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationLogs_Success_CreatedAt",
                table: "OperationLogs",
                columns: new[] { "Success", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperationLogs");
        }
    }
}
