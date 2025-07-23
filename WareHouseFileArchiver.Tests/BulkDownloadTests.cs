using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Moq;
using NUnit.Framework;
using System.IO.Compression;
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
    public class BulkDownloadTests
    {
        private Mock<IArchiveFileRepository> _mockRepository;
        private Mock<IWebHostEnvironment> _mockWebHostEnvironment;
        private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
        private Mock<UserManager<ApplicationUser>> _mockUserManager;
        private Mock<IHubContext<NotificationsHub>> _mockHubContext;
        private Mock<IFileManagementService> _mockFileManagementService;
        private FilesController _controller;
        private List<ArchiveFile> _testFiles;

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

            // Setup test files
            _testFiles = new List<ArchiveFile>
            {
                new ArchiveFile
                {
                    Id = Guid.NewGuid(),
                    FileName = "TestFile1",
                    FileExtension = ".pdf",
                    FileSizeInBytes = 1024,
                    FilePath = "/path/to/test1.pdf",
                    CreatedBy = "TestUser"
                },
                new ArchiveFile
                {
                    Id = Guid.NewGuid(),
                    FileName = "TestFile2",
                    FileExtension = ".docx",
                    FileSizeInBytes = 2048,
                    FilePath = "/path/to/test2.docx",
                    CreatedBy = "TestUser"
                }
            };

            SetupControllerUser();
            SetupSignalRMocks();
        }

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

        private Mock<UserManager<ApplicationUser>> CreateMockUserManager()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            return new Mock<UserManager<ApplicationUser>>(store.Object, null, null, null, null, null, null, null, null);
        }

        private void SetupControllerUser()
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
                new Claim(ClaimTypes.Name, "TestUser")
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

        [Test]
        public async Task BulkDownload_WithValidFileIds_ReturnsZipFile()
        {
            // Arrange
            var request = new BulkDownloadRequestDto
            {
                FileIds = _testFiles.Select(f => f.Id).ToList(),
                ZipFileName = "TestBulkDownload"
            };

            var testUser = new ApplicationUser { Id = "test-user-id", UserName = "TestUser" };

            _mockUserManager.Setup(um => um.FindByIdAsync("test-user-id"))
                .ReturnsAsync(testUser);

            _mockRepository.Setup(r => r.LogDownloadAsync(It.IsAny<FileDownloadLog>()))
                .Returns(Task.CompletedTask);

            // Mock file existence - this also sets up the repository mocks
            SetupFileSystemMocks();

            // Act
            var result = await _controller.BulkDownload(request);

            // Assert
            Assert.That(result, Is.InstanceOf<FileResult>());
            var fileResult = result as FileResult;
            Assert.That(fileResult.ContentType, Is.EqualTo("application/zip"));
            Assert.That(fileResult.FileDownloadName, Is.EqualTo("TestBulkDownload.zip"));
        }

        [Test]
        public async Task BulkDownload_WithEmptyFileIds_ReturnsBadRequest()
        {
            // Arrange
            var request = new BulkDownloadRequestDto
            {
                FileIds = new List<Guid>(),
                ZipFileName = "TestBulkDownload"
            };

            // Act
            var result = await _controller.BulkDownload(request);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            var badRequest = result as BadRequestObjectResult;
            Assert.That(badRequest.Value, Is.Not.Null);
        }

        [Test]
        public async Task BulkDownload_WithNonExistentFileIds_ReturnsNotFound()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid();
            var request = new BulkDownloadRequestDto
            {
                FileIds = new List<Guid> { nonExistentId },
                ZipFileName = "TestBulkDownload"
            };

            _mockRepository.Setup(r => r.GetFileByIdAsync(nonExistentId))
                .ReturnsAsync((ArchiveFile)null);

            // Act
            var result = await _controller.BulkDownload(request);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task BulkDownload_WithMixedValidAndInvalidIds_ReturnsNotFound()
        {
            // Arrange
            var validId = _testFiles[0].Id;
            var invalidId = Guid.NewGuid();
            var request = new BulkDownloadRequestDto
            {
                FileIds = new List<Guid> { validId, invalidId },
                ZipFileName = "TestBulkDownload"
            };

            _mockRepository.Setup(r => r.GetFileByIdAsync(validId))
                .ReturnsAsync(_testFiles[0]);
            _mockRepository.Setup(r => r.GetFileByIdAsync(invalidId))
                .ReturnsAsync((ArchiveFile)null);

            // Act
            var result = await _controller.BulkDownload(request);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task BulkDownload_WithDefaultZipFileName_GeneratesTimestampedName()
        {
            // Arrange
            var request = new BulkDownloadRequestDto
            {
                FileIds = new List<Guid> { _testFiles[0].Id },
                ZipFileName = null // No custom name
            };

            var testUser = new ApplicationUser { Id = "test-user-id", UserName = "TestUser" };

            _mockUserManager.Setup(um => um.FindByIdAsync("test-user-id"))
                .ReturnsAsync(testUser);
            _mockRepository.Setup(r => r.LogDownloadAsync(It.IsAny<FileDownloadLog>()))
                .Returns(Task.CompletedTask);

            SetupFileSystemMocks();

            // Act
            var result = await _controller.BulkDownload(request);

            // Assert
            Assert.That(result, Is.InstanceOf<FileResult>());
            var fileResult = result as FileResult;
            Assert.That(fileResult.FileDownloadName, Does.StartWith("ArchiveFiles_"));
            Assert.That(fileResult.FileDownloadName, Does.EndWith(".zip"));
        }

        [Test]
        public async Task BulkDownload_LogsDownloadForEachFile()
        {
            // Arrange
            var request = new BulkDownloadRequestDto
            {
                FileIds = _testFiles.Select(f => f.Id).ToList(),
                ZipFileName = "TestBulkDownload"
            };

            var testUser = new ApplicationUser { Id = "test-user-id", UserName = "TestUser" };

            _mockUserManager.Setup(um => um.FindByIdAsync("test-user-id"))
                .ReturnsAsync(testUser);

            SetupFileSystemMocks();

            // Act
            await _controller.BulkDownload(request);

            // Assert
            _mockRepository.Verify(r => r.LogDownloadAsync(It.IsAny<FileDownloadLog>()), 
                Times.Exactly(_testFiles.Count));
        }

        [Test]
        public async Task BulkDownload_SendsSignalRNotification()
        {
            // Arrange
            var request = new BulkDownloadRequestDto
            {
                FileIds = new List<Guid> { _testFiles[0].Id },
                ZipFileName = "TestBulkDownload"
            };

            var testUser = new ApplicationUser { Id = "test-user-id", UserName = "TestUser" };
            var mockClients = new Mock<IHubClients>();
            var mockClientProxy = new Mock<IClientProxy>();

            _mockRepository.Setup(r => r.GetFileByIdAsync(_testFiles[0].Id))
                .ReturnsAsync(_testFiles[0]);
            _mockUserManager.Setup(um => um.FindByIdAsync("test-user-id"))
                .ReturnsAsync(testUser);
            _mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);
            mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);

            SetupFileSystemMocks();

            // Act
            await _controller.BulkDownload(request);

            // Assert
            mockClientProxy.Verify(cp => cp.SendCoreAsync(
                "ReceiveNotification", 
                It.IsAny<object[]>(), 
                default(CancellationToken)), 
                Times.Once);
        }

        [Test]
        public async Task BulkDownload_HandlesFileSystemErrors_ReturnsInternalServerError()
        {
            // Arrange
            var request = new BulkDownloadRequestDto
            {
                FileIds = new List<Guid> { _testFiles[0].Id },
                ZipFileName = "TestBulkDownload"
            };

            var testUser = new ApplicationUser { Id = "test-user-id", UserName = "TestUser" };

            _mockRepository.Setup(r => r.GetFileByIdAsync(_testFiles[0].Id))
                .ThrowsAsync(new IOException("File access error"));
            _mockUserManager.Setup(um => um.FindByIdAsync("test-user-id"))
                .ReturnsAsync(testUser);

            // Act
            var result = await _controller.BulkDownload(request);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult.StatusCode, Is.EqualTo(500));
        }

        private void SetupFileSystemMocks()
        {
            // Create temporary test files and update the file paths
            for (int i = 0; i < _testFiles.Count; i++)
            {
                var tempPath = Path.GetTempFileName();
                File.WriteAllText(tempPath, $"Test content for {_testFiles[i].FileName}");
                _testFiles[i].FilePath = tempPath;
                
                // Update the repository mock to return the file with the correct path
                _mockRepository.Setup(r => r.GetFileByIdAsync(_testFiles[i].Id))
                    .ReturnsAsync(_testFiles[i]);
            }
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up temporary files
            foreach (var file in _testFiles)
            {
                if (File.Exists(file.FilePath))
                {
                    File.Delete(file.FilePath);
                }
            }
        }
    }
}
