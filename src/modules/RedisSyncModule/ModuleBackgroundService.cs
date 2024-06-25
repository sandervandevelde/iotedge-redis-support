using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.Text;

namespace RedisSyncModule;

internal class ModuleBackgroundService : BackgroundService
{
    private static string DefaultEndpoint = string.Empty;
    private static string Endpoint { get; set; } = DefaultEndpoint;

    private ModuleClient? _moduleClient;
    private CancellationToken _cancellationToken;
    private readonly ILogger<ModuleBackgroundService> _logger;

    public ModuleBackgroundService(ILogger<ModuleBackgroundService> logger) => _logger = logger;

    private ConnectionMultiplexer _redis = null;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("***********************************");
        _logger.LogInformation("***********************************");
        _logger.LogInformation("Copyrights 2024 svelde - Redis sync");
        _logger.LogInformation("***********************************");
        _logger.LogInformation("***********************************");

        _cancellationToken = cancellationToken;
        MqttTransportSettings mqttSetting = new(TransportType.Mqtt_Tcp_Only);
        ITransportSettings[] settings = { mqttSetting };

        // Open a connection to the Edge runtime
        _moduleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);

        // Reconnect is not implented because we'll let docker restart the process when the connection is lost
        _moduleClient.SetConnectionStatusChangesHandler((status, reason) => 
            _logger.LogWarning("Connection changed: Status: {status} Reason: {reason}", status, reason));

        // Attach callback for Twin desired properties updates
        await _moduleClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertiesUpdate, _moduleClient);

        await _moduleClient.OpenAsync(cancellationToken);

        _logger.LogInformation("IoT Hub module client initialized.");

        // Execute callback method for Twin desired properties updates. Function will retrieve the actual twin collection.
        await onDesiredPropertiesUpdate(new TwinCollection(), _moduleClient);
    }

    private async Task onDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
    {
        var twin = await _moduleClient.GetTwinAsync();
        desiredProperties = twin.Properties.Desired;

        if (desiredProperties.Count == 0)
        {
            _logger.LogInformation($"{DateTime.UtcNow} - Empty desired properties ignored.");

            return;
        }

        try
        {
            _logger.LogInformation($"{DateTime.UtcNow} Desired properties received:");
            _logger.LogInformation(JsonConvert.SerializeObject(desiredProperties));

            var client = userContext as ModuleClient;

            if (client == null)
            {
                throw new InvalidOperationException($"UserContext doesn't contain expected ModuleClient");
            }

            var reportedProperties = new TwinCollection();

            if (desiredProperties.Contains("endpoint")) 
            {
                if (desiredProperties.Contains("endpoint") && desiredProperties["endpoint"] != null)
                {
                    Endpoint = desiredProperties["endpoint"];
                }               
                else
                {
                    Endpoint = DefaultEndpoint;
                }

                _logger.LogInformation($"{DateTime.UtcNow} - Endpoint changed to {Endpoint}");

                reportedProperties["endpoint"] = Endpoint;
            }
            else
            {
                _logger.LogInformation($"{DateTime.UtcNow} - Endpoint ignored");
            }

            if (reportedProperties.Count > 0)
            {
                await client.UpdateReportedPropertiesAsync(reportedProperties);
            }

            //// Close the connection to the Redis server

            if (_redis != null)
            {
                _redis.Close();
                _redis.Dispose();
                _redis = null;

                _logger.LogInformation("Redis endpoint is closed");
            }

            //// Open a connection to the Redis server

            if (!string.IsNullOrEmpty(Endpoint))
            {
                _redis = ConnectionMultiplexer.Connect(
                    new ConfigurationOptions
                    {
                        EndPoints = { Endpoint }
                    });

                _logger.LogInformation("Redis endpoint is connected");
            }
            else
            {
                _logger.LogInformation("Desired Redis endpoint property is empty");
            }

            //// Update All Redis keys
            

        }
        catch (AggregateException ex)
        {
            _logger.LogInformation($"{DateTime.UtcNow} - Desired properties change error: {ex.Message}");
            
            foreach (Exception exception in ex.InnerExceptions)
            {
                _logger.LogInformation($"{DateTime.UtcNow} - Error when receiving desired properties: {exception}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation($"{DateTime.UtcNow} - Error when receiving desired properties: {ex.Message}");
        }
    }
}
