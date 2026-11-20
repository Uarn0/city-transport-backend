namespace CityTransport.Entities;

public class Route
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Number { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;   // from where to where
    public int EstimatedDurationMinutes { get; set; }

    public ICollection<RouteStop> RouteStops { get; set; } = new List<RouteStop>();
    public ICollection<Tariff> Tariffs { get; set; } = new List<Tariff>();
}
