using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartAnalytics.SecretSanta.Services.Services.Base;
using SmartAnalytics.SecretSanta.Data.Core.Models;
using System.Linq;
using System;
using SmartAnalytics.SecretSanta.Services.ViewModels;
using SmartAnalytics.SecretSanta.Data.Core.Enums;

namespace SmartAnalytics.SecretSanta.Services.Services
{
    public class ApplicationService : BaseApplicationContextService<ApplicationService>
    {
        private const string ApplicationSettingsSection = "Application";
        private const string SupportEmailSection = "SupportEmail";
        private const string TeamNameSection = "TeamName";
        private const string DeadlineTossSection = "DeadlineToss";
        private const string GiftPreparationDeadlineSection = "GiftPreparationDeadline";
        private const string RecommendedPriceSection = "RecommendedPrice";
        private const string WishesBadLimitSection = "WishesBadPercentsLimit";
        private const string WishesNormLimitSection = "WishesNormPercentsLimit";
        private const string CongratulationsLocationTextSection = "CongratulationsLocationText";

        public static bool NeedSetDeadlines { get; private set; } = true;
        public static DateTime DeadlineToss { get; private set; }
        public static DateTime GiftPreparationDeadline { get; private set; }
        public static string TeamName { get; private set; }
        public static string CongratulationsLocationText { get; private set; }


        private readonly StoryDescriptionService _storyDescriptionService;

        public ApplicationService(
            IConfiguration configuration,
            ILogger<ApplicationService> logger,
            ApplicationContext context,
            StoryDescriptionService storyDescriptionService)
            : base(configuration, logger, context)
        {
            _storyDescriptionService = storyDescriptionService;
            if (NeedSetDeadlines)
            {
                SetDeadlines();
                TeamName = GetStringFromConfig(TeamNameSection);
                CongratulationsLocationText = GetStringFromConfig(CongratulationsLocationTextSection);
                NeedSetDeadlines = false;
            }
        }

        public ApplicationDataViewModel GetApplicationData()
        {
            var appData = new ApplicationDataViewModel {
                ParticipantsCount = GetParticipantsCount(),
                SupportEmail = GetStringFromConfig(SupportEmailSection),
                TeamName = TeamName,
                StoryDescription = _storyDescriptionService.Text,
                RecommendedPrice = GetIntFromConfig(RecommendedPriceSection),
                WishesBadLimit = GetIntFromConfig(WishesBadLimitSection),
                WishesNormLimit = GetIntFromConfig(WishesNormLimitSection),
                TossIsMaked = TossService.TossIsMaked,
                DeadlineToss = DeadlineToss,
                GiftPreparationDeadline = GiftPreparationDeadline,
            };

            return appData;
        }

        protected override IConfigurationSection GetFromConfig(string sectionName)
        {
            return base.GetFromConfig($"{ApplicationSettingsSection}:{sectionName}");
        }

        private void SetDeadlines()
        {
            string deadlineToss = GetStringFromConfig(DeadlineTossSection);
            if (!string.IsNullOrWhiteSpace(deadlineToss))
            {
                DeadlineToss = Convert.ToDateTime(deadlineToss);
            }
            string giftPreparationDeadline = GetStringFromConfig(GiftPreparationDeadlineSection);
            if (!string.IsNullOrWhiteSpace(giftPreparationDeadline))
            {
                GiftPreparationDeadline = Convert.ToDateTime(giftPreparationDeadline);
            }
        }

        private int GetParticipantsCount()
        {
            return _context.Users
                .Where(x => x.Status == UserStatus.Involved)
                .Count();
        }
    }
}
