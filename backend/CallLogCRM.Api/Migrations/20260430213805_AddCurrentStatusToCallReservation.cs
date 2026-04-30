using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CallLogCRM.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrentStatusToCallReservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrentStatus",
                table: "CallReservations",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentStatus",
                table: "CallReservations");
        }
    }
}
