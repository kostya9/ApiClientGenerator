using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;

namespace CupsApiSourceGenerator.Controllers
{
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
}
