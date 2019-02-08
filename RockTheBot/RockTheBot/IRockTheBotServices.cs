using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ready19.RockTheBot
{
    public interface IRockTheBotServices
    {
        Task<string> GetStocksAsync();

        Task<string> GetWeatherAsync(string location = null);
    }

    public class RockTheBotServices : IRockTheBotServices
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _config;

        public RockTheBotServices(ILoggerFactory loggerFactory, IConfiguration config)
        {
            if (loggerFactory == null)
            {
                throw new System.ArgumentNullException(nameof(loggerFactory));
            }

            _logger = loggerFactory.CreateLogger<RockTheBotServices>();

            _config = config ?? throw new System.ArgumentNullException(nameof(config));
        }

        public async Task<string> GetStocksAsync()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    return await client.GetStringAsync(_config.GetSection("stockServiceUrl").Value);
                }
            }
            catch (System.Exception)
            {
                // _logger.LogError($"GetStocksAsync failed with {e.ToString()}");
                return "<An error occurred retrieving the stock value>";
            }
        }

        public async Task<string> GetWeatherAsync(string location = null)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string url = _config.GetSection("weatherServiceUrl").Value;
                    if (!string.IsNullOrEmpty(location))
                    {
                        url += "/" + location;
                    }

                    return await client.GetStringAsync(url);
                }
            }
            catch (System.Exception)
            {
                // _logger.LogError($"GetWeatherAsync failed with {e.ToString()}");
                return "<An error occurred retrieving the stock value>";
            }
        }
    }
}