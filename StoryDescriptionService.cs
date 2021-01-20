using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartAnalytics.SecretSanta.Services.Services.Base;
using System.IO;
using MarkdownSharp;
using Microsoft.Extensions.DependencyInjection;

namespace SmartAnalytics.SecretSanta.Services.Services
{
    public class StoryDescriptionService : BaseSingletonService<StoryDescriptionService>
    {
        private const string StoryDescriptionPathSection = "StoryDescriptionFile";
        public string Text { get; private set; }

        public StoryDescriptionService(
            IConfiguration configuration,
            ILogger<StoryDescriptionService> logger,
            IServiceScopeFactory scopeFactory)
            : base(configuration, logger, scopeFactory)
        {
            Text = ReadMarkdownFile();
        }
        public string ReadMarkdownFile()
        {
            string filePath = GetStringFromConfig(StoryDescriptionPathSection);
            if (File.Exists(filePath))
            {
                using(StreamReader reader = new StreamReader(filePath))
                {
                    string storyDesc = reader.ReadToEnd();
                    Markdown markdown = new Markdown();
                    try
                    {
                        markdown.Transform(storyDesc);
                        return storyDesc;
                    }
                    catch
                    {
                        _logger.LogError(string.Format("Ошибка парсинга markdown файла: {0}", filePath));
                    }
                }
            }
            return null;
        }
        protected override void CheckServices()
        {
        }
    }
}
