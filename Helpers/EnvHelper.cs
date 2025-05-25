using dotenv.net;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
namespace localscrape.Helpers
{
    public interface IEnvHelper
    {
        string GetEnvValue(string Key);
    }

    public class DotEnvHelper : IEnvHelper
    {
        private readonly ILogger _logger;
        public DotEnvHelper(DotEnvOptions options, ILogger logger)
        {
            _ = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            DotEnv.Load(options);
            _logger.LogInformation($"Initialising env variables based on {options.EnvFilePaths.First()}");
        }
        public DotEnvHelper(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            DotEnv.Load();
            _logger.LogInformation($"Initialising env variables based on default values");
        }

        public string GetEnvValue(string Key)
        {
            _logger.LogInformation($"Getting env variables {Key}");
            if (Environment.GetEnvironmentVariable(Key) is null)
                _logger.LogWarning($"env variable for {Key} is empty");
            return Environment.GetEnvironmentVariable(Key) ?? string.Empty;
        }
    }
}