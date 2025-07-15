using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using WareHouseFileArchiver.Data;
using WareHouseFileArchiver.Models.Domains;
using WareHouseFileArchiver.Repositories;

namespace WareHouseFileArchiver.Tests
{
    public class ArchiveFileRepositoryTests
    {
        private WareHouseDbContext _context;
        private ArchiveFileRepository _repository;

        [SetUp]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<WareHouseDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new WareHouseDbContext(options);
            _repository = new ArchiveFileRepository(_context);
        }

        [Test]
        public async Task UploadAsync_ShouldAssignVersionAndSaveFile()
        {
            var item = new Item { Id = Guid.NewGuid(), Name = "Doc", Category = "Blueprint", CreatedAt = DateTime.UtcNow, CreatedBy = "test" };
            await _context.Items.AddAsync(item);
            await _context.SaveChangesAsync();

            var file = new ArchiveFile
            {
                Id = Guid.NewGuid(),
                FileName = "test.pdf",
                FileExtension = ".pdf",
                FilePath = "/files/test.pdf",
                FileSizeInBytes = 1000,
                Category = CategoryType.Blueprint,
                ItemId = item.Id,
                CreatedBy = "test"
            };

            var result = await _repository.UploadAsync(file);

            Assert.That(result.VersionNumber, Is.EqualTo(1));
            Assert.That(result.CreatedAt, Is.Not.EqualTo(default(DateTime)));
        }

        [Test]
        public async Task UploadAsync_ShouldAutoIncrementVersion()
        {
            var itemId = Guid.NewGuid();
            var item = new Item { Id = itemId, Name = "Doc", Category = "Blueprint", CreatedAt = DateTime.UtcNow, CreatedBy = "test" };
            await _context.Items.AddAsync(item);

            var file1 = new ArchiveFile
            {
                Id = Guid.NewGuid(),
                FileName = "versioned.pdf",
                FileExtension = ".pdf",
                FilePath = "/files/v1.pdf",
                FileSizeInBytes = 1000,
                Category = CategoryType.Blueprint,
                ItemId = itemId,
                CreatedBy = "test"
            };

            var file2 = new ArchiveFile
            {
                Id = Guid.NewGuid(),
                FileName = "versioned.pdf",
                FileExtension = ".pdf",
                FilePath = "/files/v2.pdf",
                FileSizeInBytes = 1000,
                Category = CategoryType.Blueprint,
                ItemId = itemId,
                CreatedBy = "test"
            };

            await _repository.UploadAsync(file1);
            var result2 = await _repository.UploadAsync(file2);

            Assert.That(result2.VersionNumber, Is.EqualTo(2));
        }

        [Test]
        public async Task GetFilesByItemIdAsync_ShouldReturnFilesSortedByVersion()
        {
            var itemId = Guid.NewGuid();
            await _context.Items.AddAsync(new Item { Id = itemId, Name = "Item", Category = "Blueprint", CreatedAt = DateTime.UtcNow, CreatedBy = "unit" });

            var files = new List<ArchiveFile>
            {
                new ArchiveFile { Id = Guid.NewGuid(), FileName = "file.pdf", FileExtension = ".pdf", FilePath = "a", FileSizeInBytes = 1, Category = CategoryType.Blueprint, ItemId = itemId, VersionNumber = 1, CreatedAt = DateTime.UtcNow },
                new ArchiveFile { Id = Guid.NewGuid(), FileName = "file.pdf", FileExtension = ".pdf", FilePath = "b", FileSizeInBytes = 1, Category = CategoryType.Blueprint, ItemId = itemId, VersionNumber = 3, CreatedAt = DateTime.UtcNow },
                new ArchiveFile { Id = Guid.NewGuid(), FileName = "file.pdf", FileExtension = ".pdf", FilePath = "c", FileSizeInBytes = 1, Category = CategoryType.Blueprint, ItemId = itemId, VersionNumber = 2, CreatedAt = DateTime.UtcNow }
            };

            await _context.ArchiveFiles.AddRangeAsync(files);
            await _context.SaveChangesAsync();

            var result = await _repository.GetFilesByItemIdAsync(itemId);
            Assert.That(result.First().VersionNumber, Is.EqualTo(3));
        }

