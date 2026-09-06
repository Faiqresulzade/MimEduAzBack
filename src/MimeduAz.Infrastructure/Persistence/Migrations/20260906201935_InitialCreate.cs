using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MimeduAz.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "blog_posts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Tag = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ReadTime = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Excerpt = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    Body = table.Column<List<string>>(type: "text[]", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blog_posts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "request_logs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TraceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    QueryString = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    StatusCode = table.Column<int>(type: "integer", nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    RequestContentType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    RequestBody = table.Column<string>(type: "text", nullable: true),
                    ResponseBody = table.Column<string>(type: "text", nullable: true),
                    ExceptionType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_request_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "trainings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    DurationHours = table.Column<int>(type: "integer", nullable: false),
                    MetaLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SeatLimit = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trainings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Subject = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "role_claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_role_claims_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "training_syllabus_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainingId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_training_syllabus_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_training_syllabus_items_trainings_TrainingId",
                        column: x => x.TrainingId,
                        principalTable: "trainings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "carts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_carts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_carts_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "enrollments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainingId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgressPercent = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EnrolledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_enrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_enrollments_trainings_TrainingId",
                        column: x => x.TrainingId,
                        principalTable: "trainings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_enrollments_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_orders_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReplacedByToken = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Subject = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Grade = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Downloads = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsPaid = table.Column<bool>(type: "boolean", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OriginalFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_resources_users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_claims_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_logins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_logins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_user_logins_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_user_roles_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_roles_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_tokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_tokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_user_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cart_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CartId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cart_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cart_items_carts_CartId",
                        column: x => x.CartId,
                        principalTable: "carts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    AuthorPayoutAmount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_items_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quizzes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PassPercent = table.Column<int>(type: "integer", nullable: false, defaultValue: 70),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quizzes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quizzes_resources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quiz_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuizId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScorePercent = table.Column<int>(type: "integer", nullable: false),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    AttemptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quiz_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quiz_attempts_quizzes_QuizId",
                        column: x => x.QuizId,
                        principalTable: "quizzes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_quiz_attempts_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quiz_questions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuizId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    QuestionText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Options = table.Column<List<string>>(type: "text[]", nullable: false),
                    CorrectOptionIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quiz_questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quiz_questions_quizzes_QuizId",
                        column: x => x.QuizId,
                        principalTable: "quizzes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "certificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainingId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResourceQuizAttemptId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_certificates_quiz_attempts_ResourceQuizAttemptId",
                        column: x => x.ResourceQuizAttemptId,
                        principalTable: "quiz_attempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_certificates_trainings_TrainingId",
                        column: x => x.TrainingId,
                        principalTable: "trainings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_certificates_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cart_items_CartId_ItemType_ItemId",
                table: "cart_items",
                columns: new[] { "CartId", "ItemType", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_carts_UserId",
                table: "carts",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_certificates_Code",
                table: "certificates",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_certificates_ResourceQuizAttemptId",
                table: "certificates",
                column: "ResourceQuizAttemptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_certificates_TrainingId",
                table: "certificates",
                column: "TrainingId");

            migrationBuilder.CreateIndex(
                name: "IX_certificates_UserId",
                table: "certificates",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_enrollments_TrainingId",
                table: "enrollments",
                column: "TrainingId");

            migrationBuilder.CreateIndex(
                name: "IX_enrollments_UserId_TrainingId",
                table: "enrollments",
                columns: new[] { "UserId", "TrainingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_quiz_attempts_QuizId",
                table: "quiz_attempts",
                column: "QuizId");

            migrationBuilder.CreateIndex(
                name: "IX_quiz_attempts_UserId_QuizId",
                table: "quiz_attempts",
                columns: new[] { "UserId", "QuizId" });

            migrationBuilder.CreateIndex(
                name: "IX_quiz_questions_QuizId_OrderIndex",
                table: "quiz_questions",
                columns: new[] { "QuizId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_quizzes_ResourceId",
                table: "quizzes",
                column: "ResourceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_items_ItemType_ItemId",
                table: "order_items",
                columns: new[] { "ItemType", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_order_items_OrderId",
                table: "order_items",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_Code",
                table: "orders",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_CreatedAt",
                table: "orders",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_orders_UserId",
                table: "orders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_Token",
                table: "refresh_tokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_UserId",
                table: "refresh_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_request_logs_CreatedAt",
                table: "request_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_request_logs_Method_Path",
                table: "request_logs",
                columns: new[] { "Method", "Path" });

            migrationBuilder.CreateIndex(
                name: "IX_request_logs_StatusCode",
                table: "request_logs",
                column: "StatusCode");

            migrationBuilder.CreateIndex(
                name: "IX_request_logs_UserId",
                table: "request_logs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_resources_AuthorId",
                table: "resources",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_resources_Status_Subject_Grade",
                table: "resources",
                columns: new[] { "Status", "Subject", "Grade" });

            migrationBuilder.CreateIndex(
                name: "IX_role_claims_RoleId",
                table: "role_claims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "roles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_training_syllabus_items_TrainingId_OrderIndex",
                table: "training_syllabus_items",
                columns: new[] { "TrainingId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_user_claims_UserId",
                table: "user_claims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_logins_UserId",
                table: "user_logins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_RoleId",
                table: "user_roles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "users",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_users_FullName",
                table: "users",
                column: "FullName");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "users",
                column: "NormalizedUserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "blog_posts");

            migrationBuilder.DropTable(
                name: "cart_items");

            migrationBuilder.DropTable(
                name: "certificates");

            migrationBuilder.DropTable(
                name: "enrollments");

            migrationBuilder.DropTable(
                name: "quiz_questions");

            migrationBuilder.DropTable(
                name: "order_items");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "request_logs");

            migrationBuilder.DropTable(
                name: "role_claims");

            migrationBuilder.DropTable(
                name: "training_syllabus_items");

            migrationBuilder.DropTable(
                name: "user_claims");

            migrationBuilder.DropTable(
                name: "user_logins");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "user_tokens");

            migrationBuilder.DropTable(
                name: "carts");

            migrationBuilder.DropTable(
                name: "quiz_attempts");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropTable(
                name: "trainings");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "quizzes");

            migrationBuilder.DropTable(
                name: "resources");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
