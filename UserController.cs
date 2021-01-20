using Microsoft.AspNetCore.Mvc;
using System.Text;
using SmartAnalytics.SecretSanta.Services.Controllers.Base;
using SmartAnalytics.SecretSanta.Services.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Collections;
using System;
using SmartAnalytics.SecretSanta.Data.Core.Models;
using SmartAnalytics.SecretSanta.Services.Exceptions;
using SmartAnalytics.SecretSanta.Services.ViewModels;

namespace SmartAnalytics.SecretSanta.Services.Controllers
{
    [Route("user")]
    [ApiController]
    public class UserController : BaseController<UserController>
    {
        private readonly UserService _service;
        private readonly AuthenticationService _authenticationService;

        public UserController(
            UserService service, IConfiguration config, ILogger<UserController> logger, AuthenticationService authenticationService)
            : base(config, logger)
        {
            _service = service;
            _authenticationService = authenticationService;
        }

        /// <summary>
        /// Метод для получения информации о участнике.
        /// </summary>
        /// <returns></returns>
        [Route("info")]
        [HttpGet]
        public async Task<UserViewModel> GetUserInfo()
        {
            User user = await _authenticationService.GetUser(HttpContext);
            return await _service.GetUserInfo(user.UserKey);
        }

        /// <summary>
        /// Метод для обновления информации о пользователе.
        /// </summary>
        /// <returns></returns>
        [Route("update")]
        [HttpPost]
        public async Task<ActionResult> SetUserWishes([FromBody] UpdateUserViewModel model)
        {
            User user = await _authenticationService.GetUser(HttpContext);
            await _service.UpdateUser(model, user);

            return Ok();
        }
    }
}
