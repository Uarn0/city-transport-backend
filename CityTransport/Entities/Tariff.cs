namespace CityTransport.Entities;

public class Tariff
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationMinutes { get; set; } 
    public bool IsActive { get; set; } = true;

    // if any route is associated with this tariff, it will be stored here, 
    // otherwise it will be null, meaning the tariff is valid for all routes
    public ICollection<Route> Routes { get; set; } = new List<Route>();
}
