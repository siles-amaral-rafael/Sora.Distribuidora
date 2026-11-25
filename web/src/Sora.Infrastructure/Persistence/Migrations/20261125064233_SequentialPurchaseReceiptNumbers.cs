using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SequentialPurchaseReceiptNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "purchase_receipt_number_seq");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSequence(
                name: "purchase_receipt_number_seq");
        }
    }
}
