using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SmartAnalytics.SecretSanta.Services.Controllers.Base
{
    [Route("[controller]")]
    [ApiController]
    public class BaseController<TController>: ControllerBase
    {
        protected readonly IConfiguration _configuration;
        protected readonly ILogger<TController> _logger;

        protected BaseController(IConfiguration configuration, ILogger<TController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }
    }
}
