namespace CityTransport.Entities;

public class RouteStop
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RouteId { get; set; }
    public Route Route { get; set; } = null!;

    public Guid StopId { get; set; }
    public Stop Stop { get; set; } = null!;

    public int Order { get; set; }
}
