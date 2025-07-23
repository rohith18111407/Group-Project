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
    public class ScheduledUploadTests
    {
        private Mock<IArchiveFileRepository> _mockRepository;
        private Mock<IWebHostEnvironment> _mockWebHostEnvironment;
        private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
        private Mock<UserManager<ApplicationUser>> _mockUserManager;
        private Mock<IHubContext<NotificationsHub>> _mockHubContext;
        private Mock<IFileManagementService> _mockFileManagementService;
        private FilesController _controller;
        private UploadArchiveFileRequestDto _scheduledUploadRequest;
        private UploadArchiveFileRequestDto _immediateUploadRequest;
        private Item _testItem;

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
            _testItem = new Item
            {
                Id = Guid.NewGuid(),
                Name = "Test Item",
                Category = "Test Category"
            };

            // Create mock file for upload with proper stream support
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.FileName).Returns("test-file.pdf");
            mockFile.Setup(f => f.Length).Returns(1024);
            mockFile.Setup(f => f.ContentType).Returns("application/pdf");
            
            // Setup CopyToAsync to simulate file copying
            mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns((Stream stream, CancellationToken token) =>
                {
                    // Write some test data to the stream
                    var testData = System.Text.Encoding.UTF8.GetBytes("Test file content");
                    return stream.WriteAsync(testData, 0, testData.Length, token);
                });

            _scheduledUploadRequest = new UploadArchiveFileRequestDto
            {
                File = mockFile.Object,
                ItemId = _testItem.Id,
                Description = "Test scheduled upload",
                Category = CategoryType.Documentation,
                ScheduledUploadDate = DateTime.UtcNow.AddHours(2) // Future date
            };

            _immediateUploadRequest = new UploadArchiveFileRequestDto
            {
                File = mockFile.Object,
                ItemId = _testItem.Id,
                Description = "Test immediate upload",
                Category = CategoryType.Documentation,
                ScheduledUploadDate = null // No scheduled date
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

        #region Scheduled Upload Tests

        [Test]
        public async Task Upload_WithFutureScheduledDate_CreatesScheduledUpload()
        {
            // Arrange
            var testUser = new ApplicationUser { Id = "test-user-id", UserName = "TestUser" };

            _mockRepository.Setup(r => r.ItemExistsAsync(_testItem.Id))
                .ReturnsAsync(true);
            _mockRepository.Setup(r => r.GetItemByIdAsync(_testItem.Id))
                .ReturnsAsync(_testItem);
            _mockRepository.Setup(r => r.GetLatestVersionAsync("test-file", _testItem.Id))
                .ReturnsAsync((ArchiveFile)null);
            _mockUserManager.Setup(um => um.FindByIdAsync("test-user-id"))
                .ReturnsAsync(testUser);
            SetupWebHostEnvironment();
            _mockRepository.Setup(r => r.UploadAsync(It.IsAny<ArchiveFile>()))
                .ReturnsAsync((ArchiveFile a) => a);

            SetupSignalRMocks();

            // Act
            var result = await _controller.Upload(_scheduledUploadRequest);

            // Debug: Check the actual result
            if (result is ObjectResult objResult && objResult.StatusCode != 200)
            {
                Console.WriteLine($"Upload failed with status: {objResult.StatusCode}");
                Console.WriteLine($"Result: {objResult.Value}");
            }

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            
            // Verify scheduled file was created
            _mockRepository.Verify(r => r.UploadAsync(It.Is<ArchiveFile>(a => 
                a.IsScheduled == true && 
                a.IsProcessed == false && 
                a.CreatedAt == _scheduledUploadRequest.ScheduledUploadDate.Value)), 
                Times.Once);
        }

        [Test]
        public async Task Upload_WithPastScheduledDate_CreatesImmediateUpload()
        {
            // Arrange
            SetupWebHostEnvironment();
            _scheduledUploadRequest.ScheduledUploadDate = DateTime.UtcNow.AddHours(-1); // Past date
            var testUser = new ApplicationUser { Id = "test-user-id", UserName = "TestUser" };

            _mockRepository.Setup(r => r.ItemExistsAsync(_testItem.Id))
                .ReturnsAsync(true);
            _mockRepository.Setup(r => r.GetItemByIdAsync(_testItem.Id))
                .ReturnsAsync(_testItem);
            _mockRepository.Setup(r => r.GetLatestVersionAsync("test-file", _testItem.Id))
                .ReturnsAsync((ArchiveFile)null);
            _mockUserManager.Setup(um => um.FindByIdAsync("test-user-id"))
                .ReturnsAsync(testUser);
            _mockRepository.Setup(r => r.UploadAsync(It.IsAny<ArchiveFile>()))
                .ReturnsAsync((ArchiveFile a) => a);

            SetupSignalRMocks();

            // Act
            var result = await _controller.Upload(_scheduledUploadRequest);

            // Assert
            Assert.That(result, Is.InstanceOf<OkResult>().Or.InstanceOf<OkObjectResult>());
            
            // Verify immediate upload was created (not scheduled)
            _mockRepository.Verify(r => r.UploadAsync(It.Is<ArchiveFile>(a => 
                a.IsScheduled == false && 
                a.CreatedAt <= DateTime.UtcNow)), 
                Times.Once);
        }

        [Test]
        public async Task Upload_WithNoScheduledDate_CreatesImmediateUpload()
        {
            // Arrange
            SetupWebHostEnvironment();
            var testUser = new ApplicationUser { Id = "test-user-id", UserName = "TestUser" };

            _mockRepository.Setup(r => r.ItemExistsAsync(_testItem.Id))
                .ReturnsAsync(true);
            _mockRepository.Setup(r => r.GetItemByIdAsync(_testItem.Id))
                .ReturnsAsync(_testItem);
            _mockRepository.Setup(r => r.GetLatestVersionAsync("test-file", _testItem.Id))
                .ReturnsAsync((ArchiveFile)null);
            _mockUserManager.Setup(um => um.FindByIdAsync("test-user-id"))
                .ReturnsAsync(testUser);
            _mockRepository.Setup(r => r.UploadAsync(It.IsAny<ArchiveFile>()))
                .ReturnsAsync((ArchiveFile a) => a);

            SetupSignalRMocks();

            // Act
            var result = await _controller.Upload(_immediateUploadRequest);

            // Assert
            Assert.That(result, Is.InstanceOf<OkResult>().Or.InstanceOf<OkObjectResult>());
            
            // Verify immediate upload was created
            _mockRepository.Verify(r => r.UploadAsync(It.Is<ArchiveFile>(a => 
                a.IsScheduled == false && 
                a.CreatedAt <= DateTime.UtcNow)), 
                Times.Once);
        }

        [Test]
        public async Task ScheduledUpload_StoresFileInTempDirectory()
        {
            // Arrange
            SetupWebHostEnvironment();
            var testUser = new ApplicationUser { Id = "test-user-id", UserName = "TestUser" };

            _mockRepository.Setup(r => r.ItemExistsAsync(_testItem.Id))
                .ReturnsAsync(true);
            _mockRepository.Setup(r => r.GetItemByIdAsync(_testItem.Id))
                .ReturnsAsync(_testItem);
            _mockRepository.Setup(r => r.GetLatestVersionAsync("test-file", _testItem.Id))
                .ReturnsAsync((ArchiveFile)null);
            _mockUserManager.Setup(um => um.FindByIdAsync("test-user-id"))
                .ReturnsAsync(testUser);
            _mockRepository.Setup(r => r.UploadAsync(It.IsAny<ArchiveFile>()))
                .ReturnsAsync((ArchiveFile a) => a);

            SetupSignalRMocks();

            // Act
            var result = await _controller.Upload(_scheduledUploadRequest);

            // Assert
            _mockRepository.Verify(r => r.UploadAsync(It.Is<ArchiveFile>(a => 
                a.FilePath.Contains("TempFiles"))), 
                Times.Once);
        }

        [Test]
        public async Task ScheduledUpload_WithExistingFile_CreatesNewVersion()
        {
            // Arrange
            SetupWebHostEnvironment();
            var existingFile = new ArchiveFile
            {
                Id = Guid.NewGuid(),
                FileName = "test-file",
                VersionNumber = 1,
                ItemId = _testItem.Id
            };

            var testUser = new ApplicationUser { Id = "test-user-id", UserName = "TestUser" };

            _mockRepository.Setup(r => r.ItemExistsAsync(_testItem.Id))
                .ReturnsAsync(true);
            _mockRepository.Setup(r => r.GetItemByIdAsync(_testItem.Id))
                .ReturnsAsync(_testItem);
            _mockRepository.Setup(r => r.GetLatestVersionAsync("test-file", _testItem.Id))
                .ReturnsAsync(existingFile);
            _mockUserManager.Setup(um => um.FindByIdAsync("test-user-id"))
                .ReturnsAsync(testUser);
            _mockRepository.Setup(r => r.UpdateAsync(existingFile))
                .Returns(Task.CompletedTask);
            _mockRepository.Setup(r => r.UploadAsync(It.IsAny<ArchiveFile>()))
                .ReturnsAsync((ArchiveFile a) => a);

            SetupSignalRMocks();

            // Act
            var result = await _controller.Upload(_scheduledUploadRequest);

            // Assert
            // Verify new version is created
            _mockRepository.Verify(r => r.UploadAsync(It.Is<ArchiveFile>(a => 
                a.VersionNumber == 2)), 
                Times.Once);
            
            // Verify existing file is updated
            _mockRepository.Verify(r => r.UpdateAsync(existingFile), Times.Once);
        }

        #endregion

        #region Get Scheduled Files Tests

        [Test]
        public async Task GetScheduledFiles_ReturnsAllScheduledFiles()
        {
            // Arrange
            var scheduledFiles = new List<ArchiveFile>
            {
                new ArchiveFile
                {
                    Id = Guid.NewGuid(),
                    FileName = "ScheduledFile1",
                    FileExtension = ".pdf",
                    IsScheduled = true,
                    IsProcessed = false,
                    CreatedAt = DateTime.UtcNow.AddHours(1),
                    Item = _testItem
                },
                new ArchiveFile
                {
                    Id = Guid.NewGuid(),
                    FileName = "ScheduledFile2",
                    FileExtension = ".docx",
                    IsScheduled = true,
                    IsProcessed = true,
                    CreatedAt = DateTime.UtcNow.AddHours(-1),
                    Item = _testItem
                }
            };

            _mockRepository.Setup(r => r.GetScheduledFilesAsync())
                .ReturnsAsync(scheduledFiles);

            // Act
            var result = await _controller.GetScheduledFiles();

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult.Value;
            
            var dataProperty = response.GetType().GetProperty("data");
            var files = dataProperty.GetValue(response) as IEnumerable<AllFilesResponseDto>;
            
            Assert.That(files, Is.Not.Null);
            Assert.That(files.Count(), Is.EqualTo(2));
            Assert.That(files.All(f => f.IsScheduled == true), Is.True);
        }

        [Test]
        public async Task GetScheduledFiles_ReturnsEmptyWhenNoScheduledFiles()
        {
            // Arrange
            _mockRepository.Setup(r => r.GetScheduledFilesAsync())
                .ReturnsAsync(new List<ArchiveFile>());

            // Act
            var result = await _controller.GetScheduledFiles();

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult.Value;
            
            var dataProperty = response.GetType().GetProperty("data");
            var files = dataProperty.GetValue(response) as IEnumerable<AllFilesResponseDto>;
            
            Assert.That(files, Is.Not.Null);
            Assert.That(files.Count(), Is.EqualTo(0));
        }

        #endregion

        #region Cancel Scheduled Upload Tests

        [Test]
        public async Task CancelScheduledUpload_WithValidUnprocessedFile_CancelsSuccessfully()
        {
            // Arrange
            var scheduledFileId = Guid.NewGuid();
            var scheduledFile = new ArchiveFile
            {
                Id = scheduledFileId,
                FileName = "ScheduledFile",
                FileExtension = ".pdf",
                FilePath = "/temp/scheduled_file.pdf",
                IsScheduled = true,
                IsProcessed = false
            };

            _mockRepository.Setup(r => r.GetScheduledFileByIdAsync(scheduledFileId))
                .ReturnsAsync(scheduledFile);
            _mockRepository.Setup(r => r.DeleteScheduledFileAsync(scheduledFileId))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.CancelScheduledUpload(scheduledFileId);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            
            _mockRepository.Verify(r => r.DeleteScheduledFileAsync(scheduledFileId), Times.Once);
        }

        [Test]
        public async Task CancelScheduledUpload_WithNonExistentFile_ReturnsNotFound()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid();

            _mockRepository.Setup(r => r.GetScheduledFileByIdAsync(nonExistentId))
                .ReturnsAsync((ArchiveFile)null);

            // Act
            var result = await _controller.CancelScheduledUpload(nonExistentId);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task CancelScheduledUpload_WithProcessedFile_ReturnsBadRequest()
        {
            // Arrange
            var processedFileId = Guid.NewGuid();
            var processedFile = new ArchiveFile
            {
                Id = processedFileId,
                FileName = "ProcessedFile",
                IsScheduled = true,
                IsProcessed = true
            };

            _mockRepository.Setup(r => r.GetScheduledFileByIdAsync(processedFileId))
                .ReturnsAsync(processedFile);

            // Act
            var result = await _controller.CancelScheduledUpload(processedFileId);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            
            // Verify deletion was not attempted
            _mockRepository.Verify(r => r.DeleteScheduledFileAsync(It.IsAny<Guid>()), Times.Never);
        }

        #endregion

        #region Validation Tests

        [Test]
        public async Task Upload_WithInvalidItemId_ReturnsBadRequest()
        {
            // Arrange
            var invalidItemId = Guid.NewGuid();
            _scheduledUploadRequest.ItemId = invalidItemId;

            _mockRepository.Setup(r => r.ItemExistsAsync(invalidItemId))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.Upload(_scheduledUploadRequest);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task Upload_SendsAppropriateSignalRNotification()
        {
            // Arrange
            SetupWebHostEnvironment();
            var testUser = new ApplicationUser { Id = "test-user-id", UserName = "TestUser" };

            _mockRepository.Setup(r => r.ItemExistsAsync(_testItem.Id))
                .ReturnsAsync(true);
            _mockRepository.Setup(r => r.GetItemByIdAsync(_testItem.Id))
                .ReturnsAsync(_testItem);
            _mockRepository.Setup(r => r.GetLatestVersionAsync("test-file", _testItem.Id))
                .ReturnsAsync((ArchiveFile)null);
            _mockUserManager.Setup(um => um.FindByIdAsync("test-user-id"))
                .ReturnsAsync(testUser);
            _mockRepository.Setup(r => r.UploadAsync(It.IsAny<ArchiveFile>()))
                .ReturnsAsync((ArchiveFile a) => a);

            SetupSignalRMocks();

            // Act - Scheduled Upload
            var result = await _controller.Upload(_scheduledUploadRequest);

            // Assert
            Assert.That(result, Is.InstanceOf<OkResult>().Or.InstanceOf<OkObjectResult>());
            
            // Verify SignalR notification was attempted
            _mockHubContext.Verify(h => h.Clients, Times.AtLeastOnce);
        }

        #endregion

        private void SetupSignalRMocks()
        {
            var mockClients = new Mock<IHubClients>();
            var mockClientProxy = new Mock<IClientProxy>();
            _mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);
            mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);
            
            // Setup the SendAsync method to return a completed task
            mockClientProxy.Setup(cp => cp.SendCoreAsync(
                It.IsAny<string>(), 
                It.IsAny<object[]>(), 
                It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        private void SetupWebHostEnvironment()
        {
            // Use a temp directory that we can write to
            var tempDir = Path.Combine(Path.GetTempPath(), "WarehouseFileArchiverTests");
            Directory.CreateDirectory(tempDir);
            _mockWebHostEnvironment.Setup(we => we.ContentRootPath)
                .Returns(tempDir);
        }
    }
}
