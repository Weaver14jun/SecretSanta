using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartAnalytics.SecretSanta.Services.Services.Base;
using SmartAnalytics.SecretSanta.Data.Core.Models;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System;
using SmartAnalytics.SecretSanta.Services.Models;
using SmartAnalytics.SecretSanta.Services.ViewModels;

namespace SmartAnalytics.SecretSanta.Services.Services
{
    public class NotificationService : BaseApplicationContextService<NotificationService>
    {
        public NotificationService(
            IConfiguration configuration,
            ILogger<NotificationService> logger,
            ApplicationContext context)
            : base(configuration, logger, context)
        {
        }

        public async Task AddNotificationsForUsers(List<int> usersIds, List<NotificationInfo> infoList)
        {
            if (!infoList.Any())
            {
                return;
            }

            int notificationId = await _context.GetNextNotificationId();
            DateTime creatingTime = DateTime.UtcNow;

            var notifications = new List<Notification>();
            foreach (var info in infoList)
            {
                foreach (var userId in usersIds)
                {
                    notifications.Add(new Notification
                    {
                        Id = notificationId,
                        Message = info.Message,
                        Title = info.Title,
                        Created = creatingTime,
                        Viewed = false,
                        UserId = userId,
                        Sended = false,
                    });
                    notificationId++;
                }
            }

            await _context.Notifications.AddRangeAsync(notifications);
            await _context.SaveChangesAsync(true);
        }

        public async Task AddNotificationsForAll(List<NotificationInfo> infoList)
        {
            List<int> usersIds = await _context.Users
                .Select(x => x.Id)
                .ToListAsync();
            await AddNotificationsForUsers(usersIds, infoList);
        }

        public async Task AddNotificationsForUser(int userId, List<NotificationInfo> infoList)
        {
            await AddNotificationsForUsers(new List<int> { userId }, infoList);
        }

        public async Task AddNotificationForAll(NotificationInfo info)
        {
            await AddNotificationsForAll(new List<NotificationInfo> { info });
        }

        public async Task AddNotificationsForUsers(List<int> usersIds, NotificationInfo info)
        {
            await AddNotificationsForUsers(usersIds, new List<NotificationInfo> { info });
        }

        public async Task AddNotificationForUser(int userId, NotificationInfo info)
        {
            await AddNotificationsForUser(userId, new List<NotificationInfo> { info });
        }

        public async Task AddNotificationForUsers(List<int> usersIds, NotificationInfo info)
        {
            await AddNotificationsForUsers(usersIds, new List<NotificationInfo> { info });
        }

        public async Task SetNotificationViewed(int notificationId)
        {
            Notification message = await _context.Notifications
                .SingleAsync(x => x.Id == notificationId);
            message.Viewed = true;
            await _context.SaveChangesAsync(true);
        }

        public async Task ClearAllNotifications()
        {
            _context.RemoveRange(_context.Notifications);
            await _context.SaveChangesAsync(true);
        }

        public async Task<List<NotificationViewModel>> GetNotifications(int userId)
        {
            return await _context.Notifications
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.Created)
                .Select(x => new NotificationViewModel(x))
                .ToListAsync();
        }

    }
}
