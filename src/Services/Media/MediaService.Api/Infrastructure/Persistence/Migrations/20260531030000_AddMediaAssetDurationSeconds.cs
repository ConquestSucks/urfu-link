using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaService.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaAssetDurationSeconds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.AddColumn<int>(
                name: "duration_seconds",
                schema: "media",
                table: "media_assets",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropColumn(
                name: "duration_seconds",
                schema: "media",
                table: "media_assets");
        }
    }
}
