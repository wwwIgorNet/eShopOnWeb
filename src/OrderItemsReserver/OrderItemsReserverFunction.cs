using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.ServiceBus;

namespace OrderItemsReserver
{
    public static class OrderItemsReserverFunction
    {
        private const int _maxRetries = 3;

        [FunctionName(nameof(OrderItemsReserverFunction))]
        public static async Task Run(
            [ServiceBusTrigger("orderitemsreserver", Connection = "ServiceBusConnection")] ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions)
        {
            try
            {
                Retry.Do(() =>
                {
                    var storageConnectionString = Environment.GetEnvironmentVariable("BlobStorageConnectionString");
                    var blobContainerClient = new BlobContainerClient(storageConnectionString, "items");
                    BlobClient blob = blobContainerClient.GetBlobClient(Guid.NewGuid().ToString());
                    blob.Upload(message.Body.ToStream());

                }, TimeSpan.FromSeconds(1), _maxRetries);
            }
            catch (Exception)
            {
                var connectionString = Environment.GetEnvironmentVariable("ServiceBusConnection");
                var clientOptions = new ServiceBusClientOptions();
                var client = new ServiceBusClient(connectionString, clientOptions);
                var queueName = "notsevedorders";
                var sender = client.CreateSender(queueName);
                var orderJson = new StreamReader(message.Body.ToStream()).ReadToEnd();
                await sender.SendMessageAsync(new ServiceBusMessage(orderJson));
                await client.DisposeAsync();
            }
            finally
            {
                await messageActions.CompleteMessageAsync(message);
            }
        }
    }
}
