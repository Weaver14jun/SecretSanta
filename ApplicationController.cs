using Microsoft.AspNetCore.Mvc;
using SmartAnalytics.SecretSanta.Services.Controllers.Base;
using SmartAnalytics.SecretSanta.Services.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using SmartAnalytics.SecretSanta.Services.ViewModels;

namespace SmartAnalytics.SecretSanta.Services.Controllers
{
    [Route("application")]
    [ApiController]
    public class ApplicationController : BaseController<ApplicationController>
    {
        private readonly ApplicationService _service;
        private readonly AuthenticationService _authenticationService;

        public ApplicationController(
            ApplicationService service,
            IConfiguration config,
            ILogger<ApplicationController> logger,
            AuthenticationService authenticationService)
            : base(config, logger)
        {
            _service = service;
            _authenticationService = authenticationService;
        }

        /// <summary>
        /// Метод для получения информации о приложении.
        /// </summary>
        /// <returns></returns>
        [Route("data")]
        [HttpGet]
        public async Task<ApplicationDataViewModel> GetApplicationData()
        {
            await _authenticationService.GetUser(HttpContext);
            return _service.GetApplicationData();
        }
    }
}
