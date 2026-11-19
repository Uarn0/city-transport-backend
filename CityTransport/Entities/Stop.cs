namespace CityTransport.Entities;

public class Stop
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Order { get; set; }

    public Guid RouteId { get; set; }
    public Route Route { get; set; } = null!;
}
