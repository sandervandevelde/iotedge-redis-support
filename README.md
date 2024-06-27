# iotedge-redis-support

Demonstration of using Redis cache in Azure IoT Edge

## Blog post

to see this in action, check out this [blog post](https://sandervandevelde.wordpress.com/2024/06/27/redis-cache-integration-in-azure-iot-edge/) for details.

## Modules

This repo contains two modules for working with a Redis service:

1. Redis Client
2. Redis Sync

At the end of this readme, the installation of the Redis service is shown.

### Sample deployment manifest

A sample deployment manifest is added to the repo so you can compare your own deployment manifest with the one here.

### Redis Client

The following desired property is needed to connect to the Redis service: 

```
{
    "endpoint": "192.168.2.6:6379"
}
```

When the module starts or the properties are updated, the server is connected:

```
<6>RedisClientModule.ModuleBackgroundService[0] *************************************
<6>RedisClientModule.ModuleBackgroundService[0] *************************************
<6>RedisClientModule.ModuleBackgroundService[0] Copyrights 2024 svelde - Redis client
<6>RedisClientModule.ModuleBackgroundService[0] *************************************
<6>RedisClientModule.ModuleBackgroundService[0] *************************************
<6>Microsoft.Hosting.Lifetime[0] Application started. Press Ctrl+C to shut down.
<6>Microsoft.Hosting.Lifetime[0] Hosting environment: Production
<6>Microsoft.Hosting.Lifetime[0] Content root path: /app
<4>RedisClientModule.ModuleBackgroundService[0] 06/26/2024 22:52:09 - ModuleClient connection changed: Status: Connected Reason: Connection_Ok
<6>RedisClientModule.ModuleBackgroundService[0] 06/26/2024 22:52:09 - Module Input route 'GetItem' is attached.
<6>RedisClientModule.ModuleBackgroundService[0] 06/26/2024 22:52:09 - Module Output route 'SetItem' is reserved.
<6>RedisClientModule.ModuleBackgroundService[0] 06/26/2024 22:52:10 - IoT Hub module client initialized.
<6>RedisClientModule.ModuleBackgroundService[0] 06/26/2024 22:52:10 Desired properties received:
<6>RedisClientModule.ModuleBackgroundService[0] {"endpoint":"192.168.2.6:6379","$version":20}
<6>RedisClientModule.ModuleBackgroundService[0] 06/26/2024 22:52:10 - Endpoint changed to 192.168.2.6:6379
<6>RedisClientModule.ModuleBackgroundService[0] 06/26/2024 22:52:11 - Redis endpoint is connected
<6>RedisClientModule.ModuleBackgroundService[0] 06/26/2024 22:52:11 - Supported desired properties: endpoint (default '')
<6>RedisClientModule.ModuleBackgroundService[0] 06/26/2024 22:52:54: Direct method 'SetItem' called with request 'marco:polo'.
<6>RedisClientModule.ModuleBackgroundService[0] Written:True => key = 'marco', value = 'polo'
<6>RedisClientModule.ModuleBackgroundService[0] 06/26/2024 22:53:12: Direct method 'GetItem' called with request 'marco'.
<6>RedisClientModule.ModuleBackgroundService[0] polo
<6>RedisClientModule.ModuleBackgroundService[0] 06/26/2024 23:13:41: Direct method 'GetItem' called with request 'key2'.
<6>RedisClientModule.ModuleBackgroundService[0] value2
```
#### Direct Methods

The client module implements two Direct methods:

1. SetItem
2. GetItem

The SetItem expects a body like: 

```
"ping:pong"
```

When executed succesfully and the value is written, the response looks like:

```
{
    "status": 200,
    "payload": {
        "Message": "Written:True => key = 'ping', value = 'pong'",
        "Status": 200
    }
}
```

Any response not having the status 200 must be seen as an error. The message shows the error details.  

The GetItem expects a body like:

```
"ping"
```

When executed succesfully and the value is read, the response looks like:

```
{
    "status": 200,
    "payload": {
        "Message": "pong",
        "Status": 200
    }
}
```

Any response not having the status 200 must be seen as an error or the value is not available. The message shows the error details.  

### Redis Sync

The following desired properties are needed to connect to the Redis service and to read a blob file containing key-value pairs to update: 

```
{
    "endpoint": "192.168.2.6:6379",
    "storageAccountName": "abc",
    "blobContainerName": "redis",
    "blobFileName": "update.csv",
    "blobSasToken": "sp=r&st=2024-06-26T08:45:22Z&se=2024-06-26T16:45:22Z&spr=https&sv=2022-11-02&sr=b&sig=XYZ"
}
```

When the module starts or the properties are updated, the synchronization is executed:

```
<6>RedisSyncModule.ModuleBackgroundService[0] ***********************************
<6>RedisSyncModule.ModuleBackgroundService[0] ***********************************
<6>RedisSyncModule.ModuleBackgroundService[0] Copyrights 2024 svelde - Redis sync
<6>RedisSyncModule.ModuleBackgroundService[0] ***********************************
<6>RedisSyncModule.ModuleBackgroundService[0] ***********************************
<6>Microsoft.Hosting.Lifetime[0] Application started. Press Ctrl+C to shut down.
<6>Microsoft.Hosting.Lifetime[0] Hosting environment: Production
<6>Microsoft.Hosting.Lifetime[0] Content root path: /app
<4>RedisSyncModule.ModuleBackgroundService[0] 06/26/2024 09:16:00 - Connection changed: Status: Connected Reason: Connection_Ok
<6>RedisSyncModule.ModuleBackgroundService[0] 06/26/2024 09:16:00 - IoT Hub module client initialized.
<6>RedisSyncModule.ModuleBackgroundService[0] 06/26/2024 09:16:00 Desired properties received:
<6>RedisSyncModule.ModuleBackgroundService[0] {"endpoint":"192.168.2.6:6379","storageAccountName":"ABC","blobContainerName":"redis","blobFileName":"update.csv","blobSasToken":"sp=r&st=2024-06-26T08:45:22Z&se=2024-06-26T16:45:22Z&spr=https&sv=2022-11-02&sr=b&sig=XYZ","$version":4}
<6>RedisSyncModule.ModuleBackgroundService[0] 06/26/2024 09:16:01 - Endpoint changed to 192.168.2.6:6379
<6>RedisSyncModule.ModuleBackgroundService[0] 06/26/2024 09:16:01 - StorageAccountName changed to ABC
<6>RedisSyncModule.ModuleBackgroundService[0] 06/26/2024 09:16:01 - BlobContainerName changed to redis
<6>RedisSyncModule.ModuleBackgroundService[0] 06/26/2024 09:16:01 - BlobFileName changed to update.csv
<6>RedisSyncModule.ModuleBackgroundService[0] 06/26/2024 09:16:01 - BlobSasToken changed to sp=r&st=2024-06-26T08:45:22Z&se=2024-06-26T16:45:22Z&spr=https&sv=2022-11-02&sr=b&sig=KEY
<6>RedisSyncModule.ModuleBackgroundService[0] 06/26/2024 09:16:01 - Redis endpoint is connected
<6>RedisSyncModule.ModuleBackgroundService[0] 06/26/2024 09:16:01 - Access the blob reference using the SAS token
<6>RedisSyncModule.ModuleBackgroundService[0] 06/26/2024 09:16:01 - Download the blob from the storage account
<6>RedisSyncModule.ModuleBackgroundService[0] 06/26/2024 09:16:02 - Blob content downloaded:
 key3:value3Module.ModuleBackgroundService[0] key1:value1
<6>RedisSyncModule.ModuleBackgroundService[0] 06/26/2024 09:16:02 - 3 key/value pairs found
<6>RedisSyncModule.ModuleBackgroundService[0] 06/26/2024 09:16:02 - Written: key1:value1
<6>RedisSyncModule.ModuleBackgroundService[0] 06/26/2024 09:16:02 - Written: key2:value2
<6>RedisSyncModule.ModuleBackgroundService[0] 06/26/2024 09:16:02 - Written: key3:value3
<6>RedisSyncModule.ModuleBackgroundService[0] 06/26/2024 09:16:02 - 3 keys inserted/updated
<6>RedisSyncModule.ModuleBackgroundService[0] 06/26/2024 09:16:02 - Supported desired properties: endpoint (default ''); storageAccountName (default ''); blobContainerName (default ''); blobFileName (default ''); blobSasToken (default '')
```

## Redis service deployment

We make use of a open-source Redis service, made available as a Docker container:

```
redis:alpine3.20
```

We deploy it with basic container create options to expose the internal port:

````
{
    "ExposedPorts": {
        "6379/tcp": {},
        "8001/tcp": {}
    },
    "HostConfig": {
        "PortBindings": {
            "6379/tcp": [
                {
                    "HostPort": "6379"
                }
            ],
            "8001/tcp": [
                {
                    "HostPort": "8001"
                }
            ]
        }
    }
}
```

*Note*: Redis Insight (on port 8001) is out of scope in this post. I found this option in the Redis documentation but I'm not sure this is part of my Redis.

*Note*: this Redis container will not persist any data. I also offers no security.

## Routing

No explicit Azure IoT Edge routing is needed for these modules.

Your edge device needs outbound access to both the IoT Hub and the blob storage containing the CSV file (over port 443).

## Contributions

If you want to contribute, please create a pull request. Thanks!