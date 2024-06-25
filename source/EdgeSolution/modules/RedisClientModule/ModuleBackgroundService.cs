using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.Text;

namespace RedisClientModule;

internal class ModuleBackgroundService : BackgroundService
{
    private static string DefaultEndpoint = string.Empty;
    private static string Endpoint { get; set; } = DefaultEndpoint;

    private ModuleClient? _moduleClient;
    private CancellationToken _cancellationToken;
    private readonly ILogger<ModuleBackgroundService> _logger;

    private ConnectionMultiplexer _redis = null;

    public ModuleBackgroundService(ILogger<ModuleBackgroundService> logger) => _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("*************************************");
        _logger.LogInformation("*************************************");
        _logger.LogInformation("Copyrights 2024 svelde - Redis client");
        _logger.LogInformation("*************************************");
        _logger.LogInformation("*************************************");

        _cancellationToken = cancellationToken;
        MqttTransportSettings mqttSetting = new(TransportType.Mqtt_Tcp_Only);
        ITransportSettings[] settings = { mqttSetting };

        // Open a connection to the Edge runtime
        _moduleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);

        // Reconnect is not implented because we'll let docker restart the process when the connection is lost
        _moduleClient.SetConnectionStatusChangesHandler((status, reason) => 
            _logger.LogWarning("ModuleClient connection changed: Status: {status} Reason: {reason}", status, reason));

        // Create a handler for the direct method call
        await _moduleClient.SetMethodHandlerAsync("GetItem", GetItem, _moduleClient);
        await _moduleClient.SetMethodHandlerAsync("SetItem", SetItem, _moduleClient);

        _logger.LogInformation($"Module Input route 'GetItem' is attached.");
        _logger.LogInformation($"Module Output route 'SetItem' is reserved.");

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

    private Task<MethodResponse> GetItem(MethodRequest methodRequest, object userContext)
    {
        DirectMethodResponse response;

        try
        {   
            var requestText = Encoding.UTF8.GetString(methodRequest.Data);

            // remove Direct Method string quotation 
            requestText = requestText.Replace("\"", "");

            _logger.LogInformation($"{DateTime.Now}: Direct method 'GetItem' called with request '{requestText}'.");

            if (_redis != null)
            {
                IDatabase db = _redis.GetDatabase();

                var redisResponse = db.StringGet(requestText);

                if (!redisResponse.IsNullOrEmpty)
                {
                    response = new DirectMethodResponse{Message = $"{redisResponse}", Status = 200 };

                    _logger.LogInformation(response.Message);
                }
                else
                {
                    response = new DirectMethodResponse{ Message = "No value available in Redis", Status = 500 };

                    _logger.LogInformation(response.Message);
                }
            }
            else
            {
                response = new DirectMethodResponse{ Message = "No connection to Redis server available", Status = 500 };

                _logger.LogInformation(response.Message);
            }
        }
        catch (Exception ex)
        {
            response = new DirectMethodResponse{ Message = $"GetItem error: {ex.Message}", Status = 500 };

            _logger.LogInformation(response.Message);
        }

        var responseJson = JsonConvert.SerializeObject(response);

        var methodSesponse = new MethodResponse(Encoding.UTF8.GetBytes(responseJson), response.Status);

        return Task.FromResult(methodSesponse);
    }

    private Task<MethodResponse> SetItem(MethodRequest methodRequest, object userContext)
    {
        DirectMethodResponse response;

        try
        {
            var requestText = Encoding.UTF8.GetString(methodRequest.Data);

            // remove Direct Method string quotation 
            requestText = requestText.Replace("\"", "");

            _logger.LogInformation($"{DateTime.Now}: Direct method 'SetItem' called with request '{requestText}'.");

            if (_redis != null)
            {
                IDatabase db = _redis.GetDatabase();

                var split = requestText.Split(':'); 

                var redisResponse = db.StringSet(split[0], split[1]);

                response = new DirectMethodResponse{ Message = $"Written:{redisResponse} => key = '{split[0]}', value = '{split[1]}'", Status = 200 };

                _logger.LogInformation(response.Message);
            }
            else
            {
                response = new DirectMethodResponse{ Message = "No connection to Redis server", Status = 500 };

                _logger.LogInformation(response.Message);
            }
        }
        catch (Exception ex)
        {
            response = new DirectMethodResponse{ Message = $"SetItem error: {ex.Message}", Status = 500 };

            _logger.LogInformation(response.Message);
        }

        var responseJson = JsonConvert.SerializeObject(response);

        var methodSesponse = new MethodResponse(Encoding.UTF8.GetBytes(responseJson), response.Status);

        return Task.FromResult(methodSesponse);
    }
}
