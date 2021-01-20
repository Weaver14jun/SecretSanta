using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SmartAnalytics.SecretSanta.Services.Services.Base
{
    public abstract class BaseService<TService>
        where TService : class
    {
        protected readonly IConfiguration _configuration;
        protected readonly ILogger<TService> _logger;

        public BaseService(IConfiguration configuration, ILogger<TService> logger)
        {
            _logger = logger;
            _configuration = configuration;
        }

        protected int GetIntFromConfig(string sectionName)
        {
            return GetFromConfig(sectionName).Get<int>();
        }

        protected string GetStringFromConfig(string sectionName)
        {
            return GetFromConfig(sectionName).Get<string>();
        }

        protected virtual IConfigurationSection GetFromConfig(string sectionName)
        {
            IConfigurationSection section = _configuration.GetSection(sectionName);
            if (!section.Exists())
            {
                return null;
            }
            return section;
        }
    }
}
