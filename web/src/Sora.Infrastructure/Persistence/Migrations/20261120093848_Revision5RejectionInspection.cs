using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Revision5RejectionInspection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "inspected_at_utc",
                table: "dispatch_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "inspection_notes",
                table: "dispatch_orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "inspector_id",
                table: "dispatch_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_orders_inspector_id",
                table: "dispatch_orders",
                column: "inspector_id");

            migrationBuilder.AddForeignKey(
                name: "FK_dispatch_orders_staff_accounts_inspector_id",
                table: "dispatch_orders",
                column: "inspector_id",
                principalTable: "staff_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_dispatch_orders_staff_accounts_inspector_id",
                table: "dispatch_orders");

            migrationBuilder.DropIndex(
                name: "IX_dispatch_orders_inspector_id",
                table: "dispatch_orders");

            migrationBuilder.DropColumn(
                name: "inspected_at_utc",
                table: "dispatch_orders");

            migrationBuilder.DropColumn(
                name: "inspection_notes",
                table: "dispatch_orders");

            migrationBuilder.DropColumn(
                name: "inspector_id",
                table: "dispatch_orders");
        }
    }
}
