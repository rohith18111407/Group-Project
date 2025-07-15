using Microsoft.EntityFrameworkCore;
using WareHouseFileArchiver.Models.Domains;

namespace WareHouseFileArchiver.Data
{
    public class WareHouseDbContext : DbContext
    {
        public WareHouseDbContext(DbContextOptions<WareHouseDbContext> options) : base(options)
        {
        }

        public DbSet<ArchiveFile> ArchiveFiles { get; set; }
        public DbSet<Item> Items { get; set; }
        public DbSet<FileDownloadLog> FileDownloadLogs { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ArchiveFile>()
                        .HasIndex(f => new { f.FileName, f.Category, f.VersionNumber })
                        .IsUnique();

            modelBuilder.Entity<Item>()
                .Property(i => i.Category)
                .HasConversion<string>();

            modelBuilder.Entity<ArchiveFile>()
                .Property(f => f.Category)
                .HasConversion<string>();

            // Cascade delete configuration
            modelBuilder.Entity<ArchiveFile>()
                        .HasOne(f => f.Item)
                        .WithMany(i => i.ArchiveFiles)
                        .HasForeignKey(f => f.ItemId)
                        .OnDelete(DeleteBehavior.Cascade);

            base.OnModelCreating(modelBuilder);
        }
    }
}