        [Test]
        public async Task GetFileByNameAndVersionAsync_ShouldReturnCorrectFile()
        {
            var file = new ArchiveFile
            {
                Id = Guid.NewGuid(),
                FileName = "search.pdf",
                VersionNumber = 5,
                FileExtension = ".pdf",
                FilePath = "/files/s.pdf",
                FileSizeInBytes = 500,
                Category = CategoryType.Blueprint,
                CreatedAt = DateTime.UtcNow
            };

            await _context.ArchiveFiles.AddAsync(file);
            await _context.SaveChangesAsync();

            var result = await _repository.GetFileByNameAndVersionAsync("search.pdf", 5);
            Assert.That(result, Is.Not.Null);
            Assert.That(result?.FileName, Is.EqualTo("search.pdf"));
        }

        [Test]
        public async Task ItemExistsAsync_ShouldReturnTrueIfExists()
        {
            var id = Guid.NewGuid();
            await _context.Items.AddAsync(new Item { Id = id, Name = "Check", Category = "Invoice", CreatedAt = DateTime.UtcNow, CreatedBy = "t" });
            await _context.SaveChangesAsync();

            var exists = await _repository.ItemExistsAsync(id);
            Assert.That(exists, Is.True);
        }

        [Test]
        public async Task GetLatestVersionAsync_ShouldReturnHighestVersion()
        {
            var itemId = Guid.NewGuid();
            await _context.Items.AddAsync(new Item { Id = itemId, Name = "Doc", Category = "Blueprint", CreatedAt = DateTime.UtcNow, CreatedBy = "t" });

            var files = new[]
            {
                new ArchiveFile { Id = Guid.NewGuid(), FileName = "latest.pdf", VersionNumber = 2, Category = CategoryType.Blueprint, ItemId = itemId, FileExtension = ".pdf", FilePath = "x", FileSizeInBytes = 1, CreatedAt = DateTime.UtcNow },
                new ArchiveFile { Id = Guid.NewGuid(), FileName = "latest.pdf", VersionNumber = 4, Category = CategoryType.Blueprint, ItemId = itemId, FileExtension = ".pdf", FilePath = "y", FileSizeInBytes = 1, CreatedAt = DateTime.UtcNow }
            };

            await _context.ArchiveFiles.AddRangeAsync(files);
            await _context.SaveChangesAsync();

            var latest = await _repository.GetLatestVersionAsync("latest.pdf", itemId);
            Assert.That(latest?.VersionNumber, Is.EqualTo(4));
        }

        [Test]
        public async Task UpdateAsync_ShouldModifyFilePath()
        {
            var file = new ArchiveFile
            {
                Id = Guid.NewGuid(),
                FileName = "update.pdf",
                FileExtension = ".pdf",
                FilePath = "/old/path.pdf",
                FileSizeInBytes = 1000,
                VersionNumber = 1,
                Category = CategoryType.Blueprint,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test"
            };

            await _context.ArchiveFiles.AddAsync(file);
            await _context.SaveChangesAsync();

            file.FilePath = "/new/path.pdf";
            await _repository.UpdateAsync(file);

            var updated = await _context.ArchiveFiles.FindAsync(file.Id);
            Assert.That(updated?.FilePath, Is.EqualTo("/new/path.pdf"));
        }

        [Test]
        public async Task GetItemByIdAsync_ShouldReturnItem()
        {
            var itemId = Guid.NewGuid();
            var item = new Item { Id = itemId, Name = "Fetch", Category = "Blueprint", CreatedAt = DateTime.UtcNow, CreatedBy = "t" };

            await _context.Items.AddAsync(item);
            await _context.SaveChangesAsync();

            var fetched = await _repository.GetItemByIdAsync(itemId);
            Assert.That(fetched, Is.Not.Null);
            Assert.That(fetched?.Name, Is.EqualTo("Fetch"));
        }

        [TearDown]
        public void TearDown()
        {
            _context.Dispose();
        }
    }
}
