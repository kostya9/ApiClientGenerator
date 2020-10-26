using Microsoft.AspNetCore.Mvc.Testing;
using System;
using System.Threading.Tasks;
using Xunit;

namespace CupsApiSourceGenerator.Tests
{
    public class CupApiTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> _factory;
        private readonly ApiClient _apiClient;

        public CupApiTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
            _apiClient = new ApiClient(_factory.CreateClient());
        }

        [Fact]
        public async Task CanGetCreatedCup()
        {
            var request = new Controllers.CreateCupRequest("MyLovelyCreated_ForGetCup", Controllers.CupPreferredLiquid.Coffee);
            await _apiClient.Cups.CreateCup(request);

            var allCups = await _apiClient.Cups.GetAllCups();

            var cup = Assert.Single(allCups, cup => cup.Name == "MyLovelyCreated_ForGetCup");
            Assert.Equal(Controllers.CupPreferredLiquid.Coffee, cup.PreferredLiquid);
        }

        [Fact]
        public async Task CanCreateCup()
        {
            var request = new Controllers.CreateCupRequest("MyLovelyCreatedCup", Controllers.CupPreferredLiquid.Tea);
            var cup = await _apiClient.Cups.CreateCup(request);

            Assert.Equal("MyLovelyCreatedCup", cup.Name);
            Assert.Equal(Controllers.CupPreferredLiquid.Tea, cup.PreferredLiquid);
        }

        [Fact]
        public async Task CanDeleteCup()
        {
            var request = new Controllers.CreateCupRequest("MyLovelyCreated_ForDeleteCup", Controllers.CupPreferredLiquid.Coffee);
            var cup = await _apiClient.Cups.CreateCup(request);

            await _apiClient.Cups.DeleteCup(cup.Id);

            var allCups = await _apiClient.Cups.GetAllCups();
            Assert.DoesNotContain(allCups, c => c.Name == "MyLovelyCreated_ForDeleteCup");
        }
    }
}
