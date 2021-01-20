using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartAnalytics.SecretSanta.Services.Services.Base;
using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SmartAnalytics.SecretSanta.Data.Core.Models;
using System.Collections.Generic;
using SmartAnalytics.SecretSanta.Data.Core.Enums;
using SmartAnalytics.SecretSanta.Services.Models;
using SmartAnalytics.SecretSanta.Resources.Notifications;
using SmartAnalytics.SecretSanta.Services.Enums;

namespace SmartAnalytics.SecretSanta.Services.Services
{
    public class SchedulerService : BaseSingletonService<SchedulerService>
    {
        private const string ApplicationSection = "Application";
        private const string ScheduleReminderTimesSection = "ScheduleReminderTimes";
        private const string CustomMessagesInLastDaySection = "CustomMessagesInLastDay";

        private readonly List<SchedulerTimer> _reminderTimes;
        private bool _startCongratulationsMessageSended = false;

        public SchedulerService(
            IConfiguration configuration,
            ILogger<SchedulerService> logger,
            IServiceScopeFactory scopeFactory)
            : base(configuration, logger, scopeFactory)
        {
            _reminderTimes = GetReminderTimes();
            if (_reminderTimes.Any())
            {
                RunSheduler();
            }
        }

        protected override IConfigurationSection GetFromConfig(string sectionName)
        {
            return base.GetFromConfig($"{ApplicationSection}:{sectionName}");
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

        private List<SchedulerTimer> GetEverydayReminderTimes()
        {
            var section = GetFromConfig(ScheduleReminderTimesSection);
            return section == null
                ? new List<SchedulerTimer> { }
                : section.Get<TimeSpan[]>()
                    .Select(x => new SchedulerTimer(x, SchedulerTimerType.Everyday))
                    .ToList();
        }

        private List<SchedulerTimer> GetCustomInLastDayReminderTimes()
        {
            var section = GetFromConfig(CustomMessagesInLastDaySection);
            if (section == null)
            {
                return new List<SchedulerTimer>();
            }             
            var customTimers = section.Get<List<CustomNotification>>();
            return customTimers
                .Select(x => new SchedulerTimer(x.Time, SchedulerTimerType.CustomInLastDay, x))
                .ToList();
        }

        private List<SchedulerTimer> GetReminderTimes()
        {
            List<SchedulerTimer> reminderTimes = GetEverydayReminderTimes();
            List<SchedulerTimer> customReminderTimes = GetCustomInLastDayReminderTimes();
            reminderTimes.AddRange(customReminderTimes);
            DateTime tossDeadline = ApplicationService.DeadlineToss;
            DateTime congratulationsDeadline = ApplicationService.GiftPreparationDeadline;
            reminderTimes.AddRange(new List<SchedulerTimer> {
                GetOnHourTimer(SchedulerTimerType.TossDeadlineOneHour, tossDeadline),
                GetOnFiveMinutesTimer(SchedulerTimerType.TossDeadlineFiveMinute, tossDeadline),
                GetOnHourTimer(SchedulerTimerType.CongratulationsDeadlineOneHour, congratulationsDeadline),
                GetOnFiveMinutesTimer(SchedulerTimerType.CongratulationsDeadlineFiveMinute, congratulationsDeadline),
                new SchedulerTimer(congratulationsDeadline.TimeOfDay, SchedulerTimerType.CongratulationsStart),
            });
            return reminderTimes
                .OrderBy(x => x.Time)
                .ToList();
        }

        private SchedulerTimer GetOnHourTimer(SchedulerTimerType type, DateTime deadline)
        {
            TimeSpan tossDeadlineOneHour = deadline.TimeOfDay - new TimeSpan(1, 0, 0);
            if (deadline.TimeOfDay.Hours < 1)
            {
                tossDeadlineOneHour += new TimeSpan(1, 0, 0, 0);
            }
            return new SchedulerTimer(tossDeadlineOneHour, type);
        }

        private SchedulerTimer GetOnFiveMinutesTimer(SchedulerTimerType type, DateTime deadline)
        {
            TimeSpan tossDeadlineOneHour = deadline.TimeOfDay - new TimeSpan(0, 5, 0);
            if (deadline.TimeOfDay.Hours < 1 && deadline.TimeOfDay.Minutes < 5)
            {
                tossDeadlineOneHour += new TimeSpan(1, 0, 0, 0);
            }
            return new SchedulerTimer(tossDeadlineOneHour, type);
        }

        private void RunSheduler()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    foreach (var timer in _reminderTimes)
                    {
                        await SetUpTimer(timer);
                    }
                    await AwaitEndOfDay();
                }
            });
        }
        private async Task AwaitEndOfDay()
        {
            var endOfDay = new TimeSpan(1, 0, 0, 0);
            DateTime current = DateTime.Now;
            TimeSpan timeToGo = endOfDay - current.TimeOfDay;
            await Task.Delay(timeToGo);
        }

        private async Task SetUpTimer(SchedulerTimer timer)
        {
            DateTime current = DateTime.Now;
            TimeSpan timeToGo = timer.Time - current.TimeOfDay;
            if (timeToGo < TimeSpan.Zero)
            {
                return;
            }
            await Task.Delay(timeToGo);

            _logger.LogInformation(string.Format(
                "Начало оповещения пользователей по таймеру {0}.",
                timer.Time.ToString(@"hh\:mm")));

            await RunSheduledWork(timer);

            _logger.LogInformation(string.Format(
                "Конец оповещения пользователей по таймеру {0}.",
                timer.Time.ToString(@"hh\:mm")));
        }

        private async Task RunSheduledWork(SchedulerTimer timer)
        {
            await UseApplicationContextAsync(async (context, scope) =>
            {
                bool tossIsMaked = TossService.TossIsMaked;
                TimeSpan diffTime = GetTimeRemainingBeforeEvent(tossIsMaked);
                if (diffTime < TimeSpan.Zero &&
                    (timer.Type != SchedulerTimerType.CongratulationsStart || _startCongratulationsMessageSended))
                {
                    return;
                }

                string reminingTimeText = GetTimeText(tossIsMaked, diffTime.Days);
                var notificationService = scope.ServiceProvider.GetService<NotificationService>();

                List<User> users = await context.Users.ToListAsync();
                foreach (User user in users)
                {
                    var notifications = new List<NotificationInfo>();
                    AddNotifications(timer, users, notifications, user, tossIsMaked, diffTime, reminingTimeText);
                    await notificationService.AddNotificationsForUser(user.Id, notifications);
                }
                if (timer.Type == SchedulerTimerType.CongratulationsStart)
                {
                    _startCongratulationsMessageSended = true;
                }
            });
        }

        private void AddNotifications(
            SchedulerTimer timer,
            List<User> users,
            List<NotificationInfo> notifications,
            User user,
            bool tossIsMaked,
            TimeSpan diffTime,
            string reminingTimeText)
        {
            var oneDay = new TimeSpan(1, 0, 0, 0);
            bool itIsToday = diffTime < oneDay;

            switch (timer.Type)
            {
                case SchedulerTimerType.Everyday:
                    AddEverydayNotifications(users, notifications, user, tossIsMaked, reminingTimeText);
                    break;
                case SchedulerTimerType.TossDeadlineOneHour:
                    if (itIsToday && !tossIsMaked)
                    {
                        AddTypedNotifications(notifications, user,
                            Notifications.SchedulerService_TossDeadlineOneHour_Title,
                            Notifications.SchedulerService_TossDeadlineOneHour_Message);
                    }
                    break;
                case SchedulerTimerType.TossDeadlineFiveMinute:
                    if (itIsToday && !tossIsMaked)
                    {
                        AddTypedNotifications(notifications, user,
                            Notifications.SchedulerService_TossDeadlineFiveMinute_Title,
                            Notifications.SchedulerService_TossDeadlineFiveMinute_Message);
                    }
                    break;
                case SchedulerTimerType.CongratulationsDeadlineOneHour:
                    if (itIsToday && tossIsMaked)
                    {
                        AddTypedNotifications(notifications, user,
                            Notifications.SchedulerService_CongratulationsDeadlineOneHour_Title,
                            user.TargetUserStatus == TargetUserStatus.GiftIsReady 
                                ? Notifications.SchedulerService_CongratulationsDeadlineOneHour_GiftIsReady_Message
                                : Notifications.SchedulerService_CongratulationsDeadlineOneHour_Message);
                    }
                    break;
                case SchedulerTimerType.CongratulationsDeadlineFiveMinute:
                    if (itIsToday && tossIsMaked)
                    {
                        AddTypedNotifications(notifications, user,
                            Notifications.SchedulerService_CongratulationsDeadlineFiveMinute_Title,
                            user.TargetUserStatus == TargetUserStatus.GiftIsReady
                                ? Notifications.SchedulerService_CongratulationsDeadlineFiveMinute_GiftIsReady_Message
                                : Notifications.SchedulerService_CongratulationsDeadlineFiveMinute_Message);
                    }
                    break;
                case SchedulerTimerType.CongratulationsStart:
                    if (itIsToday && tossIsMaked)
                    {
                        AddTypedNotifications(notifications, user,
                            Notifications.SchedulerService_CongratulationsStart_Title,
                            string.Format(
                                Notifications.SchedulerService_CongratulationsStart_Message, 
                                ApplicationService.CongratulationsLocationText));
                    }
                    break;
                case SchedulerTimerType.CustomInLastDay:
                    if (itIsToday && tossIsMaked)
                    {
                        AddCustomNotification(timer.CustomNotification, notifications, user);
                    }
                    break;
                default:
                    break;
            }
        }

        private TimeSpan GetTimeRemainingBeforeEvent(bool tossIsMaked)
        {
            DateTime toTime = tossIsMaked
                ? ApplicationService.GiftPreparationDeadline
                : ApplicationService.DeadlineToss;
            return toTime - DateTime.Now;
        }

        private string GetTimeText(bool tossIsMaked, int daysCount)
        {
            if (daysCount > 1)
            {
                string mainText = tossIsMaked
                    ? Notifications.SchedulerService_UntilCongratulationsLeft
                    : Notifications.SchedulerService_BeforeMakeTossLeft;
                string daysText = daysCount > 4
                    ? Notifications.SchedulerService_TimeText_ManyDays
                    : Notifications.SchedulerService_TimeText_FewDays;
                return string.Format(mainText, $"{daysCount} {daysText}");
            }
            else if (daysCount == 1)
            {
                return tossIsMaked
                    ? Notifications.SchedulerService_UntilCongratulationsLeft_OneDay
                    : Notifications.SchedulerService_BeforeMakeTossLeft_OneDay;
            }
            else
            {
                return tossIsMaked
                    ? Notifications.SchedulerService_CongratulationsToday
                    : Notifications.SchedulerService_TossToday;
            }
        }

        private void AddTypedNotifications(
            List<NotificationInfo> notifications,
            User user,
            string title,
            string message
            )
        {
            if (user.Status != UserStatus.Involved)
            {
                return;
            }
            notifications.Add(new NotificationInfo(title, message));
        }
        
        private void AddEverydayNotifications(
            List<User> users,
            List<NotificationInfo> notifications,
            User user,
            bool tossIsMaked,
            string reminingTimeText)
        {
            if (tossIsMaked)
            {
                if (user.Status == UserStatus.Involved)
                {
                    switch (user.TargetUserStatus)
                    {
                        case TargetUserStatus.GiftInformationNotViewed:
                            notifications.Add(new NotificationInfo(
                                Notifications.SchedulerService_GiftInformationNotViewed_Title,
                                string.Format(Notifications.SchedulerService_GiftInformationNotViewed_Description, reminingTimeText)
                            ));
                            return;
                        case TargetUserStatus.GiftInformationViewed:
                            User congratulationsUser = users.Single(x => x.TargetUserId == user.Id);
                            if (congratulationsUser.TargetUserStatus == TargetUserStatus.GiftIsReady)
                            {
                                notifications.Add(new NotificationInfo(
                                    Notifications.SchedulerService_GiftInformationViewed_Negative_Title,
                                    string.Format(Notifications.SchedulerService_GiftInformationViewed_Negative_Description, reminingTimeText)
                                ));
                            }
                            else
                            {
                                notifications.Add(new NotificationInfo(
                                    Notifications.SchedulerService_GiftInformationViewed_Positive_Title,
                                    string.Format(Notifications.SchedulerService_GiftInformationViewed_Positive_Description, reminingTimeText)
                                ));
                            }
                            return;
                        case TargetUserStatus.GiftIsReady:
                        case TargetUserStatus.Undefined:
                        default:
                            return;
                    }
                }
            }
            else
            {
                if (user.Status == UserStatus.ExpectedToChoose)
                {
                    notifications.Add(new NotificationInfo(
                        Notifications.SchedulerService_ExpectedToChoose_Title,
                        string.Format(Notifications.SchedulerService_ExpectedToChoose_Description, reminingTimeText)
                    )); ;
                }
            }
        }

        private void AddCustomNotification(
            CustomNotification customNotification,
            List<NotificationInfo> notifications,
            User user)
        {
            if (user.Status == UserStatus.Involved)
            {
                notifications.Add(
                    new NotificationInfo(customNotification.Title, customNotification.Message)
                );
            }
        }
    }
}
