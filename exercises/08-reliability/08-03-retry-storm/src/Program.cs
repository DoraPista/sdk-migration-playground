using MigrationKit.Simulation;

var settings = new SimulationSettings();
var result = FleetSimulation.Run(new ClientRetryPolicy(), settings);

Console.WriteLine($"Fleet: {settings.Clients} clients, platform capacity {settings.CapacityPerSecond} req/s, " +
                  $"outage {settings.OutageStart.TotalSeconds:0}s–{settings.OutageEnd.TotalSeconds:0}s");
Console.WriteLine();
Console.WriteLine(" time   offered  accepted  rejected   (each # = 25 requests offered, | = capacity)");

var capacityColumn = settings.CapacityPerSecond / 25;
foreach (var second in result.Seconds.Where(s => s.Second % 5 == 0))
{
    var bar = new string('#', Math.Min(60, second.Offered / 25)).PadRight(Math.Max(capacityColumn, Math.Min(60, second.Offered / 25)));
    bar = bar.Length > capacityColumn ? bar[..capacityColumn] + "|" + bar[capacityColumn..] : bar + "|";
    var marker = second.Second >= settings.OutageStart.TotalSeconds && second.Second < settings.OutageEnd.TotalSeconds ? " (outage)" : "";
    Console.WriteLine($"{second.Second,4}s  {second.Offered,7}  {second.Accepted,8}  {second.Rejected,8}   {bar}{marker}");
}

Console.WriteLine();
Console.WriteLine($"Peak offered load         : {result.PeakOfferedPerSecond} req/s ({(double)result.PeakOfferedPerSecond / settings.CapacityPerSecond:0.0}x capacity)");
Console.WriteLine($"Recovery after the outage : {(double.IsInfinity(result.RecoverySeconds) ? "never (within the simulation)" : $"{result.RecoverySeconds:0} s")}");
Console.WriteLine($"Total requests            : {result.TotalRequests:N0}");
Console.WriteLine($"Files uploaded / failed   : {result.FilesUploaded:N0} / {result.FilesFailed:N0}");
Console.WriteLine($"Requests per uploaded file: {result.RequestsPerUploadedFile:0.00}");
