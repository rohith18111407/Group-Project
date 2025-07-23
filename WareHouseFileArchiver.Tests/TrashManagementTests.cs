using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Moq;
using NUnit.Framework;
using System.Security.Claims;
using WareHouseFileArchiver.Controllers;
using WareHouseFileArchiver.Interfaces;
using WareHouseFileArchiver.Models.Domains;
using WareHouseFileArchiver.Models.DTOs;
using WareHouseFileArchiver.Services;
using WareHouseFileArchiver.SignalRHub;

namespace WareHouseFileArchiver.Tests
{
    [TestFixture]
    public class TrashManagementTests
    {
        private Mock<IArchiveFileRepository> _mockRepository;
        private Mock<IWebHostEnvironment> _mockWebHostEnvironment;
        private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
        private Mock<UserManager<ApplicationUser>> _mockUserManager;
        private Mock<IHubContext<NotificationsHub>> _mockHubContext;
        private Mock<IFileManagementService> _mockFileManagementService;
        private FilesController _controller;
        private List<ArchiveFile> _testTrashedFiles;
        private ArchiveFile _testActiveFile;

        [SetUp]
        public void Setup()
        {
            _mockRepository = new Mock<IArchiveFileRepository>();
            _mockWebHostEnvironment = new Mock<IWebHostEnvironment>();
            _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
            _mockUserManager = CreateMockUserManager();
            _mockHubContext = new Mock<IHubContext<NotificationsHub>>();
            _mockFileManagementService = new Mock<IFileManagementService>();

            _controller = new FilesController(
                _mockRepository.Object,
                _mockWebHostEnvironment.Object,
                _mockHttpContextAccessor.Object,
                _mockUserManager.Object,
                _mockHubContext.Object,
                _mockFileManagementService.Object
            );

            SetupTestData();
            SetupControllerUser();
        }

        private Mock<UserManager<ApplicationUser>> CreateMockUserManager()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            return new Mock<UserManager<ApplicationUser>>(store.Object, null, null, null, null, null, null, null, null);
        }

        private void SetupTestData()
        {
            _testActiveFile = new ArchiveFile
            {
                Id = Guid.NewGuid(),
                FileName = "ActiveFile",
                FileExtension = ".pdf",
                FileSizeInBytes = 1024,
                FilePath = "/path/to/active.pdf",
                IsDeleted = false,
                CreatedBy = "TestUser",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };

            _testTrashedFiles = new List<ArchiveFile>
            {
                new ArchiveFile
                {
                    Id = Guid.NewGuid(),
                    FileName = "RecentTrashedFile",
                    FileExtension = ".docx",
                    FileSizeInBytes = 2048,
                    FilePath = "/trash/recent.docx",
                    IsDeleted = true,
                    DeletedAt = DateTime.UtcNow.AddDays(-2), // 2 days ago - can be restored
                    DeletedBy = "TestUser",
                    CreatedBy = "TestUser",
                    Item = new Item { Name = "Test Item" }
                },
                new ArchiveFile
                {
                    Id = Guid.NewGuid(),
                    FileName = "ExpiredTrashedFile",
                    FileExtension = ".xlsx",
                    FileSizeInBytes = 4096,
                    FilePath = "/trash/expired.xlsx",
                    IsDeleted = true,
                    DeletedAt = DateTime.UtcNow.AddDays(-8), // 8 days ago - expired
                    DeletedBy = "TestUser",
                    CreatedBy = "TestUser",
                    Item = new Item { Name = "Test Item 2" }
                }
            };
        }

