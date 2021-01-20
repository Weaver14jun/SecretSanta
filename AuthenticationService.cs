using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartAnalytics.SecretSanta.Data.Core.Models;
using SmartAnalytics.SecretSanta.Services.Services.Base;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Net;
using System.Web.Http;
using SmartAnalytics.SecretSanta.Services.Models;
using System.Security.Principal;
using SmartAnalytics.SecretSanta.Data.Core.Enums;
using SmartAnalytics.SecretSanta.Resources.Notifications;
using System;

namespace SmartAnalytics.SecretSanta.Services.Services
{
    public class AuthenticationService : BaseApplicationContextService<AuthenticationService>
    {
        private const string AuthenticationSection = "Authentication";
        private const string AdminSection = "Admins";
        private const string DomainListSection = "Domains";
        private const string UserNameMailPostfixSection = "UserNameMailPostfix";

        private readonly List<string> _admins;
        private readonly List<string> _allowedDomains;
        private readonly string _userNameMailPostfix;
        private readonly bool _needSyncAdminsInDb = true;

        private readonly NotificationService _notificationService;

        public AuthenticationService(
            IConfiguration configuration,
            ILogger<AuthenticationService> logger,
            ApplicationContext context,
            NotificationService notificationService)
            : base(configuration, logger, context)
        {
            _notificationService = notificationService;
            _admins = GetAdmins();
            _allowedDomains = GetAllowedDomains();
            _userNameMailPostfix = GetStringFromConfig(UserNameMailPostfixSection);
            if (_needSyncAdminsInDb)
            {
                SyncAdminsInDb().Wait();
                _needSyncAdminsInDb = false;
            }
        }

        public bool CheckIsAbsoluteAdmin(LoginInfo info)
        {
            return CheckIsAbsoluteAdmin(info.DomainUserKey);
        }

        public bool CheckIsAbsoluteAdmin(User user)
        {
            return CheckIsAbsoluteAdmin(user.UserKey);
        }

        public bool CheckIsAbsoluteAdmin(string userKey)
        {
            return _admins.Contains(userKey);
        }

        public bool CheckIsAdmin(User user)
        {
            return user.IsAdmin || CheckIsAbsoluteAdmin(user);
        }

        public async Task<User> GetUser(HttpContext httpContext)
        {
            LoginInfo info = GetLoginInfo(httpContext);

            if (_allowedDomains.All(x => !x.Contains(info.Domain)))
            {
                throw new HttpResponseException(HttpStatusCode.Forbidden);
            }

            User user = await _context.Users
                .SingleOrDefaultAsync(x => x.UserKey.Equals(info.DomainUserKey));

            if (user == null)
            {
                user = await AddNewUser(info);
            }

            return user;
        }

        protected override IConfigurationSection GetFromConfig(string sectionName)
        {
            return base.GetFromConfig($"{AuthenticationSection}:{sectionName}");
        }

        private async Task SyncAdminsInDb()
        {
            List<User> notSyncAdmins = await _context.Users
                .Where(x => !x.IsAdmin && _admins.Contains(x.UserKey))
                .ToListAsync();
            if (!notSyncAdmins.Any())
            {
                return;
            }
            foreach (var user in notSyncAdmins)
            {
                user.IsAdmin = true;
            }
            await _context.SaveChangesAsync(true);
        }

        private async Task<User> AddNewUser(LoginInfo userInfo)
        {
            var newUser = new User
            {
                Id = await _context.GetNextUserId(),
                UserKey = userInfo.DomainUserKey,
                IsAdmin = CheckIsAbsoluteAdmin(userInfo),
                Status = UserStatus.ExpectedToChoose,
                TargetUserStatus = TargetUserStatus.Undefined,
                Name = GetUserName(userInfo.UserKey),
                Email = GetUserEMail(userInfo),
                Color = GetUserColor(),
            };
            await _context.Users.AddAsync(newUser);
            await _context.SaveChangesAsync(true);

            await _notificationService.AddNotificationForUser(newUser.Id, new NotificationInfo(
                Notifications.AuthenticationService_Welcome_Title,
                Notifications.AuthenticationService_Welcome_Message));

            return newUser;
        }

        private string GetUserColor()
        {
            var random = new Random();
            int hue = random.Next(0, 255);
            int saturation = random.Next(40, 75);
            int lightness = random.Next(25, 50);
            return $"hsl({hue},{saturation}%,{lightness}%)";
        }

        private string GetUserEMail(LoginInfo userInfo)
        {
            if (string.IsNullOrWhiteSpace(_userNameMailPostfix))
            {
                return "";
            }
            return $"{userInfo.UserKey}@{_userNameMailPostfix}";
        }

        private List<string> GetAdmins()
        {
            IConfigurationSection section = GetFromConfig(AdminSection);
            return section == null
                ? new List<string>()
                : section.Get<List<string>>();
        }

        private List<string> GetAllowedDomains()
        {
            IConfigurationSection domainsSection = GetFromConfig(DomainListSection);
            return domainsSection == null
                ? new List<string>()
                : domainsSection.Get<List<string>>();
        }

        private string GetUserName(string userKey)
        {
            string[] names = userKey.Split(".");
            string firstName = FirstLetterToUpper(names[0]);
            string lastName = FirstLetterToUpper(names[1]);
            return $"{new string(firstName)} {new string(lastName)}";
        }

        private string FirstLetterToUpper(string text)
        {
            string firstLetter = text.Substring(0, 1);
            string restText = text.Substring(1);
            return $"{firstLetter.ToUpper()}{restText}";
        }

        private LoginInfo GetLoginInfo(HttpContext httpContext)
        {
            var user = (WindowsIdentity)httpContext.User.Identity;
            var loginInfo = new LoginInfo(httpContext);

            WindowsIdentity.RunImpersonated(user.AccessToken, () =>
            {
                var impersonatedUser = WindowsIdentity.GetCurrent();
                loginInfo.Name = impersonatedUser.Name;
            });

            return loginInfo;
        }
    }
}
