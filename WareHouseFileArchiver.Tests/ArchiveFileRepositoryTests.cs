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
    [TestFixture]
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

        [TearDown]
        public void TearDown()
        {
            _context.Dispose();
        }

        #region UploadAsync Tests

        [Test]
        public async Task UploadAsync_ShouldAssignVersionAndSaveFile()
        {
            // Arrange
            var item = await CreateTestItem();
            var file = CreateTestArchiveFile(item.Id, "test.pdf");

            // Act
            var result = await _repository.UploadAsync(file);

            // Assert
            Assert.That(result.VersionNumber, Is.EqualTo(1));
            Assert.That(result.CreatedAt, Is.Not.EqualTo(default(DateTime)));
            Assert.That(result.Id, Is.Not.EqualTo(Guid.Empty));
        }

        [Test]
        public async Task UploadAsync_ShouldAutoIncrementVersion_WhenSameFileNameAndCategoryExists()
        {
            // Arrange
            var item = await CreateTestItem();
            var file1 = CreateTestArchiveFile(item.Id, "versioned.pdf");
            var file2 = CreateTestArchiveFile(item.Id, "versioned.pdf");

            // Act
            await _repository.UploadAsync(file1);
            var result2 = await _repository.UploadAsync(file2);

            // Assert
            Assert.That(result2.VersionNumber, Is.EqualTo(2));
        }

        [Test]
        public async Task UploadAsync_ShouldAllowSameFileNameWithDifferentCategory()
        {
            // Arrange
            var item = await CreateTestItem();
            var file1 = CreateTestArchiveFile(item.Id, "same.pdf", CategoryType.Blueprint);
            var file2 = CreateTestArchiveFile(item.Id, "same.pdf", CategoryType.Invoice);

            // Act
            var result1 = await _repository.UploadAsync(file1);
            var result2 = await _repository.UploadAsync(file2);

            // Assert
            Assert.That(result1.VersionNumber, Is.EqualTo(1));
            Assert.That(result2.VersionNumber, Is.EqualTo(1));
        }

        #endregion

        #region GetFilesByItemIdAsync Tests

        [Test]
        public async Task GetFilesByItemIdAsync_ShouldReturnFilesSortedByVersionDescending()
        {
            // Arrange
            var item = await CreateTestItem();
            var files = new List<ArchiveFile>
            {
                CreateTestArchiveFile(item.Id, "file.pdf", CategoryType.Blueprint, 1),
                CreateTestArchiveFile(item.Id, "file.pdf", CategoryType.Blueprint, 3),
                CreateTestArchiveFile(item.Id, "file.pdf", CategoryType.Blueprint, 2)
            };

            foreach (var file in files)
            {
                await _context.ArchiveFiles.AddAsync(file);
            }
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetFilesByItemIdAsync(item.Id);

            // Assert
            var resultList = result.ToList();
            Assert.That(resultList.Count, Is.EqualTo(3));
            Assert.That(resultList.First().VersionNumber, Is.EqualTo(3));
            Assert.That(resultList.Last().VersionNumber, Is.EqualTo(1));
        }

        [Test]
        public async Task GetFilesByItemIdAsync_ShouldExcludeArchivedFiles_WhenIncludeArchivedIsFalse()
        {
            // Arrange
            var item = await CreateTestItem();
            var activeFile = CreateTestArchiveFile(item.Id, "active.pdf");
            var archivedFile = CreateTestArchiveFile(item.Id, "archived.pdf");
            archivedFile.IsArchivedDueToInactivity = true;

            await _context.ArchiveFiles.AddRangeAsync(new[] { activeFile, archivedFile });
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetFilesByItemIdAsync(item.Id, includeArchived: false);

            // Assert
            var resultList = result.ToList();
            Assert.That(resultList.Count, Is.EqualTo(1));
            Assert.That(resultList.First().FileName, Is.EqualTo("active.pdf"));
        }

        [Test]
        public async Task GetFilesByItemIdAsync_ShouldIncludeArchivedFiles_WhenIncludeArchivedIsTrue()
        {
            // Arrange
            var item = await CreateTestItem();
            var activeFile = CreateTestArchiveFile(item.Id, "active.pdf");
            var archivedFile = CreateTestArchiveFile(item.Id, "archived.pdf");
            archivedFile.IsArchivedDueToInactivity = true;

            await _context.ArchiveFiles.AddRangeAsync(new[] { activeFile, archivedFile });
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetFilesByItemIdAsync(item.Id, includeArchived: true);

            // Assert
            Assert.That(result.Count(), Is.EqualTo(2));
        }

        #endregion

        #region GetFileByNameAndVersionAsync Tests

        [Test]
        public async Task GetFileByNameAndVersionAsync_ShouldReturnCorrectFile()
        {
            // Arrange
            var item = await CreateTestItem();
            var file = CreateTestArchiveFile(item.Id, "search.pdf", CategoryType.Blueprint, 5);
            await _context.ArchiveFiles.AddAsync(file);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetFileByNameAndVersionAsync("search.pdf", 5);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result?.FileName, Is.EqualTo("search.pdf"));
            Assert.That(result?.VersionNumber, Is.EqualTo(5));
        }

        [Test]
        public async Task GetFileByNameAndVersionAsync_ShouldReturnNull_WhenFileNotFound()
        {
            // Act
            var result = await _repository.GetFileByNameAndVersionAsync("nonexistent.pdf", 1);

            // Assert
            Assert.That(result, Is.Null);
        }

        #endregion

        #region GetLatestVersionAsync Tests

        [Test]
        public async Task GetLatestVersionAsync_ShouldReturnHighestVersion()
        {
            // Arrange
            var item = await CreateTestItem();
            var files = new[]
            {
                CreateTestArchiveFile(item.Id, "latest.pdf", CategoryType.Blueprint, 2),
                CreateTestArchiveFile(item.Id, "latest.pdf", CategoryType.Blueprint, 4),
                CreateTestArchiveFile(item.Id, "latest.pdf", CategoryType.Blueprint, 1)
            };

            await _context.ArchiveFiles.AddRangeAsync(files);
            await _context.SaveChangesAsync();

            // Act
            var latest = await _repository.GetLatestVersionAsync("latest.pdf", item.Id);

            // Assert
            Assert.That(latest, Is.Not.Null);
            Assert.That(latest?.VersionNumber, Is.EqualTo(4));
        }

        [Test]
        public async Task GetLatestVersionAsync_WithIncludeArchived_ShouldExcludeArchivedFiles_WhenIncludeArchivedIsFalse()
        {
            // Arrange
            var item = await CreateTestItem();
            var activeFile = CreateTestArchiveFile(item.Id, "test.pdf", CategoryType.Blueprint, 1);
            var archivedFile = CreateTestArchiveFile(item.Id, "test.pdf", CategoryType.Blueprint, 2);
            archivedFile.IsArchivedDueToInactivity = true;

            await _context.ArchiveFiles.AddRangeAsync(new[] { activeFile, archivedFile });
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetLatestVersionAsync("test.pdf", item.Id, includeArchived: false);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result?.VersionNumber, Is.EqualTo(1));
        }

        [Test]
        public async Task GetLatestVersionAsync_WithIncludeArchived_ShouldIncludeArchivedFiles_WhenIncludeArchivedIsTrue()
        {
            // Arrange
            var item = await CreateTestItem();
            var activeFile = CreateTestArchiveFile(item.Id, "test.pdf", CategoryType.Blueprint, 1);
            var archivedFile = CreateTestArchiveFile(item.Id, "test.pdf", CategoryType.Blueprint, 2);
            archivedFile.IsArchivedDueToInactivity = true;

            await _context.ArchiveFiles.AddRangeAsync(new[] { activeFile, archivedFile });
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetLatestVersionAsync("test.pdf", item.Id, includeArchived: true);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result?.VersionNumber, Is.EqualTo(2));
        }

        #endregion

        #region ItemExistsAsync Tests

        [Test]
        public async Task ItemExistsAsync_ShouldReturnTrue_WhenItemExists()
        {
            // Arrange
            var item = await CreateTestItem();

            // Act
            var exists = await _repository.ItemExistsAsync(item.Id);

            // Assert
            Assert.That(exists, Is.True);
        }

        [Test]
        public async Task ItemExistsAsync_ShouldReturnFalse_WhenItemDoesNotExist()
        {
            // Act
            var exists = await _repository.ItemExistsAsync(Guid.NewGuid());

            // Assert
            Assert.That(exists, Is.False);
        }

        #endregion

        #region UpdateAsync Tests

        [Test]
        public async Task UpdateAsync_ShouldModifyFileProperties()
        {
            // Arrange
            var item = await CreateTestItem();
            var file = CreateTestArchiveFile(item.Id, "update.pdf");
            file.FilePath = "/old/path.pdf";
            await _context.ArchiveFiles.AddAsync(file);
            await _context.SaveChangesAsync();

            // Act
            file.FilePath = "/new/path.pdf";
            file.UpdatedBy = "UpdatedUser";
            file.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(file);

            // Assert
            var updated = await _context.ArchiveFiles.FindAsync(file.Id);
            Assert.That(updated?.FilePath, Is.EqualTo("/new/path.pdf"));
            Assert.That(updated?.UpdatedBy, Is.EqualTo("UpdatedUser"));
        }

        #endregion

        #region GetItemByIdAsync Tests

        [Test]
        public async Task GetItemByIdAsync_ShouldReturnItem_WhenItemExists()
        {
            // Arrange
            var item = await CreateTestItem("TestItem");

            // Act
            var fetched = await _repository.GetItemByIdAsync(item.Id);

            // Assert
            Assert.That(fetched, Is.Not.Null);
            Assert.That(fetched?.Name, Is.EqualTo("TestItem"));
        }

        [Test]
        public async Task GetItemByIdAsync_ShouldReturnNull_WhenItemDoesNotExist()
        {
            // Act
            var fetched = await _repository.GetItemByIdAsync(Guid.NewGuid());

            // Assert
            Assert.That(fetched, Is.Null);
        }

        #endregion

        #region GetAllFilesAsync Tests

        [Test]
        public async Task GetAllFilesAsync_ShouldReturnAllFiles_OrderedByCreatedAtDescending()
        {
            // Arrange
            var item = await CreateTestItem();
            var file1 = CreateTestArchiveFile(item.Id, "file1.pdf");
            var file2 = CreateTestArchiveFile(item.Id, "file2.pdf");
            file1.CreatedAt = DateTime.UtcNow.AddDays(-1);
            file2.CreatedAt = DateTime.UtcNow;

            await _context.ArchiveFiles.AddRangeAsync(new[] { file1, file2 });
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetAllFilesAsync();

            // Assert
            var resultList = result.ToList();
            Assert.That(resultList.Count, Is.EqualTo(2));
            Assert.That(resultList.First().FileName, Is.EqualTo("file2.pdf"));
        }

        [Test]
        public async Task GetAllFilesAsync_ShouldExcludeArchivedFiles_WhenIncludeArchivedIsFalse()
        {
            // Arrange
            var item = await CreateTestItem();
            var activeFile = CreateTestArchiveFile(item.Id, "active.pdf");
            var archivedFile = CreateTestArchiveFile(item.Id, "archived.pdf");
            archivedFile.IsArchivedDueToInactivity = true;

            await _context.ArchiveFiles.AddRangeAsync(new[] { activeFile, archivedFile });
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetAllFilesAsync(includedArchived: false);

            // Assert
            var resultList = result.ToList();
            Assert.That(resultList.Count, Is.EqualTo(1));
            Assert.That(resultList.First().FileName, Is.EqualTo("active.pdf"));
        }

        #endregion

        #region GetFileByIdAsync Tests

        [Test]
        public async Task GetFileByIdAsync_ShouldReturnFile_WhenFileExists()
        {
            // Arrange
            var item = await CreateTestItem();
            var file = CreateTestArchiveFile(item.Id, "test.pdf");
            await _context.ArchiveFiles.AddAsync(file);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetFileByIdAsync(file.Id);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result?.Id, Is.EqualTo(file.Id));
        }

        [Test]
        public async Task GetFileByIdAsync_ShouldReturnNull_WhenFileDoesNotExist()
        {
            // Act
            var result = await _repository.GetFileByIdAsync(Guid.NewGuid());

            // Assert
            Assert.That(result, Is.Null);
        }

        #endregion

        #region DeleteAsync Tests

        [Test]
        public async Task DeleteAsync_ShouldRemoveFileAndAssociatedLogs()
        {
            // Arrange
            var item = await CreateTestItem();
            var file = CreateTestArchiveFile(item.Id, "delete.pdf");
            await _context.ArchiveFiles.AddAsync(file);
            await _context.SaveChangesAsync();

            var downloadLog = new FileDownloadLog
            {
                Id = Guid.NewGuid(),
                ArchiveFileId = file.Id,
                DownloadedBy = "TestUser",
                DownloadedAt = DateTime.UtcNow
            };
            await _context.FileDownloadLogs.AddAsync(downloadLog);
            await _context.SaveChangesAsync();

            // Act
            await _repository.DeleteAsync(file);

            // Assert
            var deletedFile = await _context.ArchiveFiles.FindAsync(file.Id);
            var deletedLog = await _context.FileDownloadLogs.FindAsync(downloadLog.Id);
            
            Assert.That(deletedFile, Is.Null);
            Assert.That(deletedLog, Is.Null);
        }

        #endregion

        #region LogDownloadAsync Tests

        [Test]
        public async Task LogDownloadAsync_ShouldSaveDownloadLog()
        {
            // Arrange
            var item = await CreateTestItem();
            var file = CreateTestArchiveFile(item.Id, "download.pdf");
            await _context.ArchiveFiles.AddAsync(file);
            await _context.SaveChangesAsync();

            var log = new FileDownloadLog
            {
                Id = Guid.NewGuid(),
                ArchiveFileId = file.Id,
                DownloadedBy = "TestUser",
                DownloadedAt = DateTime.UtcNow
            };

            // Act
            await _repository.LogDownloadAsync(log);

            // Assert
            var savedLog = await _context.FileDownloadLogs.FindAsync(log.Id);
            Assert.That(savedLog, Is.Not.Null);
            Assert.That(savedLog?.DownloadedBy, Is.EqualTo("TestUser"));
        }

        #endregion

        #region GetArchivedFilesAsync Tests

        [Test]
        public async Task GetArchivedFilesAsync_ShouldReturnOnlyArchivedFiles()
        {
            // Arrange
            var item = await CreateTestItem();
            var activeFile = CreateTestArchiveFile(item.Id, "active.pdf");
            var archivedFile = CreateTestArchiveFile(item.Id, "archived.pdf");
            archivedFile.IsArchivedDueToInactivity = true;
            archivedFile.ArchivedAt = DateTime.UtcNow;

            await _context.ArchiveFiles.AddRangeAsync(new[] { activeFile, archivedFile });
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetArchivedFilesAsync();

            // Assert
            var resultList = result.ToList();
            Assert.That(resultList.Count, Is.EqualTo(1));
            Assert.That(resultList.First().FileName, Is.EqualTo("archived.pdf"));
        }

        #endregion

        #region GetArchivedFilesByAdminAsync Tests

        [Test]
        public async Task GetArchivedFilesByAdminAsync_ShouldReturnArchivedFilesBySpecificAdmin()
        {
            // Arrange
            var item = await CreateTestItem();
            var file1 = CreateTestArchiveFile(item.Id, "file1.pdf", createdBy: "Admin1");
            file1.IsArchivedDueToInactivity = true;
            file1.ArchivedAt = DateTime.UtcNow;

            var file2 = CreateTestArchiveFile(item.Id, "file2.pdf", createdBy: "Admin2");
            file2.IsArchivedDueToInactivity = true;
            file2.ArchivedAt = DateTime.UtcNow;

            await _context.ArchiveFiles.AddRangeAsync(new[] { file1, file2 });
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetArchivedFilesByAdminAsync("Admin1");

            // Assert
            var resultList = result.ToList();
            Assert.That(resultList.Count, Is.EqualTo(1));
            Assert.That(resultList.First().CreatedBy, Is.EqualTo("Admin1"));
        }

        [Test]
        public async Task GetArchivedFilesByAdminAsync_ShouldReturnEmptyList_WhenAdminHasNoArchivedFiles()
        {
            // Arrange
            var item = await CreateTestItem();
            var file = CreateTestArchiveFile(item.Id, "file.pdf", createdBy: "Admin1");
            file.IsArchivedDueToInactivity = false; // Not archived

            await _context.ArchiveFiles.AddAsync(file);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetArchivedFilesByAdminAsync("Admin1");

            // Assert
            Assert.That(result.Count(), Is.EqualTo(0));
        }

        #endregion

        #region UnarchiveFileAsync Tests

        [Test]
        public async Task UnarchiveFileAsync_ShouldUnarchiveFile_WhenFileIsArchived()
        {
            // Arrange
            var item = await CreateTestItem();
            var file = CreateTestArchiveFile(item.Id, "archived.pdf");
            file.IsArchivedDueToInactivity = true;
            file.ArchivedAt = DateTime.UtcNow;
            file.ArchivedReason = "Inactivity";

            await _context.ArchiveFiles.AddAsync(file);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.UnarchiveFileAsync(file.Id, "UnarchiveUser");

            // Assert
            Assert.That(result, Is.True);
            
            var unarchived = await _context.ArchiveFiles.FindAsync(file.Id);
            Assert.That(unarchived?.IsArchivedDueToInactivity, Is.False);
            Assert.That(unarchived?.ArchivedAt, Is.Null);
            Assert.That(unarchived?.ArchivedReason, Is.Null);
            Assert.That(unarchived?.UpdatedBy, Is.EqualTo("UnarchiveUser"));
        }

        [Test]
        public async Task UnarchiveFileAsync_ShouldReturnFalse_WhenFileIsNotArchived()
        {
            // Arrange
            var item = await CreateTestItem();
            var file = CreateTestArchiveFile(item.Id, "active.pdf");
            await _context.ArchiveFiles.AddAsync(file);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.UnarchiveFileAsync(file.Id, "UnarchiveUser");

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task UnarchiveFileAsync_ShouldReturnFalse_WhenFileDoesNotExist()
        {
            // Act
            var result = await _repository.UnarchiveFileAsync(Guid.NewGuid(), "UnarchiveUser");

            // Assert
            Assert.That(result, Is.False);
        }

        #endregion

        #region ArchiveFileManuallyAsync Tests

        [Test]
        public async Task ArchiveFileManuallyAsync_ShouldArchiveFile_WhenFileIsActive()
        {
            // Arrange
            var item = await CreateTestItem();
            var file = CreateTestArchiveFile(item.Id, "active.pdf");
            await _context.ArchiveFiles.AddAsync(file);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.ArchiveFileManuallyAsync(file.Id, "ArchiveUser", "Manual archival");

            // Assert
            Assert.That(result, Is.True);
            
            var archived = await _context.ArchiveFiles.FindAsync(file.Id);
            Assert.That(archived?.IsArchivedDueToInactivity, Is.True);
            Assert.That(archived?.ArchivedAt, Is.Not.Null);
            Assert.That(archived?.ArchivedReason, Is.EqualTo("Manual archival"));
            Assert.That(archived?.UpdatedBy, Is.EqualTo("ArchiveUser"));
        }

        [Test]
        public async Task ArchiveFileManuallyAsync_ShouldReturnFalse_WhenFileIsAlreadyArchived()
        {
            // Arrange
            var item = await CreateTestItem();
            var file = CreateTestArchiveFile(item.Id, "archived.pdf");
            file.IsArchivedDueToInactivity = true;
            await _context.ArchiveFiles.AddAsync(file);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.ArchiveFileManuallyAsync(file.Id, "ArchiveUser", "Manual archival");

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task ArchiveFileManuallyAsync_ShouldReturnFalse_WhenFileDoesNotExist()
        {
            // Act
            var result = await _repository.ArchiveFileManuallyAsync(Guid.NewGuid(), "ArchiveUser", "Manual archival");

            // Assert
            Assert.That(result, Is.False);
        }

        #endregion

        #region GetArchivalStatsByAdminAsync Tests

        [Test]
        public async Task GetArchivalStatsByAdminAsync_ShouldReturnCorrectStatistics()
        {
            // Arrange
            var item = await CreateTestItem();
            
            var file1 = CreateTestArchiveFile(item.Id, "file1.pdf", createdBy: "Admin1");
            file1.IsArchivedDueToInactivity = true;

            var file2 = CreateTestArchiveFile(item.Id, "file2.pdf", createdBy: "Admin1");
            file2.IsArchivedDueToInactivity = true;

            var file3 = CreateTestArchiveFile(item.Id, "file3.pdf", createdBy: "Admin2");
            file3.IsArchivedDueToInactivity = true;

            var file4 = CreateTestArchiveFile(item.Id, "file4.pdf", createdBy: "Admin1");
            file4.IsArchivedDueToInactivity = false; // Not archived

            await _context.ArchiveFiles.AddRangeAsync(new[] { file1, file2, file3, file4 });
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetArchivalStatsByAdminAsync();

            // Assert
            Assert.That(result.ContainsKey("Admin1"), Is.True);
            Assert.That(result.ContainsKey("Admin2"), Is.True);
            Assert.That(result["Admin1"], Is.EqualTo(2));
            Assert.That(result["Admin2"], Is.EqualTo(1));
        }

        [Test]
        public async Task GetArchivalStatsByAdminAsync_ShouldHandleNullCreatedBy()
        {
            // Arrange
            var item = await CreateTestItem();
            var file = CreateTestArchiveFile(item.Id, "file.pdf");
            file.CreatedBy = null;
            file.IsArchivedDueToInactivity = true;

            await _context.ArchiveFiles.AddAsync(file);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetArchivalStatsByAdminAsync();

            // Assert
            Assert.That(result.ContainsKey("Unknown"), Is.True);
            Assert.That(result["Unknown"], Is.EqualTo(1));
        }

        #endregion

        #region Automatic Archival Tests

        [Test]
        public async Task ArchiveInactiveAdminFilesAsync_ShouldArchiveFilesOfSpecifiedAdmins()
        {
            // Arrange
            var item = await CreateTestItem();
            var admin1Files = new[]
            {
                CreateTestArchiveFile(item.Id, "admin1_file1.pdf", createdBy: "InactiveAdmin1"),
                CreateTestArchiveFile(item.Id, "admin1_file2.pdf", createdBy: "InactiveAdmin1")
            };

            var admin2File = CreateTestArchiveFile(item.Id, "admin2_file.pdf", createdBy: "ActiveAdmin2");
            var inactiveAdmin3File = CreateTestArchiveFile(item.Id, "admin3_file.pdf", createdBy: "InactiveAdmin3");

            await _context.ArchiveFiles.AddRangeAsync(admin1Files.Concat(new[] { admin2File, inactiveAdmin3File }));
            await _context.SaveChangesAsync();

            var inactiveAdmins = new List<string> { "InactiveAdmin1", "InactiveAdmin3" };

            // Act
            var archivedCount = await _repository.ArchiveInactiveAdminFilesAsync(inactiveAdmins, "Test archival");

            // Assert
            Assert.That(archivedCount, Is.EqualTo(3)); // 2 from Admin1 + 1 from Admin3

            var archivedFiles = await _context.ArchiveFiles
                .Where(f => f.IsArchivedDueToInactivity)
                .ToListAsync();

            Assert.That(archivedFiles.Count, Is.EqualTo(3));
            Assert.That(archivedFiles.All(f => f.ArchivedReason == "Test archival"), Is.True);
            Assert.That(archivedFiles.All(f => f.UpdatedBy == "System"), Is.True);

            // Verify active admin files are not archived
            var activeAdminFile = await _context.ArchiveFiles
                .FirstOrDefaultAsync(f => f.CreatedBy == "ActiveAdmin2");
            Assert.That(activeAdminFile?.IsArchivedDueToInactivity, Is.False);
        }

        [Test]
        public async Task ArchiveInactiveAdminFilesAsync_ShouldReturnZero_WhenNoInactiveAdminsProvided()
        {
            // Arrange
            var item = await CreateTestItem();
            var file = CreateTestArchiveFile(item.Id, "test.pdf");
            await _context.ArchiveFiles.AddAsync(file);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.ArchiveInactiveAdminFilesAsync(new List<string>());

            // Assert
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public async Task ArchiveInactiveAdminFilesAsync_ShouldReturnZero_WhenNoFilesFound()
        {
            // Act
            var result = await _repository.ArchiveInactiveAdminFilesAsync(new List<string> { "NonExistentAdmin" });

            // Assert
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public async Task ArchiveInactiveAdminFilesAsync_ShouldNotArchiveAlreadyArchivedFiles()
        {
            // Arrange
            var item = await CreateTestItem();
            var file1 = CreateTestArchiveFile(item.Id, "file1.pdf", createdBy: "InactiveAdmin");
            file1.IsArchivedDueToInactivity = false;

            var file2 = CreateTestArchiveFile(item.Id, "file2.pdf", createdBy: "InactiveAdmin");
            file2.IsArchivedDueToInactivity = true; // Already archived
            file2.ArchivedAt = DateTime.UtcNow.AddDays(-1);

            await _context.ArchiveFiles.AddRangeAsync(new[] { file1, file2 });
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.ArchiveInactiveAdminFilesAsync(new List<string> { "InactiveAdmin" });

            // Assert
            Assert.That(result, Is.EqualTo(1)); // Only one file should be archived
        }

        [Test]
        public async Task GetFilesForInactiveAdminsAsync_ShouldReturnCorrectFiles()
        {
            // Arrange
            var item = await CreateTestItem();
            var inactiveAdminFile = CreateTestArchiveFile(item.Id, "inactive_file.pdf", createdBy: "InactiveAdmin");
            var activeAdminFile = CreateTestArchiveFile(item.Id, "active_file.pdf", createdBy: "ActiveAdmin");
            var alreadyArchivedFile = CreateTestArchiveFile(item.Id, "archived_file.pdf", createdBy: "InactiveAdmin");
            alreadyArchivedFile.IsArchivedDueToInactivity = true;

            await _context.ArchiveFiles.AddRangeAsync(new[] { inactiveAdminFile, activeAdminFile, alreadyArchivedFile });
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetFilesForInactiveAdminsAsync(new List<string> { "InactiveAdmin" });

            // Assert
            var filesList = result.ToList();
            Assert.That(filesList.Count, Is.EqualTo(1));
            Assert.That(filesList.First().FileName, Is.EqualTo("inactive_file.pdf"));
        }

        [Test]
        public async Task ArchiveInactiveUsersFilesAsync_ShouldArchiveBasedOnFileActivity()
        {
            // Arrange
            var item = await CreateTestItem();
            
            var oldFile = CreateTestArchiveFile(item.Id, "old_file.pdf", createdBy: "OldUser");
            oldFile.CreatedAt = DateTime.UtcNow.AddDays(-35);
            oldFile.UpdatedAt = DateTime.UtcNow.AddDays(-35);

            var recentFile = CreateTestArchiveFile(item.Id, "recent_file.pdf", createdBy: "RecentUser");
            recentFile.CreatedAt = DateTime.UtcNow.AddDays(-5);
            recentFile.UpdatedAt = DateTime.UtcNow.AddDays(-5);

            var updatedOldFile = CreateTestArchiveFile(item.Id, "updated_old_file.pdf", createdBy: "UpdatedUser");
            updatedOldFile.CreatedAt = DateTime.UtcNow.AddDays(-50);
            updatedOldFile.UpdatedAt = DateTime.UtcNow.AddDays(-10); // Recently updated

            await _context.ArchiveFiles.AddRangeAsync(new[] { oldFile, recentFile, updatedOldFile });
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.ArchiveInactiveUsersFilesAsync(30);

            // Assert
            Assert.That(result, Is.EqualTo(1)); // Only the old file should be archived

            var archivedFile = await _context.ArchiveFiles
                .FirstOrDefaultAsync(f => f.Id == oldFile.Id);
            Assert.That(archivedFile?.IsArchivedDueToInactivity, Is.True);

            var recentFileCheck = await _context.ArchiveFiles
                .FirstOrDefaultAsync(f => f.Id == recentFile.Id);
            Assert.That(recentFileCheck?.IsArchivedDueToInactivity, Is.False);

            var updatedFileCheck = await _context.ArchiveFiles
                .FirstOrDefaultAsync(f => f.Id == updatedOldFile.Id);
            Assert.That(updatedFileCheck?.IsArchivedDueToInactivity, Is.False);
        }

        [Test]
        public async Task GetAllFilesAsync_ShouldExcludeAutomaticallyArchivedFiles()
        {
            // Arrange
            var item = await CreateTestItem();
            var activeFile = CreateTestArchiveFile(item.Id, "active.pdf", createdBy: "ActiveUser");

            var manuallyArchivedFile = CreateTestArchiveFile(item.Id, "manual.pdf", createdBy: "ManualUser");
            manuallyArchivedFile.IsArchivedDueToInactivity = true;
            manuallyArchivedFile.ArchivedAt = DateTime.UtcNow;
            manuallyArchivedFile.ArchivedReason = "Manual archival";

            var autoArchivedFile = CreateTestArchiveFile(item.Id, "auto.pdf", createdBy: "InactiveUser");

            await _context.ArchiveFiles.AddRangeAsync(new[] { activeFile, manuallyArchivedFile, autoArchivedFile });
            await _context.SaveChangesAsync();

            // Auto-archive using repository method - only archives files by "InactiveUser"
            await _repository.ArchiveInactiveAdminFilesAsync(new List<string> { "InactiveUser" });

            // Act
            var allFiles = await _repository.GetAllFilesAsync(includedArchived: false);

            // Assert
            var filesList = allFiles.ToList();
            Assert.That(filesList.Count, Is.EqualTo(1));
            Assert.That(filesList.First().FileName, Is.EqualTo("active.pdf"));
            Assert.That(filesList.First().CreatedBy, Is.EqualTo("ActiveUser"));
        }

        [Test]
        public async Task GetArchivedFilesAsync_ShouldIncludeAutomaticallyArchivedFiles()
        {
            // Arrange
            var item = await CreateTestItem();
            var file = CreateTestArchiveFile(item.Id, "auto_archived.pdf", createdBy: "InactiveAdmin");

            await _context.ArchiveFiles.AddAsync(file);
            await _context.SaveChangesAsync();

            // Auto-archive the file
            await _repository.ArchiveInactiveAdminFilesAsync(new List<string> { "InactiveAdmin" }, "Auto archival test");

            // Act
            var archivedFiles = await _repository.GetArchivedFilesAsync();

            // Assert
            var filesList = archivedFiles.ToList();
            Assert.That(filesList.Count, Is.EqualTo(1));
            Assert.That(filesList.First().ArchivedReason, Is.EqualTo("Auto archival test"));
        }

        #endregion

        #region Helper Methods

        private async Task<Item> CreateTestItem(string name = "TestItem")
        {
            var item = new Item
            {
                Id = Guid.NewGuid(),
                Name = name,
                Category = "Blueprint",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "TestUser"
            };

            await _context.Items.AddAsync(item);
            await _context.SaveChangesAsync();
            return item;
        }

        private ArchiveFile CreateTestArchiveFile(Guid itemId, string fileName, CategoryType category = CategoryType.Blueprint, int? version = null, string createdBy = "TestUser")
        {
            return new ArchiveFile
            {
                Id = Guid.NewGuid(),
                FileName = fileName,
                FileExtension = ".pdf",
                FilePath = $"/files/{fileName}",
                FileSizeInBytes = 1000,
                Category = category,
                ItemId = itemId,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow,
                VersionNumber = version ?? 1
            };
        }

        #endregion
    }
}