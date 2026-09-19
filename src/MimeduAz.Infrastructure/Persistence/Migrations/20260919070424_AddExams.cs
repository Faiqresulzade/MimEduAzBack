using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MimeduAz.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ExamAttemptId",
                table: "certificates",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "exams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Subject = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Grade = table.Column<int>(type: "integer", nullable: true),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    PassPercent = table.Column<int>(type: "integer", nullable: false, defaultValue: 60),
                    IsPaid = table.Column<bool>(type: "boolean", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_exams_users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exam_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ScorePercent = table.Column<int>(type: "integer", nullable: false),
                    CorrectCount = table.Column<int>(type: "integer", nullable: false),
                    QuestionCount = table.Column<int>(type: "integer", nullable: false),
                    Passed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_exam_attempts_exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_exam_attempts_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exam_sections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    Subject = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_sections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_exam_sections_exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exam_questions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    QuestionText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ImagePath = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Options = table.Column<List<string>>(type: "text[]", nullable: false),
                    CorrectOptionIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_exam_questions_exam_sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "exam_sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exam_answers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedIndex = table.Column<int>(type: "integer", nullable: true),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_answers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_exam_answers_exam_attempts_AttemptId",
                        column: x => x.AttemptId,
                        principalTable: "exam_attempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_exam_answers_exam_questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "exam_questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_certificates_ExamAttemptId",
                table: "certificates",
                column: "ExamAttemptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exam_answers_AttemptId_QuestionId",
                table: "exam_answers",
                columns: new[] { "AttemptId", "QuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exam_answers_QuestionId",
                table: "exam_answers",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_exam_attempts_ExamId",
                table: "exam_attempts",
                column: "ExamId");

            migrationBuilder.CreateIndex(
                name: "IX_exam_attempts_UserId_ExamId",
                table: "exam_attempts",
                columns: new[] { "UserId", "ExamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exam_questions_SectionId_OrderIndex",
                table: "exam_questions",
                columns: new[] { "SectionId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_exam_sections_ExamId_OrderIndex",
                table: "exam_sections",
                columns: new[] { "ExamId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_exams_AuthorId",
                table: "exams",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_exams_Status_Subject",
                table: "exams",
                columns: new[] { "Status", "Subject" });

            migrationBuilder.AddForeignKey(
                name: "FK_certificates_exam_attempts_ExamAttemptId",
                table: "certificates",
                column: "ExamAttemptId",
                principalTable: "exam_attempts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_certificates_exam_attempts_ExamAttemptId",
                table: "certificates");

            migrationBuilder.DropTable(
                name: "exam_answers");

            migrationBuilder.DropTable(
                name: "exam_attempts");

            migrationBuilder.DropTable(
                name: "exam_questions");

            migrationBuilder.DropTable(
                name: "exam_sections");

            migrationBuilder.DropTable(
                name: "exams");

            migrationBuilder.DropIndex(
                name: "IX_certificates_ExamAttemptId",
                table: "certificates");

            migrationBuilder.DropColumn(
                name: "ExamAttemptId",
                table: "certificates");
        }
    }
}
