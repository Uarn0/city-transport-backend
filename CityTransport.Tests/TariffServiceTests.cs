using CityTransport.Data;
using CityTransport.DTOs.Tariffs;
using CityTransport.Entities;
using CityTransport.Services;
using Microsoft.EntityFrameworkCore;



namespace CityTransport.Tests
{
    public class TariffServiceTests
    {
        [Fact]
        public async Task GetActiveTariffsAsync_ReturnsOnlyActiveTariffs()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite("DataSource=:memory:")
                .Options;

            using var context = new AppDbContext(options);
            await context.Database.OpenConnectionAsync();
            await context.Database.EnsureCreatedAsync();

            context.Tariffs.Add(new Tariff
            {
                Id = Guid.NewGuid(),
                Name = "Active Tariff",
                Price = 20.00m,
                IsActive = true
            });

            context.Tariffs.Add(new Tariff
            {
                Id = Guid.NewGuid(),
                Name = "Old Tariff",
                Price = 10.00m,
                IsActive = false
            });

            await context.SaveChangesAsync();

            var service = new TariffService(context);

            var result = await service.GetActiveTariffsAsync();

            var tariffsList = result.ToList();
            Assert.Equal(3, tariffsList.Count);
            Assert.Contains(tariffsList, t => t.Name == "Загальний разовий");
            Assert.Contains(tariffsList, t => t.Name == "Active Tariff");
        }
    }
}
