using Inventar.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventar.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260321154500_BackfillStorefrontCatalogData")]
    public partial class BackfillStorefrontCatalogData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE T
                SET T.OnlinePrice = T.Price
                FROM Tepisi AS T
                WHERE T.OnlinePrice IS NULL;

                ;WITH SlugSeed AS
                (
                    SELECT
                        T.Id,
                        BaseSlug = LOWER(
                            CONCAT_WS(
                                '-',
                                NULLIF(LTRIM(RTRIM(T.Name)), ''),
                                NULLIF(LTRIM(RTRIM(T.ProductNumber)), ''),
                                CASE
                                    WHEN T.Width IS NOT NULL AND T.Length IS NOT NULL THEN CONCAT(T.Width, 'x', T.Length)
                                    WHEN T.Width IS NOT NULL THEN CONVERT(nvarchar(20), T.Width)
                                    WHEN T.Length IS NOT NULL THEN CONVERT(nvarchar(20), T.Length)
                                    ELSE NULL
                                END,
                                NULLIF(LTRIM(RTRIM(T.Color)), ''),
                                CONVERT(nvarchar(20), T.Id)
                            )
                        )
                    FROM Tepisi AS T
                    WHERE T.Slug IS NULL OR LTRIM(RTRIM(T.Slug)) = ''
                ),
                SanitizedSlug AS
                (
                    SELECT
                        S.Id,
                        CleanSlug = LEFT(
                            REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                                TRANSLATE(
                                    REPLACE(
                                        REPLACE(
                                            REPLACE(
                                                REPLACE(
                                                    REPLACE(S.BaseSlug, NCHAR(273), 'dj'),
                                                    NCHAR(353), 's'),
                                                NCHAR(269), 'c'),
                                            NCHAR(263), 'c'),
                                        NCHAR(382), 'z'),
                                    N' /\,.:;()[]{}+_&|''"?!',
                                    N'---------------------'
                                ),
                                '--', '-'),
                                '--', '-'),
                                '--', '-'),
                                '--', '-'),
                                '--', '-'),
                            160
                        )
                    FROM SlugSeed AS S
                )
                UPDATE T
                SET T.Slug = CASE
                    WHEN SS.CleanSlug IS NULL OR SS.CleanSlug = '' THEN CONCAT('tepih-', T.Id)
                    ELSE SS.CleanSlug
                END
                FROM Tepisi AS T
                INNER JOIN SanitizedSlug AS SS
                    ON SS.Id = T.Id
                WHERE T.Slug IS NULL OR LTRIM(RTRIM(T.Slug)) = '';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This is a data backfill migration. Reversing it would destroy
            // potentially curated storefront values, so Down intentionally does nothing.
        }
    }
}
