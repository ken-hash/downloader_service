using System;
using System.Text;
using System.Threading.Tasks;
using downloader_service.Models;
using localscrape.Helpers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace downloader_service.Repo
{
    public interface IRabbitRepo : IDisposable
    {
        Task StartConsuming();
    }

    public class RabbitRepo : IRabbitRepo
    {
        private IConnection _connection;
        private IChannel _channel;
        private const string QUEUE_NAME = "download_queue";
        private readonly IEnvHelper _envHelper;
        private readonly Action<DownloadObject> _processDownload;
        private readonly ILogger _logger;

        public RabbitRepo(
            IEnvHelper envHelper,
            Action<DownloadObject> processDownload,
            ILogger logger)
        {
            _envHelper = envHelper ?? throw new ArgumentNullException(nameof(envHelper));
            _processDownload = processDownload ?? throw new ArgumentNullException(nameof(processDownload));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            InitialiseRabbitMQ();
        }

        private async void InitialiseRabbitMQ()
        {
            try
            {
                var rabbitMQHost = _envHelper.GetEnvValue("rabbitMQHost");
                var rabbitUser = _envHelper.GetEnvValue("rabbitMQUser");
                var rabbitPass = _envHelper.GetEnvValue("rabbitMQPassword");

                _logger.LogInformation($"Initializing RabbitMQ connection to {rabbitMQHost}");

                var factory = new ConnectionFactory
                {
                    HostName = rabbitMQHost,
                    UserName = rabbitUser,
                    Password = rabbitPass,
                    AutomaticRecoveryEnabled = true,
                    TopologyRecoveryEnabled = true,
                    RequestedFrameMax = 1073741824,
                    MaxInboundMessageBodySize = 1073741824
                };

                _connection = await factory.CreateConnectionAsync();
                _channel = _connection.CreateChannelAsync().Result;

                await _channel.QueueDeclareAsync(
                    queue: QUEUE_NAME,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);
                await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

                _logger.LogInformation("RabbitMQ initialization complete. Queue declared and QoS set.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize RabbitMQ");
                throw;
            }
        }

        public async Task StartConsuming()
        {
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (sender, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var json = Encoding.UTF8.GetString(body);
                    _logger.LogInformation($"Received message: {json}");

                    var download = JsonConvert.DeserializeObject<DownloadObject>(json)
                                   ?? throw new Exception("Deserialized to null");

                    _processDownload(download);
                    await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                    _logger.LogInformation($"Message processed and acknowledged: {ea.DeliveryTag}");
                }
                catch (JsonException jex)
                {
                    _logger.LogError(jex, "Invalid JSON message, rejecting");
                    await _channel.BasicRejectAsync(ea.DeliveryTag, requeue: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Processing failed, NACK and requeue");
                    await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                }
            };

            await _channel.BasicConsumeAsync(QUEUE_NAME, autoAck: false, consumer: consumer);

            _logger.LogInformation("[*] Consumer started, waiting for messages.");
        }

        public async void Dispose()
        {
            try
            {
                await _channel?.CloseAsync();
                await _connection?.CloseAsync();
                _logger.LogInformation("RabbitMQ connection and channel closed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing RabbitMQ connection/channel");
            }
        }
    }
}
