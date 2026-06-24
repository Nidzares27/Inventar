using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventar.Migrations
{
    public partial class AddProductCategories : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BroaderCategory",
                table: "Tepisi",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NarrowerCategory",
                table: "Tepisi",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE [Tepisi]
                SET [BroaderCategory] = 'default'
                WHERE [BroaderCategory] IS NULL OR LTRIM(RTRIM([BroaderCategory])) = '';
            ");

            migrationBuilder.Sql(@"
                UPDATE [Tepisi]
                SET [NarrowerCategory] = 'default'
                WHERE [NarrowerCategory] IS NULL OR LTRIM(RTRIM([NarrowerCategory])) = '';
            ");

            migrationBuilder.AlterColumn<string>(
                name: "NarrowerCategory",
                table: "Tepisi",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
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
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BroaderCategory",
                table: "Tepisi");

            migrationBuilder.DropColumn(
                name: "NarrowerCategory",
                table: "Tepisi");
        }
    }
}
