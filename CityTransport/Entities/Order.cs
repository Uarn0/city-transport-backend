namespace CityTransport.Entities;

public enum OrderStatus
{
    Pending,
    Paid,
    Cancelled,
    Expired
}

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid TariffId { get; set; }
    public Tariff Tariff { get; set; } = null!;

    public Guid? RouteId { get; set; }
    public Route? Route { get; set; }

    public int Quantity { get; set; } = 1;
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Ticket? Ticket { get; set; }
    public Transaction? Transaction { get; set; }
}
