using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserService.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialUserProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.EnsureSchema(
                name: "users");

            migrationBuilder.CreateTable(
                name: "user_profiles",
                schema: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    avatar_url = table.Column<string>(type: "text", nullable: true),
                    about_me = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    show_online_status = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    show_last_visit_time = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    notify_new_messages = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    notify_sound = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    notify_discipline_chats = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    notify_mentions = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    playback_device_id = table.Column<string>(type: "text", nullable: true),
                    recording_device_id = table.Column<string>(type: "text", nullable: true),
                    webcam_device_id = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_profiles", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropTable(
                name: "user_profiles",
                schema: "users");
        }
    }
}
