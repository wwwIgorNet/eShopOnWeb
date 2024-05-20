using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;

namespace OrderItemsReserver
{
    public static class DeliveryOrderProcessorFunction
    {
        [FunctionName(nameof(DeliveryOrderProcessorFunction))]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req)
        {
                string json = await new StreamReader(req.Body).ReadToEndAsync();
                string connectionString = "AccountEndpoint=https://eshop-app-delivery-orders.documents.azure.com:443/;AccountKey=oE3zduWS6Q6FbZ1pkhQJUMwcELOxzduNobRaFJXuM8thA71Kskwv0Urcpk9HizduhduZSFW10YnjACDbNXKuiw==;";
                CosmosClient client = new(connectionString);
                Database database = client.GetDatabase("delivery-order-db");
                Container container = database.GetContainer("delivery-order-container");
                await container.CreateItemAsync(new { id = Guid.NewGuid().ToString(), DeliveryDetails = json });

            return new OkObjectResult(json);
        }
    }
}
