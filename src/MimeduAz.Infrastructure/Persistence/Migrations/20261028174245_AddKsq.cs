using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MimeduAz.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKsq : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ksq_tests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ClosedPoints = table.Column<string>(type: "jsonb", nullable: false),
                    OpenPoints = table.Column<string>(type: "jsonb", nullable: false),
                    KeyA = table.Column<string>(type: "jsonb", nullable: false),
                    KeyB = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ksq_tests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ksq_tests_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ksq_results",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TestId = table.Column<Guid>(type: "uuid", nullable: false),
                    TestName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Sinif = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Ad = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Variant = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    Kod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Answers = table.Column<string>(type: "jsonb", nullable: false),
                    OpenScores = table.Column<string>(type: "jsonb", nullable: false),
                    Score = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    MaxScore = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    WrongQuestions = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ksq_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ksq_results_ksq_tests_TestId",
                        column: x => x.TestId,
                        principalTable: "ksq_tests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ksq_results_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ksq_results_TestId",
                table: "ksq_results",
                column: "TestId");

            migrationBuilder.CreateIndex(
                name: "IX_ksq_results_UserId_TestId_Sinif",
                table: "ksq_results",
                columns: new[] { "UserId", "TestId", "Sinif" });

            migrationBuilder.CreateIndex(
                name: "IX_ksq_tests_UserId",
                table: "ksq_tests",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ksq_results");

            migrationBuilder.DropTable(
                name: "ksq_tests");
        }
    }
}
