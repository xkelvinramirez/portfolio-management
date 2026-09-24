using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Common.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakePortfolioNameUniquePerUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_portfolios_user_id",
                table: "Portfolios");

            migrationBuilder.CreateIndex(
                name: "ix_portfolios_user_id_name",
                table: "Portfolios",
                columns: new[] { "user_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_portfolios_user_id_name",
                table: "Portfolios");

            migrationBuilder.CreateIndex(
                name: "ix_portfolios_user_id",
                table: "Portfolios",
                column: "user_id");
        }
    }
}
