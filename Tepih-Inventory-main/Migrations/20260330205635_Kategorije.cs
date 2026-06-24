using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventar.Migrations
{
    /// <inheritdoc />
    public partial class Kategorije : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF COL_LENGTH(N'dbo.Tepisi', N'BroaderCategory') IS NULL
                BEGIN
                    ALTER TABLE [Tepisi] ADD [BroaderCategory] nvarchar(100) NULL;
                END;

                IF COL_LENGTH(N'dbo.Tepisi', N'NarrowerCategory') IS NULL
                BEGIN
                    ALTER TABLE [Tepisi] ADD [NarrowerCategory] nvarchar(100) NULL;
                END;

                EXEC(N'
                UPDATE [Tepisi]
                SET [BroaderCategory] = N''default''
                WHERE [BroaderCategory] IS NULL OR LTRIM(RTRIM([BroaderCategory])) = N'''';
                ');

                EXEC(N'
                UPDATE [Tepisi]
                SET [NarrowerCategory] = N''default''
                WHERE [NarrowerCategory] IS NULL OR LTRIM(RTRIM([NarrowerCategory])) = N'''';
                ');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
