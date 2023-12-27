using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Net5.WebApi.Test.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        [HttpGet]
        public async Task<string> Test01() => await Task.FromResult("api/Test/Test01");

        [HttpPost]
        public async Task<string> Test02() => await Task.FromResult("api/Test/Test02");

        [HttpPut]
        public async Task<string> Test03() => await Task.FromResult("api/Test/Test03");

        [HttpDelete]
        public async Task<string> Test04() => await Task.FromResult("api/Test/Test04");
    }
}
