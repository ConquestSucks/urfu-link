using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UserService.Api.Infrastructure.Persistence;

#nullable disable

namespace UserService.Api.Migrations
{
    [DbContext(typeof(UserDbContext))]
    [Migration("20260531183000_RemoveShowOnlineStatusPrivacy")]
    public partial class RemoveShowOnlineStatusPrivacy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.Sql("""
                UPDATE users.user_profiles
                SET show_last_visit_time = TRUE;
            """);

            migrationBuilder.DropColumn(
                name: "show_online_status",
                schema: "users",
                table: "user_profiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.AddColumn<bool>(
                name: "show_online_status",
                schema: "users",
                table: "user_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }
    }
}
