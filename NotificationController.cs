using Microsoft.AspNetCore.Mvc;
using SmartAnalytics.SecretSanta.Services.Controllers.Base;
using SmartAnalytics.SecretSanta.Services.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Collections.Generic;
using SmartAnalytics.SecretSanta.Data.Core.Models;
using SmartAnalytics.SecretSanta.Services.ViewModels;

namespace SmartAnalytics.SecretSanta.Services.Controllers
{
    [Route("notifications")]
    [ApiController]
    public class NotificationsController : BaseController<NotificationsController>
    {
        private readonly NotificationService _service;
        private readonly AuthenticationService _authenticationService;

        public NotificationsController(
            NotificationService service, IConfiguration config, ILogger<NotificationsController> logger, AuthenticationService authenticationService)
            : base(config, logger)
        {
            _service = service;
            _authenticationService = authenticationService;
        }

        /// <summary>
        /// Метод для изменения информации о сообщении.
        /// </summary>
        /// <returns></returns>
        [Route("setShowed/{notificationId}")]
        [HttpGet]
        public async Task<ActionResult> SetNotificationViewed(int notificationId)
        {
            await _authenticationService.GetUser(HttpContext);
            await _service.SetNotificationViewed(notificationId);

            return Ok();
        }

        /// <summary>
        /// Метод для получения уведомлений.
        /// </summary>
        /// <returns></returns>
        [Route("list")]
        [HttpGet]
        public async Task<List<NotificationViewModel>> GetNotifications()
        {
            User user = await _authenticationService.GetUser(HttpContext);
            return await _service.GetNotifications(user.Id);
        }
    }
}
