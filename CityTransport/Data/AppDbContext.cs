using CityTransport.Entities;
using Microsoft.EntityFrameworkCore;
using Route = CityTransport.Entities.Route;

namespace CityTransport.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Tariff> Tariffs => Set<Tariff>();
    public DbSet<Route> Routes => Set<Route>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // configure many-to-many relationship between Tariff and Route
        modelBuilder.Entity<Tariff>()
            .HasMany(t => t.Routes)
            .WithMany(r => r.Tariffs);

        // configure relationships for Order
        modelBuilder.Entity<Order>()
            .HasOne(o => o.Route)
            .WithMany()
            .HasForeignKey(o => o.RouteId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Order>()
            .HasOne(o => o.Ticket)
            .WithOne(t => t.Order)
            .HasForeignKey<Ticket>(t => t.OrderId);

        modelBuilder.Entity<Order>()
            .HasOne(o => o.Transaction)
            .WithOne(tr => tr.Order)
            .HasForeignKey<Transaction>(tr => tr.OrderId);

        //seed Data
        var route1Id = Guid.Parse("33333333-3333-3333-3333-333333333333");
        modelBuilder.Entity<Route>().HasData(
            new Route { Id = route1Id, Number = "10", Name = "Центр - Вокзал" }
        );

        modelBuilder.Entity<Tariff>().HasData(
            new Tariff { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Загальний разовий", Price = 15.00m, DurationMinutes = 60 },
            new Tariff { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Студентський разовий", Price = 7.50m, DurationMinutes = 60 }
        );
    }
}
