using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartAnalytics.SecretSanta.Data.Core.Models;
using SmartAnalytics.SecretSanta.Services.Controllers.Base;
using SmartAnalytics.SecretSanta.Services.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using SmartAnalytics.SecretSanta.Services.ViewModels;
using System.Net;

namespace SmartAnalytics.SecretSanta.Services.Controllers
{
    [Route("admin")]
    [ApiController]
    public class AdminController: BaseController<AdminController>
    {
        private readonly AdminService _adminService;
        private readonly AuthenticationService _authenticationService;
        private readonly TossService _tossService;

        public AdminController(
            AdminService adminService,
            IConfiguration config,
            ILogger<AdminController> logger,
            AuthenticationService authenticationService,
            NotificationService notificationService,
            TossService tossService)
            : base(config, logger)
        {
            _adminService = adminService;
            _authenticationService = authenticationService;
            _tossService = tossService;
        }

        /// <summary>
        /// Метод для получения админом списка всех пользователей.
        /// </summary>
        /// <returns></returns>
        [Route("users")]
        [HttpGet]
        public async Task<IEnumerable<UserViewModel>> GetAllUsers()
        {
            User user = await _authenticationService.GetUser(HttpContext);
            CheckAdmin(user);
            return await _adminService.GetUsers();
        }

        /// <summary>
        /// Метод для обновления админом информации о пользователе.
        /// </summary>
        /// <returns></returns>
        [Route("updateUser/{domain}/{userKey}")]
        [HttpPost]
        public async Task<ActionResult> SetAdminAsync(
            string domain, string userKey, [FromBody] AdminUpdateUserViewModel userModel)
        {
            User user = await _authenticationService.GetUser(HttpContext);
            CheckAdmin(user);
            await _adminService.UpdateUser($"{domain}\\{userKey}", userModel);
            return Ok();
        }

        /// <summary>
        /// Метод для проведения админом жеребьевки.
        /// </summary>
        /// <returns></returns>
        [Route("makeToss")]
        [HttpGet]
        public async Task<ActionResult> MakeToss()
        {
            User user = await _authenticationService.GetUser(HttpContext);
            CheckAdmin(user);
            await _tossService.MakeToss();

            return Ok();
        }

        /// <summary>
        /// Метод для обнуления админом жеребьевки.
        /// </summary>
        /// <returns></returns>
        [Route("nullifyToss")]
        [HttpGet]
        public async Task<ActionResult> NullifyToss()
        {
            User user = await _authenticationService.GetUser(HttpContext);
            CheckAdmin(user);
            await _tossService.NullifyToss();

            return Ok();
        }

        private void CheckAdmin(User user)
        {
            if (!_authenticationService.CheckIsAdmin(user))
            {
                throw new System.Web.Http.HttpResponseException(HttpStatusCode.Unauthorized);
            }
        }
    }
}
