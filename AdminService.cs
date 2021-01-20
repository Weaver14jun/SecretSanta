using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartAnalytics.SecretSanta.Services.Services.Base;
using SmartAnalytics.SecretSanta.Data.Core.Models;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using SmartAnalytics.SecretSanta.Services.ViewModels;
using SmartAnalytics.SecretSanta.Services.Exceptions;
using SmartAnalytics.SecretSanta.Data.Core.Enums;
using SmartAnalytics.SecretSanta.Services.Models;
using SmartAnalytics.SecretSanta.Resources.Notifications;
using ExceptionsResources = SmartAnalytics.SecretSanta.Resources.Exceptions.Exceptions;

namespace SmartAnalytics.SecretSanta.Services.Services
{
    public class AdminService : BaseApplicationContextService<AdminService>
    {
        private const int MaxWishesLength = 200;

        private readonly AuthenticationService _authenticationService;
        private readonly NotificationService _notificationService;

        private static readonly Dictionary<UserStatus, string> UserStatusNotificationMessages =
            new Dictionary<UserStatus, string>
            {
                { UserStatus.ExpectedToChoose, Notifications.AdminService_User_Status_MessageExpectedToChoose },
                { UserStatus.Involved, Notifications.AdminService_User_Status_MessageInvolved },
                { UserStatus.Refused, Notifications.AdminService_User_Status_MessageRefused },
            };

        private static readonly Dictionary<TargetUserStatus, string> TargetUserStatusNotificationMessages =
            new Dictionary<TargetUserStatus, string>
            {
                { 
                    TargetUserStatus.GiftInformationNotViewed,
                    Notifications.AdminService_User_TargetStatus_MessageGiftInformationNotViewed
                },
                { 
                    TargetUserStatus.GiftInformationViewed,
                    Notifications.AdminService_User_TargetStatus_MessageGiftInformationViewed
                },
                { 
                    TargetUserStatus.GiftIsReady,
                    Notifications.AdminService_User_TargetStatus_MessageGiftIsReady
                },
            };

        public AdminService(
            IConfiguration configuration,
            ILogger<AdminService> logger,
            ApplicationContext context,
            AuthenticationService authenticationService,
            NotificationService notificationService)
            : base(configuration, logger, context)
        {
            _authenticationService = authenticationService;
            _notificationService = notificationService;
        }

        public async Task<IEnumerable<UserViewModel>> GetUsers()
        {
            List<User> users = await _context.Users.ToListAsync();
            return users.Select(user =>
            {
                User targetUser = user.TargetUserId.HasValue
                    ? users.SingleOrDefault(x => x.Id == user.TargetUserId.Value)
                    : null;
                TargetUserInfo targetUserInfo = user.TargetUserId.HasValue && targetUser != null
                    ? new TargetUserInfo(targetUser) 
                    : null;
                return new UserViewModel
                {
                    Id = user.UserKey,
                    Name = user.Name,
                    Email = user.Email,
                    IsAdmin = user.IsAdmin,
                    AntiWishes = user.AntiWishes,
                    Wishes = user.Wishes,
                    Status = user.Status,
                    TargetUserInfo = targetUserInfo,
                    TargetUserStatus = user.TargetUserStatus,
                    Color = user.Color,
                    ImgUrl = user.ImgUrl,
                };
            }).ToList();
        }

