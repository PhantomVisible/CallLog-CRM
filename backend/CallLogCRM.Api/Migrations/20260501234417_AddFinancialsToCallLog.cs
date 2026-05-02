using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CallLogCRM.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialsToCallLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AmountCollected",
                table: "CallLogs",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Revenue",
                table: "CallLogs",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmountCollected",
                table: "CallLogs");

            migrationBuilder.DropColumn(
                name: "Revenue",
                table: "CallLogs");
        }
    }
}
