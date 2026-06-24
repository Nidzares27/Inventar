using Inventar.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventar.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260609190000_BackfillLegacyProductionDefaults")]
    public partial class BackfillLegacyProductionDefaults : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE T
                SET T.OnlinePrice = T.Price
                FROM Tepisi AS T
                WHERE T.OnlinePrice IS NULL;

                UPDATE T
                SET T.BroaderCategory = N'Tepih'
                FROM Tepisi AS T
                WHERE T.BroaderCategory IS NULL
                    OR LTRIM(RTRIM(T.BroaderCategory)) = N''
                    OR T.BroaderCategory = N'default';

                UPDATE T
                SET T.NarrowerCategory = N'Tepih'
                FROM Tepisi AS T
                WHERE T.NarrowerCategory IS NULL
                    OR LTRIM(RTRIM(T.NarrowerCategory)) = N''
                    OR T.NarrowerCategory = N'default';

                UPDATE T
                SET T.ShortDescription = NULL
                FROM Tepisi AS T
                WHERE T.ShortDescription IS NOT NULL
                    AND LTRIM(RTRIM(T.ShortDescription)) = N'';

                UPDATE T
                SET T.SeoTitle = NULL
                FROM Tepisi AS T
                WHERE T.SeoTitle IS NOT NULL
                    AND LTRIM(RTRIM(T.SeoTitle)) = N'';

                UPDATE T
                SET T.SeoDescription = NULL
                FROM Tepisi AS T
                WHERE T.SeoDescription IS NOT NULL
                    AND LTRIM(RTRIM(T.SeoDescription)) = N'';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally left empty. This migration normalizes legacy production data
            // and rolling it back would destroy valid storefront values.
        }
    }
}
