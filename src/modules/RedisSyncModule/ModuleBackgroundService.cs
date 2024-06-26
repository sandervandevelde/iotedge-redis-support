using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.Text;

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace RedisSyncModule;

internal class ModuleBackgroundService : BackgroundService
{
    private string Endpoint { get; set; } = DefaultEndpoint;
    private string StorageAccountName { get; set; } = DefaultStorageAccountName;
    private string BlobContainerName { get; set; } = DefaultBlobContainerName;
    private string BlobFileName { get; set; } = DefaultBlobFileName;
    private string BlobSasToken { get; set; } = DefaultBlobSasToken;

    private static string DefaultEndpoint = string.Empty;
    private static string DefaultStorageAccountName = string.Empty;
    private static string DefaultBlobContainerName = string.Empty;
    private static string DefaultBlobFileName = string.Empty;
    private static string DefaultBlobSasToken = string.Empty;


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
            _logger.LogWarning($"{DateTime.UtcNow} - Connection changed: Status: {status} Reason: {reason}"));

        // Attach callback for Twin desired properties updates
        await _moduleClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertiesUpdate, _moduleClient);

        await _moduleClient.OpenAsync(cancellationToken);

        _logger.LogInformation($"{DateTime.UtcNow} - IoT Hub module client initialized.");

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

            // endpoint

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

            // StorageAccountName - storageAccountName 

            if (desiredProperties.Contains("storageAccountName")) 
            {
                if (desiredProperties.Contains("storageAccountName") && desiredProperties["storageAccountName"] != null)
                {
                    StorageAccountName = desiredProperties["storageAccountName"];
                }               
                else
                {
                    StorageAccountName = DefaultStorageAccountName;
                }

                _logger.LogInformation($"{DateTime.UtcNow} - StorageAccountName changed to {StorageAccountName}");

                reportedProperties["storageAccountName"] = StorageAccountName;
            }
            else
            {
                _logger.LogInformation($"{DateTime.UtcNow} - StorageAccountName ignored");
            }

            // BlobContainerName - blobContainerName 

            if (desiredProperties.Contains("blobContainerName")) 
            {
                if (desiredProperties.Contains("blobContainerName") && desiredProperties["blobContainerName"] != null)
                {
                    BlobContainerName = desiredProperties["blobContainerName"];
                }               
                else
                {
                    BlobContainerName = DefaultBlobContainerName;
                }

                _logger.LogInformation($"{DateTime.UtcNow} - BlobContainerName changed to {BlobContainerName}");

                reportedProperties["blobContainerName"] = BlobContainerName;
            }
            else
            {
                _logger.LogInformation($"{DateTime.UtcNow} - BlobContainerName ignored");
            }

            // BlobFileName - blobFileName 

            if (desiredProperties.Contains("blobFileName")) 
            {
                if (desiredProperties.Contains("blobFileName") && desiredProperties["blobFileName"] != null)
                {
                    BlobFileName = desiredProperties["blobFileName"];
                }               
                else
                {
                    BlobFileName = DefaultBlobFileName;
                }

                _logger.LogInformation($"{DateTime.UtcNow} - BlobFileName changed to {BlobFileName}");

                reportedProperties["blobFileName"] = BlobFileName;
            }
            else
            {
                _logger.LogInformation($"{DateTime.UtcNow} - BlobFileName ignored");
            }

            // BlobSasToken - blobSasToken 

            if (desiredProperties.Contains("blobSasToken")) 
            {
                if (desiredProperties.Contains("blobSasToken") && desiredProperties["blobSasToken"] != null)
                {
                    BlobSasToken = desiredProperties["blobSasToken"];
                }               
                else
                {
                    BlobSasToken = DefaultBlobSasToken;
                }

                _logger.LogInformation($"{DateTime.UtcNow} - BlobSasToken changed to {BlobSasToken}");

                reportedProperties["blobSasToken"] = BlobSasToken;
            }
            else
            {
                _logger.LogInformation($"{DateTime.UtcNow} - BlobSasToken ignored");
            }

            if (Endpoint != DefaultEndpoint)
            {
                ReconnectRedis();
            }
            else
            {
                _logger.LogInformation($"{DateTime.UtcNow} - No Redis endpoint to open.");
            }

            if (BlobSasToken != DefaultBlobSasToken
                && StorageAccountName != DefaultStorageAccountName
                && BlobContainerName != DefaultBlobContainerName
                && BlobFileName != DefaultBlobFileName)
            {
                await UpdateRedis();
            }
            else
            {
                _logger.LogInformation($"{DateTime.UtcNow} - No blob file to access.");
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

    private async Task UpdateRedis()
    {
        //// Create a credential object from the SAS token then use this and the account name to create a cloud storage connection

        _logger.LogInformation($"{DateTime.UtcNow} - Access the blob reference using the SAS token");

        var accountSAS = new StorageCredentials(BlobSasToken);
        var storageAccount = new CloudStorageAccount(accountSAS, StorageAccountName, null, true);
        var blobClient = storageAccount.CreateCloudBlobClient();
        var containerReference = blobClient.GetContainerReference(BlobContainerName);
        var blobReference = containerReference.GetBlobReference(BlobFileName);

        //// Download the blob to a string
        
        _logger.LogInformation($"{DateTime.UtcNow} - Download the blob from the storage account");

        using var stream = new MemoryStream();
        await blobReference.DownloadToStreamAsync(stream);
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var multilineText =  reader.ReadToEnd();

        _logger.LogInformation($"{DateTime.UtcNow} - Blob content downloaded: '{multilineText}'");

        //// Split the string into an array of lines
        
        var textArray = multilineText.Split(new[] { System.Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

        _logger.LogInformation($"{DateTime.UtcNow} - {textArray.Length} key/value pairs found");

        //// Update All Redis keys
        
        foreach (var line in textArray)
        {
            var keyValuePair = line.Split(':');
            var key = keyValuePair[0];
            var value = keyValuePair[1];

            var db = _redis.GetDatabase();
            await db.StringSetAsync(key, value);

            _logger.LogInformation($"{DateTime.UtcNow} - Written: {key}:{value}");
        }
        
        _logger.LogInformation($"{DateTime.UtcNow} - {textArray.Length} keys inserted/updated");
    }

    private void ReconnectRedis()
    {
        //// Close the connection to the Redis server

        if (_redis != null)
        {
            _redis.Close();
            _redis.Dispose();
            _redis = null;

            _logger.LogInformation($"{DateTime.UtcNow} - Redis endpoint is closed");
        }

        //// Open a connection to the Redis server

        _redis = ConnectionMultiplexer.Connect(
            new ConfigurationOptions
            {
                EndPoints = { Endpoint }
            });

        _logger.LogInformation($"{DateTime.UtcNow} - Redis endpoint is connected");
    }
}
