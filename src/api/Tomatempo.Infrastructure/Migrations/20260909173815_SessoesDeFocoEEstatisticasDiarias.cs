using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tomatempo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SessoesDeFocoEEstatisticasDiarias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "daily_stats",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    completed_pomodoros = table.Column<int>(type: "integer", nullable: false),
                    focus_minutes = table.Column<int>(type: "integer", nullable: false),
                    online_focus_minutes = table.Column<int>(type: "integer", nullable: false),
                    goal = table.Column<int>(type: "integer", nullable: false),
                    goal_met = table.Column<bool>(type: "boolean", nullable: false),
                    freeze_used = table.Column<bool>(type: "boolean", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_stats", x => new { x.user_id, x.local_date });
                    table.ForeignKey(
                        name: "FK_daily_stats_AspNetUsers_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "focus_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_heartbeat_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    counted_minutes = table.Column<int>(type: "integer", nullable: false),
                    ranking_eligible = table.Column<bool>(type: "boolean", nullable: false),
                    local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    client_session_id = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_focus_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_focus_sessions_AspNetUsers_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_focus_sessions_ativa_por_usuario",
                table: "focus_sessions",
                column: "user_id",
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_focus_sessions_started_at_utc",
                table: "focus_sessions",
                column: "started_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_focus_sessions_user_id_client_session_id",
                table: "focus_sessions",
                columns: new[] { "user_id", "client_session_id" },
                unique: true,
                filter: "client_session_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_focus_sessions_user_id_local_date",
                table: "focus_sessions",
                columns: new[] { "user_id", "local_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "daily_stats");

            migrationBuilder.DropTable(
                name: "focus_sessions");
        }
    }
}
