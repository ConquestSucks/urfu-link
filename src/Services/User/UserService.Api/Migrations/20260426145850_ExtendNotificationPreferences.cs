using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserService.Api.Migrations
{
    /// <inheritdoc />
    public partial class ExtendNotificationPreferences : Migration
    {
        private const string DefaultJson = """
        {
          "categories": {
            "1":  { "push": true, "email": true, "inApp": true },
            "2":  { "push": true, "email": true, "inApp": true },
            "3":  { "push": true, "email": true, "inApp": true },
            "10": { "push": true, "email": true, "inApp": true },
            "11": { "push": true, "email": true, "inApp": true },
            "20": { "push": true, "email": true, "inApp": true },
            "21": { "push": true, "email": true, "inApp": true },
            "22": { "push": true, "email": true, "inApp": true },
            "30": { "push": true, "email": true, "inApp": true },
            "31": { "push": true, "email": true, "inApp": true },
            "40": { "push": true, "email": true, "inApp": true },
            "41": { "push": true, "email": true, "inApp": true }
          },
          "quietHours": { "ianaTimezone": "Asia/Yekaterinburg", "start": null, "end": null, "enabled": false },
          "dndEnabled": false,
          "locale": "ru-RU",
          "sound": true
        }
        """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.AddColumn<string>(
                name: "notification_settings",
                schema: "users",
                table: "user_profiles",
                type: "jsonb",
                nullable: false,
                defaultValueSql: $"'{DefaultJson.Replace("'", "''", System.StringComparison.Ordinal)}'::jsonb");

            migrationBuilder.Sql("""
                UPDATE users.user_profiles
                SET notification_settings = jsonb_build_object(
                    'categories', jsonb_build_object(
                        '1',  jsonb_build_object('push', notify_new_messages, 'email', notify_new_messages, 'inApp', notify_new_messages),
                        '2',  jsonb_build_object('push', notify_mentions,    'email', notify_mentions,    'inApp', notify_mentions),
                        '3',  jsonb_build_object('push', notify_discipline_chats, 'email', notify_discipline_chats, 'inApp', notify_discipline_chats),
                        '10', jsonb_build_object('push', true, 'email', true, 'inApp', true),
                        '11', jsonb_build_object('push', true, 'email', true, 'inApp', true),
                        '20', jsonb_build_object('push', true, 'email', true, 'inApp', true),
                        '21', jsonb_build_object('push', true, 'email', true, 'inApp', true),
                        '22', jsonb_build_object('push', true, 'email', true, 'inApp', true),
                        '30', jsonb_build_object('push', true, 'email', true, 'inApp', true),
                        '31', jsonb_build_object('push', true, 'email', true, 'inApp', true),
                        '40', jsonb_build_object('push', true, 'email', true, 'inApp', true),
                        '41', jsonb_build_object('push', true, 'email', true, 'inApp', true)
                    ),
                    'quietHours', jsonb_build_object('ianaTimezone', 'Asia/Yekaterinburg', 'start', null, 'end', null, 'enabled', false),
                    'dndEnabled', false,
                    'locale', 'ru-RU',
                    'sound', notify_sound
                );
                """);

            migrationBuilder.DropColumn(
                name: "notify_discipline_chats",
                schema: "users",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "notify_mentions",
                schema: "users",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "notify_new_messages",
                schema: "users",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "notify_sound",
                schema: "users",
                table: "user_profiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.AddColumn<bool>(
                name: "notify_discipline_chats",
                schema: "users",
                table: "user_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "notify_mentions",
                schema: "users",
                table: "user_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "notify_new_messages",
                schema: "users",
                table: "user_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "notify_sound",
                schema: "users",
                table: "user_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql("""
                UPDATE users.user_profiles SET
                    notify_new_messages = COALESCE((notification_settings->'categories'->'1'->>'push')::boolean, true),
                    notify_mentions     = COALESCE((notification_settings->'categories'->'2'->>'push')::boolean, true),
                    notify_discipline_chats = COALESCE((notification_settings->'categories'->'3'->>'push')::boolean, true),
                    notify_sound        = COALESCE((notification_settings->>'sound')::boolean, true);
                """);

            migrationBuilder.DropColumn(
                name: "notification_settings",
                schema: "users",
                table: "user_profiles");
        }
    }
}
