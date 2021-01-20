using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartAnalytics.SecretSanta.Data.Core.Enums;
using SmartAnalytics.SecretSanta.Data.Core.Models;
using SmartAnalytics.SecretSanta.Resources.Notifications;
using SmartAnalytics.SecretSanta.Services.Models;
using SmartAnalytics.SecretSanta.Services.Services.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SmartAnalytics.SecretSanta.Services.Services
{
    public class TossService : BaseApplicationContextService<TossService>
    {
        public static bool TossNeedCheck { get; private set; } = true;
        public static bool TossIsMaked { get; private set; } = false;

        private readonly NotificationService _notificationService;

        public TossService(
            IConfiguration configuration,
            ILogger<TossService> logger,
            ApplicationContext context,
            NotificationService notificationService)
            : base(configuration, logger, context)
        {
            _notificationService = notificationService;
            if (TossNeedCheck)
            {
                TossIsMaked = CheckTossStatus();
                TossNeedCheck = false;
            }
        }

        public bool CheckTossStatus()
        {
            var involvedUsers = _context.Users
                .Where(x => x.Status == UserStatus.Involved)
                .ToListAsync()
                .Result;
            var involvedUsersIds = involvedUsers
                .Select(x => x.Id)
                .ToList();
            if (involvedUsersIds.Count < 2)
            {
                return false;
            }
            return involvedUsers.All(x => x.TargetUserId.HasValue &&
                involvedUsersIds.Contains(x.TargetUserId.Value));
        }

        public async Task MakeToss()
        {
            bool isReToss = TossIsMaked;
            List<User> allUsers = await _context.Users.ToListAsync();

            Dictionary<Guid, User> usersMap = allUsers
                .Where(x => x.Status == UserStatus.Involved)
                .ToDictionary(x => Guid.NewGuid(), x => x);

            WorkToss(usersMap);

            await _context.Users
                .Where(x => x.Status == UserStatus.ExpectedToChoose)
                .ForEachAsync(x => x.Status = UserStatus.Refused);

            await _context.SaveChangesAsync(true);
            TossIsMaked = true;

            List<int> involvedUsersIds = allUsers
                .Where(x => x.Status == UserStatus.Involved)
                .Select(x => x.Id)
                .ToList();
            await NotifyMakeTossInvolvedUsers(isReToss, involvedUsersIds);

            List<int> notInvolvedUsersIds = allUsers
                .Where(x => x.Status != UserStatus.Involved)
                .Select(x => x.Id)
                .ToList();
            await NotifyMakeTossNotInvolvedUsers(isReToss, notInvolvedUsersIds);
        }

        public async Task NullifyToss()
        {
            List<User> usersList = await _context.Users.ToListAsync();
            foreach (var user in usersList)
            {
                user.TargetUserId = null;
                user.TargetUserStatus = TargetUserStatus.Undefined;
            }
            await _context.SaveChangesAsync(true);
            TossIsMaked = false;

            await _notificationService.ClearAllNotifications();
            await NotifyNillifyToss();
        }

        private async Task NotifyMakeTossInvolvedUsers(bool isReToss, List<int> usersIds)
        {
            if (isReToss)
            {
                await _notificationService.AddNotificationForUsers(usersIds,
                    new NotificationInfo(
                        Notifications.TossService_ReTossMaked_Involved_Title,
                        Notifications.TossService_ReTossMaked_Involved_Message));
            }
            else
            {
                await _notificationService.AddNotificationForUsers(usersIds,
                    new NotificationInfo(
                        Notifications.TossService_TossMaked_Involved_Title,
                        Notifications.TossService_TossMaked_Involved_Message));
            }
        }

        private async Task NotifyMakeTossNotInvolvedUsers(bool isReToss, List<int> usersIds)
        {
            if (isReToss)
            {
                await _notificationService.AddNotificationForUsers(usersIds,
                    new NotificationInfo(
                        Notifications.TossService_ReTossMaked_NotInvolved_Title,
                        Notifications.TossService_ReTossMaked_NotInvolved_Message));
            }
            else
            {
                await _notificationService.AddNotificationForUsers(usersIds,
                    new NotificationInfo(
                        Notifications.TossService_TossMaked_NotInvolved_Title,
                        Notifications.TossService_TossMaked_NotInvolved_Message));
            }
        }

        private async Task NotifyNillifyToss()
        {
            await _notificationService.AddNotificationForAll(
                new NotificationInfo(
                    Notifications.TossService_TossNullified_Title,
                    Notifications.TossService_TossNullified_Message));
        }

        private void WorkToss(Dictionary<Guid, User> usersMap)
        {
            Random random = new Random();
            List<TossUserMap> usersKeys = usersMap
                .Select(x => new TossUserMap { Key = x.Key, Used = false })
                .OrderBy(x => x.Key)
                .ToList();

            Guid? userBookingKey = null;
            int targetsCountdown = usersMap.Count;
            TossUserMap lastTargetUserMap = usersKeys.Last();

            Dictionary<Guid, int> targetUsersKeysMap = usersKeys
                .ToDictionary(x => x.Key, targetUserMap => {
                    if (targetsCountdown == 3)
                    {
                        userBookingKey = targetUserMap.Key;
                    }
                    if (targetsCountdown == 1)
                    {
                        userBookingKey = null;
                    }

                    List<TossUserMap> freeKeys = usersKeys
                        .Where(y => y.Key != targetUserMap.Key && !y.Used &&
                            (userBookingKey.HasValue ? userBookingKey.Value != y.Key : true))
                        .ToList();

                    int randomIndex = random.Next(0, freeKeys.Count);
                    TossUserMap userKeyMap = freeKeys[randomIndex];
                    if (targetsCountdown == 2 && !lastTargetUserMap.Used)
                    {
                        userKeyMap = lastTargetUserMap;
                        userBookingKey = null;
                    }
                    userKeyMap.Used = true;

                    if (targetsCountdown == 3)
                    {
                        if (lastTargetUserMap.Used)
                        {
                            userBookingKey = null;
                        }
                    }

                    targetsCountdown--;
                    return usersMap[userKeyMap.Key].Id;
                });
            foreach (var userMap in usersMap)
            {
                User user = userMap.Value;
                user.TargetUserId = targetUsersKeysMap[userMap.Key];
                user.TargetUserStatus = TargetUserStatus.GiftInformationNotViewed;
            }
        }
    }
}
