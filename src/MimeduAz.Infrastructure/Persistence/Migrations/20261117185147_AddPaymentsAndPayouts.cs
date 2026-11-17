using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MimeduAz.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentsAndPayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "TermsAcceptedAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermsVersion",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalCode",
                table: "orders",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardMask",
                table: "orders",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntRef",
                table: "orders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidAt",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentNumber",
                table: "orders",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentResult",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rrn",
                table: "orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TermsAcceptedAt",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermsVersion",
                table: "orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "payment_logs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    SignatureValid = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payout_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Iban = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: false),
                    AccountHolder = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Fin = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AdminNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payout_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payout_requests_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_orders_IntRef",
                table: "orders",
                column: "IntRef",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_PaymentNumber",
                table: "orders",
                column: "PaymentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_Status_CreatedAt",
                table: "orders",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_logs_CreatedAt",
                table: "payment_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_payment_logs_OrderId",
                table: "payment_logs",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_payout_requests_UserId_Status",
                table: "payout_requests",
                columns: new[] { "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_logs");

            migrationBuilder.DropTable(
                name: "payout_requests");

            migrationBuilder.DropIndex(
                name: "IX_orders_IntRef",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_PaymentNumber",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_Status_CreatedAt",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "TermsAcceptedAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "TermsVersion",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ApprovalCode",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "CardMask",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "IntRef",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PaidAt",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PaymentNumber",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PaymentResult",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "Rrn",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "TermsAcceptedAt",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "TermsVersion",
                table: "orders");
        }
    }
}
