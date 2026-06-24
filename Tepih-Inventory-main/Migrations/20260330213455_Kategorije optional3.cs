using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventar.Migrations
{
    /// <inheritdoc />
    public partial class Kategorijeoptional3 : Migration
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
                """);

            migrationBuilder.AlterColumn<string>(
                name: "NarrowerCategory",
                table: "Tepisi",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BroaderCategory",
                table: "Tepisi",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "NarrowerCategory",
                table: "Tepisi",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "BroaderCategory",
                table: "Tepisi",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);
        }
    }
}
