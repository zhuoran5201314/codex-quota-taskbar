using System.Text.Json;
using CodexQuotaDashboard;

var client = new CodexAppServerClient();
var first = await client.ReadRateLimitsAsync(CancellationToken.None);
var second = await client.ReadRateLimitsAsync(CancellationToken.None);
Console.WriteLine(JsonSerializer.Serialize(new { First = first, Second = second },
    new JsonSerializerOptions { WriteIndented = true }));
return first.IsAvailable && second.IsAvailable ? 0 : 1;
