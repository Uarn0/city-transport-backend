namespace CityTransport.DTOs.Routes;

public class RouteWithStopsDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int EstimatedDurationMinutes { get; set; }
    public List<StopDto> Stops { get; set; } = new();
}

public class CreateRouteRequestDto
{
    public string Number { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int EstimatedDurationMinutes { get; set; }
    public List<StopRequestDto> Stops { get; set; } = new();
}

public class UpdateRouteStopsDto
{
    public int EstimatedDurationMinutes { get; set; }
    public List<StopRequestDto> Stops { get; set; } = new();
}

public class RouteSearchResultDto
{
    public Guid RouteId { get; set; }
    public string RouteNumber { get; set; } = string.Empty;
    public string RouteName { get; set; } = string.Empty;
    public StopDto? NearestFromStop { get; set; }
    public StopDto? NearestToStop { get; set; }
    public double DistanceMeters { get; set; }
}
