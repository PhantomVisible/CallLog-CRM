using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CallLogCRM.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNotesToCallLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "CallLogs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notes",
                table: "CallLogs");
        }
    }
}
