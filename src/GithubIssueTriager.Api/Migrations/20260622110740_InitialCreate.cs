using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GithubIssueTriager.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    IssueSource = table.Column<string>(type: "text", nullable: false),
                    LocalJsonPath = table.Column<string>(type: "text", nullable: false),
                    GitHubOwner = table.Column<string>(type: "text", nullable: false),
                    GitHubRepo = table.Column<string>(type: "text", nullable: false),
                    GitHubIssueNumber = table.Column<int>(type: "integer", nullable: false),
                    GitHubToken = table.Column<string>(type: "text", nullable: false),
                    PriorityCritical = table.Column<int>(type: "integer", nullable: false),
                    PriorityHigh = table.Column<int>(type: "integer", nullable: false),
                    PriorityMedium = table.Column<int>(type: "integer", nullable: false),
                    LowConfidenceReviewThreshold = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "triage_history",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Repo = table.Column<string>(type: "text", nullable: false),
                    IssueNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    Priority = table.Column<string>(type: "text", nullable: false),
                    PriorityScore = table.Column<int>(type: "integer", nullable: false),
                    Labels = table.Column<string>(type: "text", nullable: false),
                    NextStep = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    TriagedAt = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_triage_history", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_settings");

            migrationBuilder.DropTable(
                name: "triage_history");
        }
    }
}
