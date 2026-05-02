using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CallLogCRM.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNotesToCallReservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "CallReservations",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notes",
                table: "CallReservations");
        }
    }
}