        private void SetupControllerUser()
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
                new Claim(ClaimTypes.Name, "TestUser"),
                new Claim(ClaimTypes.Role, "Admin")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);

            var context = new Mock<HttpContext>();
            context.Setup(x => x.User).Returns(principal);
            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = context.Object
            };
        }

        #region Delete/Move to Trash Tests

        [Test]
        public async Task Delete_WithValidFileId_MovesToTrashSuccessfully()
        {
            // Arrange
            var fileId = _testActiveFile.Id;
            var testUser = new ApplicationUser { Id = "test-user-id", UserName = "TestUser" };

            _mockRepository.Setup(r => r.GetFileByIdAsync(fileId))
                .ReturnsAsync(_testActiveFile);
            _mockUserManager.Setup(um => um.FindByIdAsync("test-user-id"))
                .ReturnsAsync(testUser);
            _mockFileManagementService.Setup(fs => fs.MoveFileToTrashAsync(_testActiveFile))
                .ReturnsAsync("/trash/moved_file.pdf");
            _mockRepository.Setup(r => r.MoveToTrashAsync(_testActiveFile, "TestUser", "/trash/moved_file.pdf"))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Delete(fileId);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            Assert.That(okResult.Value, Is.Not.Null);

            // Verify that the file was moved to trash
            _mockFileManagementService.Verify(fs => fs.MoveFileToTrashAsync(_testActiveFile), Times.Once);
            _mockRepository.Verify(r => r.MoveToTrashAsync(_testActiveFile, "TestUser", "/trash/moved_file.pdf"), Times.Once);
        }

        [Test]
        public async Task Delete_WithNonExistentFileId_ReturnsNotFound()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid();

            _mockRepository.Setup(r => r.GetFileByIdAsync(nonExistentId))
                .ReturnsAsync((ArchiveFile)null);

            // Act
            var result = await _controller.Delete(nonExistentId);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task Delete_WithAlreadyDeletedFile_ReturnsOk()
        {
            // Arrange
            var deletedFile = _testTrashedFiles[0];

            _mockRepository.Setup(r => r.GetFileByIdAsync(deletedFile.Id))
                .ReturnsAsync(deletedFile);
            _mockFileManagementService.Setup(fs => fs.MoveFileToTrashAsync(deletedFile))
                .ReturnsAsync("/trash/already_deleted_file.pdf");
            _mockRepository.Setup(r => r.MoveToTrashAsync(deletedFile, "TestUser", "/trash/already_deleted_file.pdf"))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Delete(deletedFile.Id);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            Assert.That(okResult.Value, Is.Not.Null);
        }

        #endregion

        #region Get Trashed Files Tests

        [Test]
        public async Task GetTrashedFiles_ReturnsAllTrashedFiles()
        {
            // Arrange
            _mockRepository.Setup(r => r.GetTrashedFilesAsync())
                .ReturnsAsync(_testTrashedFiles);

            // Act
            var result = await _controller.GetTrashedFiles();

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult.Value;
            
            // Use reflection to check the data property
            var dataProperty = response.GetType().GetProperty("data");
            var trashedFiles = dataProperty.GetValue(response) as IEnumerable<TrashFileResponseDto>;
            
            Assert.That(trashedFiles, Is.Not.Null);
            Assert.That(trashedFiles.Count(), Is.EqualTo(2));
        }

        [Test]
        public async Task GetTrashedFiles_CalculatesDaysRemainingCorrectly()
        {
            // Arrange
            _mockRepository.Setup(r => r.GetTrashedFilesAsync())
                .ReturnsAsync(_testTrashedFiles);

            // Act
            var result = await _controller.GetTrashedFiles();

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult.Value;
            
            var dataProperty = response.GetType().GetProperty("data");
            var trashedFiles = (dataProperty.GetValue(response) as IEnumerable<TrashFileResponseDto>).ToList();

            // Recent file (2 days ago) should have 5 days remaining and can be restored
            var recentFile = trashedFiles.First(f => f.FileName == "RecentTrashedFile");
            Assert.That(recentFile.DaysRemaining, Is.EqualTo(5));
            Assert.That(recentFile.CanRestore, Is.True);

            // Expired file (8 days ago) should have 0 days remaining and cannot be restored
            var expiredFile = trashedFiles.First(f => f.FileName == "ExpiredTrashedFile");
            Assert.That(expiredFile.DaysRemaining, Is.EqualTo(0));
            Assert.That(expiredFile.CanRestore, Is.False);
        }

        #endregion

        #region Restore From Trash Tests

        [Test]
        public async Task RestoreFromTrash_WithValidRecentFile_RestoresSuccessfully()
        {
            // Arrange
            var recentTrashedFile = _testTrashedFiles[0]; // 2 days old
            var fileId = recentTrashedFile.Id;

            _mockRepository.Setup(r => r.GetTrashedFilesAsync())
                .ReturnsAsync(_testTrashedFiles);
            _mockFileManagementService.Setup(fs => fs.RestoreFileFromTrashAsync(recentTrashedFile))
                .ReturnsAsync("/restored/path.docx");
            _mockRepository.Setup(r => r.RestoreFromTrashAsync(fileId, "/restored/path.docx"))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.RestoreFromTrash(fileId);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            
            _mockFileManagementService.Verify(fs => fs.RestoreFileFromTrashAsync(recentTrashedFile), Times.Once);
            _mockRepository.Verify(r => r.RestoreFromTrashAsync(fileId, "/restored/path.docx"), Times.Once);
        }

        [Test]
        public async Task RestoreFromTrash_WithExpiredFile_ReturnsBadRequest()
        {
            // Arrange
            var expiredTrashedFile = _testTrashedFiles[1]; // 8 days old
            var fileId = expiredTrashedFile.Id;

            _mockRepository.Setup(r => r.GetTrashedFilesAsync())
                .ReturnsAsync(_testTrashedFiles);

            // Act
            var result = await _controller.RestoreFromTrash(fileId);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            
            // Verify restoration was not attempted
            _mockFileManagementService.Verify(fs => fs.RestoreFileFromTrashAsync(It.IsAny<ArchiveFile>()), Times.Never);
            _mockRepository.Verify(r => r.RestoreFromTrashAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task RestoreFromTrash_WithNonExistentFile_ReturnsNotFound()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid();

            _mockRepository.Setup(r => r.GetTrashedFilesAsync())
                .ReturnsAsync(_testTrashedFiles);

            // Act
            var result = await _controller.RestoreFromTrash(nonExistentId);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        #endregion

        #region Permanent Delete Tests

        [Test]
        public async Task PermanentlyDeleteFromTrash_WithValidFile_DeletesSuccessfully()
        {
            // Arrange
            var trashedFile = _testTrashedFiles[0];
            var fileId = trashedFile.Id;

            _mockRepository.Setup(r => r.GetTrashedFilesAsync())
                .ReturnsAsync(_testTrashedFiles);
            _mockFileManagementService.Setup(fs => fs.DeletePhysicalFileFromTrashAsync(trashedFile))
                .Returns(Task.CompletedTask);
            _mockRepository.Setup(r => r.PermanentlyDeleteAsync(fileId))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.PermanentlyDeleteFromTrash(fileId);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            
            _mockFileManagementService.Verify(fs => fs.DeletePhysicalFileFromTrashAsync(trashedFile), Times.Once);
            _mockRepository.Verify(r => r.PermanentlyDeleteAsync(fileId), Times.Once);
        }

        [Test]
        public async Task PermanentlyDeleteFromTrash_WithNonExistentFile_ReturnsNotFound()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid();

            _mockRepository.Setup(r => r.GetTrashedFilesAsync())
                .ReturnsAsync(_testTrashedFiles);

            // Act
            var result = await _controller.PermanentlyDeleteFromTrash(nonExistentId);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        #endregion

        #region Trash Stats Tests

        [Test]
        public async Task GetTrashStats_ReturnsCorrectStatistics()
        {
            // Arrange
            _mockRepository.Setup(r => r.GetTrashedFilesAsync())
                .ReturnsAsync(_testTrashedFiles);

            // Act
            var result = await _controller.GetTrashStats();

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult.Value;
            
            var dataProperty = response.GetType().GetProperty("data");
            var stats = dataProperty.GetValue(response) as TrashStatsDto;
            
            Assert.That(stats, Is.Not.Null);
            Assert.That(stats.TotalTrashedFiles, Is.EqualTo(2));
            Assert.That(stats.TotalSizeInBytes, Is.EqualTo(6144)); // 2048 + 4096
            Assert.That(stats.FilesExpiringSoon, Is.EqualTo(0)); // None are between 6-7 days old
        }

        #endregion

        #region Cleanup Tests

        [Test]
        public async Task ForceCleanupTrash_DeletesExpiredFiles()
        {
            // Arrange
            var expiredFiles = new List<ArchiveFile> { _testTrashedFiles[1] }; // Only the 8-day old file

            _mockRepository.Setup(r => r.GetExpiredTrashedFilesAsync(7))
                .ReturnsAsync(expiredFiles);
            _mockFileManagementService.Setup(fs => fs.DeletePhysicalFileFromTrashAsync(It.IsAny<ArchiveFile>()))
                .Returns(Task.CompletedTask);
            _mockRepository.Setup(r => r.PermanentlyDeleteAsync(It.IsAny<Guid>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.ForceCleanupTrash();

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            
            _mockFileManagementService.Verify(fs => fs.DeletePhysicalFileFromTrashAsync(It.IsAny<ArchiveFile>()), Times.Once);
            _mockRepository.Verify(r => r.PermanentlyDeleteAsync(It.IsAny<Guid>()), Times.Once);
        }

        [Test]
        public async Task ForceCleanupTrash_WithNoExpiredFiles_ReturnsSuccess()
        {
            // Arrange
            _mockRepository.Setup(r => r.GetExpiredTrashedFilesAsync(7))
                .ReturnsAsync(new List<ArchiveFile>());

            // Act
            var result = await _controller.ForceCleanupTrash();

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult.Value;
            
            var dataProperty = response.GetType().GetProperty("data");
            var data = dataProperty.GetValue(response);
            var deletedCountProperty = data.GetType().GetProperty("DeletedCount");
            var deletedCount = (int)deletedCountProperty.GetValue(data);
            
            Assert.That(deletedCount, Is.EqualTo(0));
        }

        #endregion
    }
}