        public async Task<User> UpdateUser(string userKey, AdminUpdateUserViewModel model)
        {
            User user = await _context.Users.SingleOrDefaultAsync(x => x.UserKey == userKey);
            if (user == null)
            {
                throw new InputException(ExceptionsResources.UserUpdate_Validation_NotFound);
            }
            if (model.Wishes != null && model.Wishes.Length > MaxWishesLength)
            {
                throw new InputException(string.Format(
                    ExceptionsResources.UserUpdate_Validation_ExceedingLengthWishes,
                    MaxWishesLength));
            }
            if (model.AntiWishes != null && model.AntiWishes.Length > MaxWishesLength)
            {
                throw new InputException(string.Format(
                    ExceptionsResources.UserUpdate_Validation_ExceedingLengthAntiWishes,
                    MaxWishesLength));
            }
            if (string.IsNullOrWhiteSpace(model.AntiWishes) && string.IsNullOrWhiteSpace(model.Wishes))
            {
                throw new InputException(ExceptionsResources.UserUpdate_Validation_AtLeastOneWishShouldBeIndicated);
            }
            if (TossService.TossIsMaked 
                ? model.TargetUserStatus == TargetUserStatus.Undefined
                : model.TargetUserStatus != TargetUserStatus.Undefined)
            {
                throw new InputException(ExceptionsResources.UserUpdate_Validation_InvalidTargetUserStatus);
            }
            if (user.IsAdmin != model.IsAdmin && _authenticationService.CheckIsAbsoluteAdmin(user))
            {
                throw new InputException(ExceptionsResources.UserUpdate_Validation_CannotChangePermissions);
            }
            bool isAdminChanged = user.IsAdmin != model.IsAdmin;
            bool antiWishesChanged = user.AntiWishes != model.AntiWishes;
            bool wishesChanged = user.Wishes != model.Wishes;
            bool statusChanged = user.Status != model.Status;
            bool targetUserStatusChanged = user.TargetUserStatus != model.TargetUserStatus;

            user.IsAdmin = model.IsAdmin;
            user.AntiWishes = model.AntiWishes;
            user.Wishes = model.Wishes;
            user.Status = model.Status;
            user.TargetUserStatus = model.TargetUserStatus;

            await _context.SaveChangesAsync(true);

            await NotifyUpdateUser(user, isAdminChanged, antiWishesChanged, wishesChanged, 
                statusChanged, targetUserStatusChanged);
            return user;
        }

        private async Task NotifyUpdateUser(
            User user, 
            bool isAdminChanged,
            bool antiWishesChanged,
            bool wishesChanged,
            bool statusChanged,
            bool targetUserStatusChanged)
        {
            var notifications = new List<NotificationInfo>();
            if (isAdminChanged)
            {
                NotifyUserSetAdmin(notifications, user);
            }
            NotifyUserSetWishesAndAntiwishes(notifications, user, wishesChanged, antiWishesChanged);
            if (statusChanged)
            {
                NotifyUserSetStatus(notifications, user);
            }
            if (targetUserStatusChanged)
            {
                NotifyUserSetTargetStatus(notifications, user);
            }
            await _notificationService.AddNotificationsForUser(user.Id, notifications);
        }

        private void NotifyUserSetAdmin(List<NotificationInfo> notifications, User user)
        {
            if (user.IsAdmin)
            {
                notifications.Add(new NotificationInfo(
                    Notifications.AdminService_User_SetAdmin_Title,
                    Notifications.AdminService_User_SetAdmin_Message));
            }
            else
            {
                notifications.Add(new NotificationInfo(
                    Notifications.AdminService_User_UnsetAdmin_Title,
                    Notifications.AdminService_User_UnsetAdmin_Message));
            }
        }

        private void NotifyUserSetWishesAndAntiwishes(
            List<NotificationInfo> notifications, User user, bool wishesChanged, bool antiWishesChanged)
        {
            if (antiWishesChanged && wishesChanged)
            {
                notifications.Add(new NotificationInfo(
                    Notifications.AdminService_User_WishesAndAntiWishes_Title,
                    string.Format(
                        Notifications.AdminService_User_WishesAndAntiWishes_Message,
                        user.Wishes, user.AntiWishes
                    )));
            }
            else
            {
                if (antiWishesChanged)
                {
                    notifications.Add(new NotificationInfo(
                        Notifications.AdminService_User_AntiWishes_Title,
                        string.Format(
                            Notifications.AdminService_User_AntiWishes_Message,
                            user.AntiWishes
                        )));
                }
                if (wishesChanged)
                {
                    notifications.Add(new NotificationInfo(
                        Notifications.AdminService_User_Wishes_Title,
                        string.Format(
                            Notifications.AdminService_User_Wishes_Message,
                            user.Wishes
                        )));
                }
            }
        }

        private void NotifyUserSetStatus(List<NotificationInfo> notifications, User user)
        {
            string statusMessage = UserStatusNotificationMessages[user.Status];
            notifications.Add(new NotificationInfo(
                Notifications.AdminService_User_Status_Title,
                $"{Notifications.AdminService_User_Status_MainMessage} {statusMessage}"));
        }

        private void NotifyUserSetTargetStatus(List<NotificationInfo> notifications, User user)
        {
            string statusMessage = TargetUserStatusNotificationMessages[user.TargetUserStatus];
            notifications.Add(new NotificationInfo(
                Notifications.AdminService_User_TargetStatus_Title,
                $"{Notifications.AdminService_User_TargetStatus_MainMessage} {statusMessage}"));
        }
    }
}
