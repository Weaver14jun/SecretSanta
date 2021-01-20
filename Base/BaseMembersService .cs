using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartAnalytics.SecretSanta.Data.Core.Models;


namespace SmartAnalytics.SecretSanta.Services.Services.Base
{
    public abstract class BaseApplicationContextService<TService> : BaseService<TService>
        where TService : class
    {
        protected readonly ApplicationContext _context;

        public BaseApplicationContextService(
            IConfiguration configuration,
            ILogger<TService> logger,
            ApplicationContext context)
            : base(configuration, logger)
        {
            _context = context;
        }
    }
}
