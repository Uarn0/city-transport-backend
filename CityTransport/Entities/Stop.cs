namespace CityTransport.Entities;

public class Stop
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public long? OsmId { get; set; }
    public string? TransportType { get; set; }

    public ICollection<RouteStop> RouteStops { get; set; } = new List<RouteStop>();
}
