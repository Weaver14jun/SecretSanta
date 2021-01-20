using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartAnalytics.SecretSanta.Data.Core.Enums;
using SmartAnalytics.SecretSanta.Data.Core.Models;
using SmartAnalytics.SecretSanta.Resources.Notifications;
using ExceptionsResources = SmartAnalytics.SecretSanta.Resources.Exceptions.Exceptions;
using SmartAnalytics.SecretSanta.Services.Exceptions;
using SmartAnalytics.SecretSanta.Services.Models;
using SmartAnalytics.SecretSanta.Services.Services.Base;
using SmartAnalytics.SecretSanta.Services.ViewModels;

namespace SmartAnalytics.SecretSanta.Services.Services
{
    public class UserService : BaseApplicationContextService<UserService>
    {
        private const int MaxWishesLength = 200;

        private readonly NotificationService _notificationService;

        private static readonly Dictionary<UserStatus, string> UserStatusNotificationMessages =
            new Dictionary<UserStatus, string>
            {
                { UserStatus.Involved, Notifications.UserService_Status_MessageInvolved },
                { UserStatus.Refused, Notifications.UserService_Status_MessageRefused },
            };

        private static readonly Dictionary<TargetUserStatus, string> TargetUserStatusNotificationMessages =
            new Dictionary<TargetUserStatus, string>
            {
                {
                    TargetUserStatus.GiftInformationViewed,
                    Notifications.UserService_TargetStatus_MessageGiftInformationViewed
                },
                {
                    TargetUserStatus.GiftIsReady,
                    Notifications.UserService_TargetStatus_MessageGiftIsReady
                },
            };

        public UserService(
            IConfiguration configuration,
            ILogger<UserService> logger,
            ApplicationContext context,
            NotificationService notificationService)
            : base(configuration, logger, context)
        {
            _notificationService = notificationService;
        }

        public async Task<UserViewModel> GetUserInfo(string userKey)
        {
            User user = await _context.Users.SingleAsync(x => x.UserKey.Contains(userKey));
            User targetUser = user.TargetUserId.HasValue
                ? await _context.Users.SingleOrDefaultAsync(x => x.Id == user.TargetUserId.Value)
                : null;

            return new UserViewModel
            {
                Name = user.Name,
                Email = user.Email,
                Status = user.Status,
                AntiWishes = user.AntiWishes,
                IsAdmin = user.IsAdmin,
                TargetUserInfo = targetUser != null ? new TargetUserInfo(targetUser) : null,
                TargetUserStatus = user.TargetUserStatus,
                Id = user.UserKey,
                Wishes = user.Wishes,
                Color = user.Color,
                ImgUrl = user.ImgUrl,
            };
        }

        public async Task<User> UpdateUser(UpdateUserViewModel model, User user)
        {
            if (TossService.TossIsMaked && (
                model.AntiWishes != user.AntiWishes ||
                model.Wishes != user.Wishes ||
                model.Status != user.Status))
            {
                throw new Exception(ExceptionsResources.UserUpdate_Validation_CannotChangeInfoAfretToss);
            }
            if (user.Status != UserStatus.ExpectedToChoose)
            {
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
                if (model.Status == UserStatus.ExpectedToChoose)
                {
                    throw new InputException(ExceptionsResources.UserUpdate_Validation_InvalidUserStatus);
                }
            }
            if (model.TargetUserStatus != user.TargetUserStatus &&
                model.TargetUserStatus != TargetUserStatus.GiftInformationViewed &&
                model.TargetUserStatus != TargetUserStatus.GiftIsReady)
            {
                throw new InputException(ExceptionsResources.UserUpdate_Validation_InvalidTargetUserStatus);
            }

            bool antiWishesChanged = user.AntiWishes != model.AntiWishes;
            bool wishesChanged = user.Wishes != model.Wishes;
            bool statusChanged = user.Status != model.Status;
            bool targetUserStatusChanged = user.TargetUserStatus != model.TargetUserStatus;

            user.AntiWishes = model.AntiWishes;
            user.Wishes = model.Wishes;
            user.Status = model.Status;
            user.TargetUserStatus = model.TargetUserStatus;

            await _context.SaveChangesAsync(true);
            await NotifyUpdateUser(user, antiWishesChanged, 
                wishesChanged, statusChanged, targetUserStatusChanged);

            return user;
        }

        private async Task NotifyUpdateUser(
            User user,
            bool antiWishesChanged,
            bool wishesChanged,
            bool statusChanged,
            bool targetUserStatusChanged)
        {
            var notifications = new List<NotificationInfo>();
            NotifySetWishesAndAntiwishes(notifications, user, wishesChanged, antiWishesChanged);
            if (statusChanged)
            {
                NotifySetStatus(notifications, user);
            }
            if (targetUserStatusChanged)
            {
                await NotifySetTargetStatus(notifications, user);
            }
            await _notificationService.AddNotificationsForUser(user.Id, notifications);
        }

        private void NotifySetWishesAndAntiwishes(
            List<NotificationInfo> notifications, User user, bool wishesChanged, bool antiWishesChanged)
        {
            if (antiWishesChanged && wishesChanged)
            {
                notifications.Add(new NotificationInfo(
                    Notifications.UserService_WishesAndAntiWishes_Title,
                    string.Format(
                        Notifications.UserService_WishesAndAntiWishes_Message,
                        user.Wishes, user.AntiWishes
                    )));
            }
            else
            {
                if (antiWishesChanged)
                {
                    notifications.Add(new NotificationInfo(
                        Notifications.UserService_AntiWishes_Title,
                        string.Format(
                            Notifications.UserService_AntiWishes_Message,
                            user.AntiWishes
                        )));
                }
                if (wishesChanged)
                {
                    notifications.Add(new NotificationInfo(
                        Notifications.UserService_Wishes_Title,
                        string.Format(
                            Notifications.UserService_Wishes_Message,
                            user.Wishes
                        )));
                }
            }
        }

        private void NotifySetStatus(List<NotificationInfo> notifications, User user)
        {
            string statusMessage = UserStatusNotificationMessages[user.Status];
            notifications.Add(new NotificationInfo(
                Notifications.UserService_Status_Title,
                $"{Notifications.UserService_Status_MainMessage} {statusMessage}"));
        }

        private async Task NotifySetTargetStatus(List<NotificationInfo> notifications, User user)
        {
            string statusMessage = TargetUserStatusNotificationMessages[user.TargetUserStatus];
            notifications.Add(new NotificationInfo(
                Notifications.UserService_TargetStatus_Title,
                $"{Notifications.UserService_TargetStatus_MainMessage} {statusMessage}"));

            if (user.TargetUserId.HasValue && user.TargetUserStatus == TargetUserStatus.GiftIsReady)
            {
                User targetUser = await _context.Users.SingleAsync(x => x.Id == user.TargetUserId.Value);
                if (targetUser.TargetUserStatus == TargetUserStatus.GiftIsReady)
                {
                    await _notificationService.AddNotificationForUser(targetUser.Id, new NotificationInfo(
                        Notifications.UserService_GiftIsReadyToTargetReadyUser_Title,
                        Notifications.UserService_GiftIsReadyToTargetReadyUser_Message));
                }
                else
                {
                    await _notificationService.AddNotificationForUser(targetUser.Id, new NotificationInfo(
                        Notifications.UserService_GiftIsReadyToTargetNotReadyUser_Title,
                        Notifications.UserService_GiftIsReadyToTargetNotReadyUser_Message));
                }
            }
        }
    }
}
