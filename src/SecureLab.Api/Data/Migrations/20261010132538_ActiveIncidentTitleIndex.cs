using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureLab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ActiveIncidentTitleIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_incidents_active_title",
                table: "incidents",
                column: "title",
                unique: true,
                filter: "status <> 'Closed'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_incidents_active_title",
                table: "incidents");
        }
    }
}
