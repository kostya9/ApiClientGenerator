# C# source generator 
Create API Client from controllers

## Example
Controller
```c#
public enum CupPreferredLiquid { Tea, Coffee }

public record Cup(int Id, string Name, CupPreferredLiquid PreferredLiquid);

public record CreateCupRequest(string Name, CupPreferredLiquid PreferredLiquid);

[Route("cups")]
[ApiController]
public class CupsController : ControllerBase
{
    private static readonly List<Cup> _cups = new();

    private static int _maxId = 1;

    [HttpGet]
    public ActionResult<Cup[]> GetAllCups()
    {
        return _cups.ToArray();
    }

    [HttpDelete("{id}")]
    public IActionResult DeleteCup(int id)
    {
        var cup = _cups.FirstOrDefault(c => c.Id == id);

        if (cup != null)
        {
            _cups.Remove(cup);
        }

        return NoContent();
    }

    [HttpPost]
    public ActionResult<Cup> CreateCup(CreateCupRequest request)
    {
        var nextId = _maxId++;

        var cup = new Cup(nextId, request.Name, request.PreferredLiquid);
        _cups.Add(cup);

        return cup;
    }
}
```

Generated API Client
```c#
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Json;

public class ApiClient
{
    private readonly HttpClient _client;

    public ApiClient(HttpClient client)
    {
        _client = client;
        Cups = new Cups(client);
    }

    public Cups Cups { get; }
}

public class Cups
{
    private readonly HttpClient _client;

    public Cups(HttpClient client)
    {
        _client = client;
    }

    public async Task<CupsApiSourceGenerator.Controllers.Cup[]> GetAllCups()
    {
        var result = await _client.GetAsync($"cups");
        return await result.Content.ReadFromJsonAsync<CupsApiSourceGenerator.Controllers.Cup[]>();
    }

    public async Task DeleteCup(int id)
    {
        await _client.DeleteAsync($"cups/{id}");
    }

    public async Task<CupsApiSourceGenerator.Controllers.Cup> CreateCup(CupsApiSourceGenerator.Controllers.CreateCupRequest request)
    {
        var result = await _client.PostAsJsonAsync($"cups", request);
        return await result.Content.ReadFromJsonAsync<CupsApiSourceGenerator.Controllers.Cup>();
    }
}

```
