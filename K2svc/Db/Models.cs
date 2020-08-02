using Microsoft.EntityFrameworkCore.Metadata.Builders; // for EntityTypeBuilder

namespace K2svc.Db
{
    public class Account
    {
        public long Id { get; set; }
        public string AccountName { get; set; }
        public string Password { get; set; }

        internal static void OnModelCreating(EntityTypeBuilder<Account> builder)
        {
            builder.HasKey(e => e.Id); // primary key
            builder.Property(e => e.Id).ValueGeneratedOnAdd(); // auto increment
            builder.HasIndex(e => e.AccountName).IsUnique(); // unique index
        }
    }
}