using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartAnalytics.SecretSanta.Data.Core.Models;
using System;
using System.Threading.Tasks;

namespace SmartAnalytics.SecretSanta.Services.Services.Base
{
    public abstract class BaseSingletonService<TService> : BaseService<TService>
        where TService : class
    {
        protected readonly IServiceScopeFactory _scopeFactory;

        public BaseSingletonService(
            IConfiguration configuration,
            ILogger<TService> logger,
            IServiceScopeFactory scopeFactory)
            : base(configuration, logger)
        {
            _scopeFactory = scopeFactory;
            CheckServices();
        }

        public void UseApplicationContext(Action<ApplicationContext> action)
        {
            using var scope = _scopeFactory.CreateScope();
            ApplicationContext context = scope.ServiceProvider
                .GetRequiredService<ApplicationContext>();
            action(context);
        }

        protected abstract void CheckServices();

        public async Task UseApplicationContextAsync(Func<ApplicationContext, IServiceScope, Task> action)
        {
            using var scope = _scopeFactory.CreateScope();
            ApplicationContext context = scope.ServiceProvider
                .GetRequiredService<ApplicationContext>();
            await action(context, scope);
        }
    }
}
