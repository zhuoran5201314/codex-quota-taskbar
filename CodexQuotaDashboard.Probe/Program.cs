using System.Text.Json;
using CodexQuotaDashboard;

var client = new CodexAppServerClient();
var value = await client.ReadRateLimitsAsync(CancellationToken.None);
Console.WriteLine(JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
return value.IsAvailable ? 0 : 1;
