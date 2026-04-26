using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DisciplineService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrollmentConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "disciplines",
                table: "enrollments",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "disciplines",
                table: "enrollments");
        }
    }
}
