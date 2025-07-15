using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using WareHouseFileArchiver.Data;
using WareHouseFileArchiver.Models.Domains;
using WareHouseFileArchiver.Repositories;

namespace WareHouseFileArchiver.Tests
{
    [TestFixture]
    public class ItemRepositoryTests
    {
        private WareHouseDbContext dbContext = null!;
        private ItemRepository itemRepository = null!;

        [SetUp]
        public void SetUp()
        {
            var options = new DbContextOptionsBuilder<WareHouseDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            dbContext = new WareHouseDbContext(options);
            itemRepository = new ItemRepository(dbContext);
        }

        [TearDown]
        public void TearDown()
        {
            dbContext.Dispose();
        }

        [Test]
        public async Task CreateAsync_ShouldAddItem()
        {
            var item = new Item
            {
                Id = Guid.NewGuid(),
                Name = "Test Item",
                Description = "Test Description",
                Category = "General",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test@user.com"
            };

            await itemRepository.CreateAsync(item);

            var savedItem = await dbContext.Items.FindAsync(item.Id);

            Assert.That(savedItem, Is.Not.Null);
            Assert.That(savedItem?.Name, Is.EqualTo("Test Item"));
        }

        [Test]
        public async Task ExistsAsync_ShouldReturnTrue_IfItemExists()
        {
            var item = new Item
            {
                Id = Guid.NewGuid(),
                Name = "HR Document",
                Description = "Test",
                Category = "HRDocs",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "admin"
            };

            await dbContext.Items.AddAsync(item);
            await dbContext.SaveChangesAsync();

            var exists = await itemRepository.ExistsAsync("HR Document", "HRDocs");

            Assert.That(exists, Is.True);
        }

        [Test]
        public async Task ExistsAsync_ShouldReturnFalse_IfItemDoesNotExist()
        {
            var exists = await itemRepository.ExistsAsync("NonExisting", "UnknownCategory");

            Assert.That(exists, Is.False);
        }

        [Test]
        public async Task GetByIdAsync_ShouldReturnItem_WhenExists()
        {
            var item = new Item
            {
                Id = Guid.NewGuid(),
                Name = "Manual Doc",
                Description = "Test",
                Category = "Manuals",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "admin"
            };

            await dbContext.Items.AddAsync(item);
            await dbContext.SaveChangesAsync();

            var result = await itemRepository.GetByIdAsync(item.Id);

            Assert.That(result, Is.Not.Null);
            Assert.That(result?.Category, Is.EqualTo("Manuals"));
        }

        [Test]
        public async Task UpdateAsync_ShouldModifyItem()
        {
            var item = new Item
            {
                Id = Guid.NewGuid(),
                Name = "Old Name",
                Description = "Old Description",
                Category = "OldCategory",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "admin"
            };

            await dbContext.Items.AddAsync(item);
            await dbContext.SaveChangesAsync();

            item.Name = "Updated Name";
            item.Description = "Updated Description";
            item.Category = "NewCategory";
            item.UpdatedAt = DateTime.UtcNow;
            item.UpdatedBy = "updater";

            await itemRepository.UpdateAsync(item);

            var updated = await dbContext.Items.FindAsync(item.Id);

            Assert.That(updated?.Name, Is.EqualTo("Updated Name"));
            Assert.That(updated?.Category, Is.EqualTo("NewCategory"));
        }

        [Test]
        public async Task DeleteAsync_ShouldRemoveItem()
        {
            var item = new Item
            {
                Id = Guid.NewGuid(),
                Name = "To Delete",
                Description = "Delete Me",
                Category = "DeleteCategory",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "admin"
            };

            await dbContext.Items.AddAsync(item);
            await dbContext.SaveChangesAsync();

            await itemRepository.DeleteAsync(item);

            var deleted = await dbContext.Items.FindAsync(item.Id);

            Assert.That(deleted, Is.Null);
        }

        [Test]
        public async Task GetAllAsync_ShouldReturnPaginatedAndSortedItems()
        {
            var items = new List<Item>
            {
                new Item { Id = Guid.NewGuid(), Name = "A Item", Category = "Cat1", CreatedAt = DateTime.UtcNow, CreatedBy = "user" },
                new Item { Id = Guid.NewGuid(), Name = "C Item", Category = "Cat2", CreatedAt = DateTime.UtcNow.AddSeconds(1), CreatedBy = "user" },
                new Item { Id = Guid.NewGuid(), Name = "B Item", Category = "Cat3", CreatedAt = DateTime.UtcNow.AddSeconds(2), CreatedBy = "user" }
            };

            await dbContext.Items.AddRangeAsync(items);
            await dbContext.SaveChangesAsync();

            var result = await itemRepository.GetAllAsync("name", false, 1, 2);

            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result[0].Name, Is.EqualTo("A Item"));
            Assert.That(result[1].Name, Is.EqualTo("B Item"));
        }

        [Test]
        public async Task GetTotalCountAsync_ShouldReturnCorrectCount()
        {
            await dbContext.Items.AddRangeAsync(new[]
            {
                new Item { Id = Guid.NewGuid(), Name = "Item1", Category = "Cat1", CreatedAt = DateTime.UtcNow, CreatedBy = "admin" },
                new Item { Id = Guid.NewGuid(), Name = "Item2", Category = "Cat2", CreatedAt = DateTime.UtcNow, CreatedBy = "admin" }
            });

            await dbContext.SaveChangesAsync();

            var count = await itemRepository.GetTotalCountAsync();

            Assert.That(count, Is.EqualTo(2));
        }
    }
}
