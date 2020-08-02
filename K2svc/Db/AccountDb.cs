using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Reflection;

namespace K2svc.Db
{
    public class AccountDb
        : DbContext // https://docs.microsoft.com/ko-kr/ef/core/miscellaneous/configuring-dbcontext
    {
        public DbSet<Account> Accounts { get; set; }

        public AccountDb(ServiceConfiguration _config)
        {
            config = _config;
            Database.EnsureCreated();

#if DEBUG
            // TEST environment
            if (Accounts.Count() == 0)
            {
                Accounts.AddRange(new Account[] {
                    new Account { AccountName = "k1", Password = "k1k1" },
                    new Account { AccountName = "k2", Password = "k2k2" },
                    new Account { AccountName = "k3", Password = "k3k3" },
                    new Account { AccountName = "k4", Password = "k4k4" },
                });
                SaveChanges();
            }
#endif
        }

        #region DbContext override
        protected override void OnConfiguring(DbContextOptionsBuilder builder)
        {
            // database provider 선택 ; 하드코딩없이 간편하게 
            builder.UseSqlite($"filename={config.DatabaseFileName}", options =>
            {
                options.MigrationsAssembly(Assembly.GetExecutingAssembly().FullName);
            });

            base.OnConfiguring(builder);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            // TODO: 하드코딩하지 않고 table 을 늘리거나 변경할 수 있도록(Attribute 이용?)
            Account.OnModelCreating(builder.Entity<Account>());

            base.OnModelCreating(builder);
        }
        #endregion

        private readonly ServiceConfiguration config;
    }

}