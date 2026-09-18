using System.Diagnostics;
using JevLauncher.Core;

var query = args.Length > 0 ? string.Join(' ', args) : "the pdf I just downloaded";
var key = Environment.GetEnvironmentVariable("TYPESAFE_API_KEY");

var index = LocalIndex.Build();
var candidates = Prefilter.BuildCandidates(query, index, 15);
var context = new LauncherContext(
    "probe",
    Array.Empty<string>(),
    "text",
    ContextProvider.TimeOfDay(DateTime.Now),
    DateTime.Now.DayOfWeek.ToString());

var client = new JevClient(new HttpClient(), key);

Console.WriteLine($"query      : {query}");
Console.WriteLine($"api key    : {(client.HasKey ? "set" : "missing")}");
Console.WriteLine($"index size : {index.Count}");
Console.WriteLine($"candidates : {candidates.Count}");
foreach (var c in candidates)
    Console.WriteLine($"  {c.Id,-6} {JevQuestions.KindName(c.Kind),-14} {c.Title}");

var sw = Stopwatch.StartNew();
var response = await client.QueryAsync(query, new Conversation(candidates, context));
sw.Stop();

Console.WriteLine($"\nlatency    : {sw.ElapsedMilliseconds} ms");

if (response is null)
{
    Console.WriteLine("no response (no key, error, or timeout)");
    return;
}

Console.WriteLine($"action     : {response.ActionChoice}");
Console.WriteLine($"ready      : {response.Ready:0.###}");
Console.WriteLine($"tokens     : {response.InputTokens} in / {response.OutputTokens} out");
Console.WriteLine("target distribution:");
foreach (var kv in response.TargetProbabilities.OrderByDescending(k => k.Value))
{
    var title = candidates.FirstOrDefault(c => c.Id == kv.Key)?.Title ?? kv.Key;
    Console.WriteLine($"  {kv.Key,-6} {kv.Value:0.###}  {title}");
}
