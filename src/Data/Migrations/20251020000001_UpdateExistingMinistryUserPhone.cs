using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MT.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateExistingMinistryUserPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update the existing Ministry user's phone number for testing
            migrationBuilder.Sql(@"
                -- Update the existing Ministry user with test phone number
                UPDATE AspNetUsers 
                SET PhoneNumber = '+97451270700', PhoneNumberConfirmed = 1 
                WHERE Email = 'mtologin@mto.qa';

                -- Also update super admin phone to be confirmed
                UPDATE AspNetUsers 
                SET PhoneNumberConfirmed = 1 
                WHERE Email = 'info@lumen-path.com';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore original phone numbers
            migrationBuilder.Sql(@"
                UPDATE AspNetUsers 
                SET PhoneNumber = '97455225519', PhoneNumberConfirmed = 0 
                WHERE Email = 'mtologin@mto.qa';

                UPDATE AspNetUsers 
                SET PhoneNumberConfirmed = 0 
                WHERE Email = 'info@lumen-path.com';
            ");
        }
    }
}