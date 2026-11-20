namespace CityTransport.DTOs.Routes;

public class StopDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Order { get; set; }
    public long? OsmId { get; set; }
    public string? TransportType { get; set; }
}

public class CreateStopRequestDto
{
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public long? OsmId { get; set; }
    public string? TransportType { get; set; }
}

public class ImportStopItemDto
{
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public long? OsmId { get; set; }
    public string? TransportType { get; set; }
}

public class BatchImportStopsRequestDto
{
    public List<ImportStopItemDto> Stops { get; set; } = new();
}

public class BatchImportResultDto
{
    public int TotalReceived { get; set; }
    public int ImportedCount { get; set; }
    public int SkippedDuplicatesCount { get; set; }
    public List<StopDto> Stops { get; set; } = new();
}

public class StopRequestDto
{
    public Guid? StopId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Order { get; set; }
    public long? OsmId { get; set; }
    public string? TransportType { get; set; }
}
