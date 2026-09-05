using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tomatempo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CascataNaExclusaoDeConta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_account_tokens_user_id",
                table: "account_tokens",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_account_tokens_AspNetUsers_user_id",
                table: "account_tokens",
                column: "user_id",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_freeze_ledger_AspNetUsers_user_id",
                table: "freeze_ledger",
                column: "user_id",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_goals_AspNetUsers_user_id",
                table: "user_goals",
                column: "user_id",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_streaks_AspNetUsers_user_id",
                table: "user_streaks",
                column: "user_id",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_account_tokens_AspNetUsers_user_id",
                table: "account_tokens");

            migrationBuilder.DropForeignKey(
                name: "FK_freeze_ledger_AspNetUsers_user_id",
                table: "freeze_ledger");

            migrationBuilder.DropForeignKey(
                name: "FK_user_goals_AspNetUsers_user_id",
                table: "user_goals");

            migrationBuilder.DropForeignKey(
                name: "FK_user_streaks_AspNetUsers_user_id",
                table: "user_streaks");

            migrationBuilder.DropIndex(
                name: "IX_account_tokens_user_id",
                table: "account_tokens");
        }
    }
}
