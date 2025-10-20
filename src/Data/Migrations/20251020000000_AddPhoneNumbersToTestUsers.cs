using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MT.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhoneNumbersToTestUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add phone numbers to existing Owner and Ministry users for testing
            migrationBuilder.Sql(@"
                -- Add phone number +97451270700 to Owner and Ministry users for testing
                UPDATE AspNetUsers 
                SET PhoneNumber = '+97451270700', PhoneNumberConfirmed = 1 
                WHERE NormalizedEmail = 'OWNER@MT.LOCAL';

                UPDATE AspNetUsers 
                SET PhoneNumber = '+97451270700', PhoneNumberConfirmed = 1 
                WHERE NormalizedEmail = 'MINISTRY@MT.LOCAL';

                -- Also add it to the super admin for testing
                UPDATE AspNetUsers 
                SET PhoneNumber = '+97455170700', PhoneNumberConfirmed = 1 
                WHERE NormalizedEmail = 'INFO@LUMEN-PATH.COM';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove phone numbers from test users
            migrationBuilder.Sql(@"
                UPDATE AspNetUsers 
                SET PhoneNumber = NULL, PhoneNumberConfirmed = 0 
                WHERE NormalizedEmail IN ('OWNER@MT.LOCAL', 'MINISTRY@MT.LOCAL', 'INFO@LUMEN-PATH.COM');
            ");
        }
    }
}