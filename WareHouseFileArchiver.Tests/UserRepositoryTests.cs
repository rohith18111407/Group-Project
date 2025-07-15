using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using WareHouseFileArchiver.Data;
using WareHouseFileArchiver.Models.Domains;
using WareHouseFileArchiver.Models.DTOs;
using WareHouseFileArchiver.Repositories;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace WareHouseFileArchiver.Tests
{
    public class UserRepositoryTests
    {
        private WareHouseArchiveAuthDbContext _context;
        private UserRepository _userRepository;
        private UserManager<ApplicationUser> _userManager;

        private RoleManager<IdentityRole> _roleManager;

        [SetUp]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<WareHouseArchiveAuthDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _context = new WareHouseArchiveAuthDbContext(options);

            var roleStore = new RoleStore<IdentityRole>(_context);
            _roleManager = new RoleManager<IdentityRole>(
                roleStore,
                null,
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                null);

            // Seed roles
            var roles = new[] { "Admin", "Staff" };
            foreach (var role in roles)
            {
                if (!_roleManager.RoleExistsAsync(role).Result)
                {
                    _roleManager.CreateAsync(new IdentityRole(role)).Wait();
                }
            }

            var userStore = new UserStore<ApplicationUser>(_context);

            _userManager = new UserManager<ApplicationUser>(
                userStore,
                null,
                new PasswordHasher<ApplicationUser>(),
                new List<IUserValidator<ApplicationUser>>(),
                new List<IPasswordValidator<ApplicationUser>>(),
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                null,
                null);

            _userRepository = new UserRepository(_userManager);
        }


        [Test]
        public async Task CreateAsync_ShouldCreateUser_WhenValid()
        {
            var dto = new CreateUserDto
            {
                Username = "testuser@example.com",
                Password = "P@ssw0rd1",
                Roles = new List<string> { "Staff" }
            };

            var result = await _userRepository.CreateAsync(dto);

            Assert.That(result, Is.Not.Null);
            Assert.That(result?.Username, Is.EqualTo(dto.Username));
        }

        [Test]
        public async Task CreateAsync_ShouldReturnNull_WhenInvalidRole()
        {
            var dto = new CreateUserDto
            {
                Username = "baduser@example.com",
                Password = "P@ssw0rd1",
                Roles = new List<string> { "Hacker" }
            };

            var result = await _userRepository.CreateAsync(dto);

            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task GetByIdAsync_ShouldReturnUser_WhenExists()
        {
            var user = new ApplicationUser { UserName = "user1@example.com", Email = "user1@example.com" };
            await _userManager.CreateAsync(user, "P@ssw0rd1");
            await _userManager.AddToRoleAsync(user, "Admin");

            var result = await _userRepository.GetByIdAsync(user.Id);

            Assert.That(result, Is.Not.Null);
            Assert.That(result?.Username, Is.EqualTo(user.UserName));
        }

        [Test]
        public async Task GetByIdAsync_ShouldReturnNull_WhenNotFound()
        {
            var result = await _userRepository.GetByIdAsync(Guid.NewGuid().ToString());

            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task UpdateAsync_ShouldUpdateUser_WhenValid()
        {
            var user = new ApplicationUser { UserName = "old@example.com", Email = "old@example.com" };
            await _userManager.CreateAsync(user, "P@ssw0rd1");
            await _userManager.AddToRoleAsync(user, "Staff");

            var updateDto = new UpdateUserDto
            {
                Username = "new@example.com",
                Roles = new List<string> { "Admin" }
            };

            var result = await _userRepository.UpdateAsync(user.Id, updateDto);

            Assert.That(result, Is.Not.Null);
            Assert.That(result?.Username, Is.EqualTo("new@example.com"));
        }

        [Test]
        public async Task DeleteAsync_ShouldDeleteUser_WhenExists()
        {
            var user = new ApplicationUser { UserName = "delete@example.com", Email = "delete@example.com" };
            await _userManager.CreateAsync(user, "P@ssw0rd1");
            await _userManager.AddToRoleAsync(user, "Staff");

            var result = await _userRepository.DeleteAsync(user.Id);

            Assert.That(result, Is.Not.Null);
            Assert.That(result?.Username, Is.EqualTo(user.UserName));
        }

        [TearDown]
        public void TearDown()
        {
            _context.Dispose();
            (_userManager as IDisposable)?.Dispose();
            (_roleManager as IDisposable)?.Dispose();
        }
    }
}
