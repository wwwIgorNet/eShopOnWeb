using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Azure.Messaging.ServiceBus;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.BasketAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Configuration;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class OrderService : IOrderService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IUriComposer _uriComposer;
    private readonly IRepository<Basket> _basketRepository;
    private readonly IRepository<CatalogItem> _itemRepository;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public OrderService(IRepository<Basket> basketRepository,
        IRepository<CatalogItem> itemRepository,
        IRepository<Order> orderRepository,
        IUriComposer uriComposer,
        IConfiguration config,
        IHttpClientFactory httpClientFactory)
    {
        _orderRepository = orderRepository;
        _uriComposer = uriComposer;
        _basketRepository = basketRepository;
        _itemRepository = itemRepository;
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    public async Task CreateOrderAsync(int basketId, Address shippingAddress)
    {
        var basketSpec = new BasketWithItemsSpecification(basketId);
        var basket = await _basketRepository.FirstOrDefaultAsync(basketSpec);

        Guard.Against.Null(basket, nameof(basket));
        Guard.Against.EmptyBasketOnCheckout(basket.Items);

        var catalogItemsSpecification = new CatalogItemsSpecification(basket.Items.Select(item => item.CatalogItemId).ToArray());
        var catalogItems = await _itemRepository.ListAsync(catalogItemsSpecification);

        var items = basket.Items.Select(basketItem =>
        {
            var catalogItem = catalogItems.First(c => c.Id == basketItem.CatalogItemId);
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
            var orderItem = new OrderItem(itemOrdered, basketItem.UnitPrice, basketItem.Quantity);
            return orderItem;
        }).ToList();

        var order = new Order(basket.BuyerId, shippingAddress, items);

        await _orderRepository.AddAsync(order);

        var json = JsonSerializer.Serialize(order.OrderItems.Select(item => new { item.ItemOrdered.CatalogItemId, item.Units }));
        await SendOrderItemsMessageToReserverAsync(json);

        //var httpClient = _httpClientFactory.CreateClient();
        //var orderItemsReserverUrl = _config["AzureFunctions:OrderItemsReserver"];
        //var reserverRes = await httpClient.PostAsJsonAsync(orderItemsReserverUrl, json);

        //var deliveryOrderProcessorUrl = _config["AzureFunctions:DeliveryOrderProcessor"];
        //var deliveryRes = await httpClient.PostAsJsonAsync(deliveryOrderProcessorUrl, new { order.ShipToAddress, FinalPrice = order.Total(), ListOfItems = order.OrderItems.Select(item => new { item.ItemOrdered.CatalogItemId, item.Units }) });
    }

    private async Task SendOrderItemsMessageToReserverAsync(string json)
    {
        var serviceBusConnectionString = _config["ServiceBus:ConnectionString"];
        var queueName = _config["ServiceBus:QueueName"];

        await using var client = new ServiceBusClient(serviceBusConnectionString);

        await using ServiceBusSender sender = client.CreateSender(queueName);
        try
        {
            string messageBody = json;
            var message = new ServiceBusMessage(messageBody);
            await sender.SendMessageAsync(message);
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"{DateTime.Now} :: Exception: {exception.Message}");
        }
        finally
        {
            await sender.DisposeAsync();
            await client.DisposeAsync();
        }
    }
}
