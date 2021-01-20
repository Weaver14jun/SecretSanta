using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartAnalytics.SecretSanta.Data.Core.Models;
using SmartAnalytics.SecretSanta.Services.Models;
using SmartAnalytics.SecretSanta.Services.Services.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security;
using System.Threading.Tasks;

namespace SmartAnalytics.SecretSanta.Services.Services
{
    public class MailNotificationService : BaseSingletonService<MailNotificationService>
    {
        private const int DefaultCheckTimeout = 15000;
        private const int DefaultNumberEmailsSentAtTime = 5;
        private const string TemplateReplacingTitle = "{Title}";
        private const string TemplateReplacingMessage = "{Message}";
        private const string TemplateReplacingUserName = "{UserName}";
        private const string TemplateReplacingYear = "{Year}";
        private const string TemplateReplacingTeamName = "{TeamName}";

        private const string MailSection = "MailNotification";

        private const string CheckTimeoutSection = "CheckTimeoutInSeconds";
        private const string NumberEmailsSentAtTimeSection = "NumberEmailsSentAtTime";
        private const string TemplateHtmlFileSection = "TemplateHtmlFile";
        private const string SmtpSection = "Smtp";

        private const string SmtpServerSection = "Server";
        private const string SmtpPortSection = "Port";
        private const string SmtpUseTLSSection = "UseTLS";
        private const string SmtpLoginSection = "Login";
        private const string SmtpPasswordSection = "Password";

        private readonly int _checkTimeout;
        private readonly int _numberEmailsSentAtTime;
        private readonly string _template;

        private readonly string _server;
        private readonly int _port;
        private readonly bool _useTLS;
        private readonly string _login;
        private readonly SecureString _password;

        public MailNotificationService(
            IConfiguration configuration,
            ILogger<MailNotificationService> logger,
            IServiceScopeFactory scopeFactory)
            : base(configuration, logger, scopeFactory)
        {
            IConfigurationSection smtp = GetFromConfig(SmtpSection);
            if (!smtp.Exists())
            {
                return;
            }
            _server = smtp.GetValue<string>(SmtpServerSection);
            _port = smtp.GetValue<int>(SmtpPortSection);
            _useTLS = smtp.GetValue<bool>(SmtpUseTLSSection);
            _login = smtp.GetValue<string>(SmtpLoginSection);
            _password = new SecureString();
            SetSmtpPassword(smtp.GetValue<string>(SmtpPasswordSection));

            _checkTimeout = GetCheckTimeout();
            _numberEmailsSentAtTime = GetNumberEmailsSentAtTime();
            _template = GetNotificationMessageTemplate();

            if (_checkTimeout > 0)
            {
                RunMailSender();
            }

        }

        protected override IConfigurationSection GetFromConfig(string sectionName)
        {
            return base.GetFromConfig($"{MailSection}:{sectionName}");
        }

        protected override void CheckServices()
        {
            using var scope = _scopeFactory.CreateScope();
            if (ApplicationService.NeedSetDeadlines)
            {
                scope.ServiceProvider.GetService<ApplicationService>();
            }
            if (TossService.TossNeedCheck)
            {
                scope.ServiceProvider.GetService<TossService>();
            }
        }

        private void SetSmtpPassword(string password)
        {
            foreach (var character in password) {
                _password.AppendChar(character);
            }
        }

        private string GetNotificationMessageTemplate()
        {
            IConfigurationSection section = GetFromConfig(TemplateHtmlFileSection);
            string templdateFilePath = section != null ? section.Value : string.Empty;
            if (string.IsNullOrWhiteSpace(templdateFilePath) || !File.Exists(templdateFilePath))
            {
                return GetDefaultTemplate();
            }
            try
            {
                return File.ReadAllText(templdateFilePath);
            }
            catch
            {
                _logger.LogError(string.Format("Файл html шаблона не найден: {0}", templdateFilePath));
                return GetDefaultTemplate();
            }
        }

        private string GetDefaultTemplate()
        {
            return $"{TemplateReplacingTitle}</br>{TemplateReplacingMessage}";
        }

        private int GetCheckTimeout()
        {
            IConfigurationSection section = GetFromConfig(CheckTimeoutSection);
            return section == null ? DefaultCheckTimeout : section.Get<int>() * 1000;
        }

        private int GetNumberEmailsSentAtTime()
        {
            IConfigurationSection section = GetFromConfig(NumberEmailsSentAtTimeSection);
            return section == null ? DefaultNumberEmailsSentAtTime : section.Get<int>();
        }

        private void RunMailSender()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(_checkTimeout);
                    await UseApplicationContextAsync(async (context, scope) =>
                        await CheckNotificationsQueueForSending(context)
                    );
                }
            });
        }

        private async Task CheckNotificationsQueueForSending(ApplicationContext context)
        {
            List<Notification> notifications = await context.Notifications
                .Include(x => x.User)
                .Where(x => !x.Sended)
                .Take(_numberEmailsSentAtTime)
                .ToListAsync();
            if (!notifications.Any())
            {
                return;
            }
            foreach (var notification in notifications)
            {
                await SendNotificationOnMail(notification);
            }
            await context.SaveChangesAsync(true);
        }

        private async Task SendNotificationOnMail(Notification notification)
        {
            string eMail = notification.User.Email;
            notification.Sended = true;
            if (string.IsNullOrWhiteSpace(eMail))
            {
                return;
            }
            _logger.LogDebug(string.Format("Отправка уведомления (Id = {0}) на почту {1}.", notification.Id, eMail));
            await SendMail(GetNotificationEmailMessage(notification));
        }

        private EmailMessage GetNotificationEmailMessage(Notification notification)
        {
            return new EmailMessage(
                notification.User.Email,
                notification.Title,
                _template
                    .Replace(TemplateReplacingTitle, notification.Title)
                    .Replace(TemplateReplacingMessage, notification.Message)
                    .Replace(TemplateReplacingUserName, notification.User.Name)
                    .Replace(TemplateReplacingYear, DateTime.UtcNow.Year.ToString())
                    .Replace(TemplateReplacingTeamName, ApplicationService.TeamName)
                );
        }

        private async Task SendMail(EmailMessage emailMessage)
        {
            try
            {
                using var client = new SmtpClient(_server, _port);
                client.EnableSsl = _useTLS;
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(_login, _password);
                client.DeliveryMethod = SmtpDeliveryMethod.Network;

                var message = new MailMessage(
                    _login, emailMessage.ToAddress, emailMessage.Subject, emailMessage.Content
                );
                message.IsBodyHtml = true;

                await client.SendMailAsync(message);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Ошибка отправки сообщения по почте.");
            }
        }
    }
}
