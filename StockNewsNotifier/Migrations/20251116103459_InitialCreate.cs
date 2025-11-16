using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockNewsNotifier.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    BaseUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WatchItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Exchange = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Ticker = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CompanyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IconUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    AlertsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrawlStates",
                columns: table => new
                {
                    SourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    LastCrawlUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RequestsPerSecond = table.Column<double>(type: "REAL", nullable: false),
                    RequestsPerMinute = table.Column<int>(type: "INTEGER", nullable: false),
                    RobotsTxt = table.Column<string>(type: "TEXT", maxLength: 10000, nullable: true),
                    RobotsTxtFetchedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ConsecutiveErrors = table.Column<int>(type: "INTEGER", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    LastErrorUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrawlStates", x => x.SourceId);
                    table.ForeignKey(
                        name: "FK_CrawlStates_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NewsItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WatchItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CanonicalUrl = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    TitleHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SimHash64 = table.Column<long>(type: "INTEGER", nullable: false),
                    PublishedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FetchedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsRead = table.Column<bool>(type: "INTEGER", nullable: false),
                    NotificationSent = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NewsItems_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NewsItems_WatchItems_WatchItemId",
                        column: x => x.WatchItemId,
                        principalTable: "WatchItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WatchItemSources",
                columns: table => new
                {
                    WatchItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomQuery = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchItemSources", x => new { x.WatchItemId, x.SourceId });
                    table.ForeignKey(
                        name: "FK_WatchItemSources_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WatchItemSources_WatchItems_WatchItemId",
                        column: x => x.WatchItemId,
                        principalTable: "WatchItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrawlStates_LastCrawlUtc",
                table: "CrawlStates",
                column: "LastCrawlUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NewsItem_CanonicalUrl",
                table: "NewsItems",
                column: "CanonicalUrl",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NewsItem_WatchItemId_FetchedUtc",
                table: "NewsItems",
                columns: new[] { "WatchItemId", "FetchedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NewsItems_SourceId",
                table: "NewsItems",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsItems_TitleHash",
                table: "NewsItems",
                column: "TitleHash");

            migrationBuilder.CreateIndex(
                name: "IX_NewsItems_WatchItemId_IsRead_FetchedUtc",
                table: "NewsItems",
                columns: new[] { "WatchItemId", "IsRead", "FetchedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Sources_Name",
                table: "Sources",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchItem_Exchange_Ticker",
                table: "WatchItems",
                columns: new[] { "Exchange", "Ticker" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchItems_CreatedUtc",
                table: "WatchItems",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_WatchItemSources_SourceId",
                table: "WatchItemSources",
                column: "SourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CrawlStates");

            migrationBuilder.DropTable(
                name: "NewsItems");

            migrationBuilder.DropTable(
                name: "WatchItemSources");

            migrationBuilder.DropTable(
                name: "Sources");

            migrationBuilder.DropTable(
                name: "WatchItems");
        }
    }
}
