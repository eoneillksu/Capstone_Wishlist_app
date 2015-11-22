using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Capstone_Wishlist_app.Services {
    interface IRetailer {
        Task<Item[]> FindItemsAsync(ItemCategory category, string keywords);
        Task<Item[]> LookupItemsAsync(string[] itemIds);
        Task<Item> LookupItemFromUrlAsync(string url);
    }

    public class Item {
        public string Id { get; set; }
        public string Title { get; set; }
        public decimal ListPrice { get; set; }
        public string ImageUrl { get; set; }
        public string ListingUrl { get; set; }
        public int MinAgeMonths { get; set; }
        public int MaxAgeMonths { get; set; }
    }

    public enum ItemCategory {
        Books,
        Games,
        Toys,
        Apparel,
        Software,
        VideoGames,
        Video
    }

    public class Address {
        public string LineOne { get; set; }
        public string LineTwo { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string PostalCode { get; set; }
    }

    public class CardPayment {
        public string Number { get; set; }
        public string Name { get; set; }
        public Address BillingAddress { get; set; }
        public string VerficationCode { get; set; }
        public int ExpirationMonth { get; set; }
        public int ExpirationYear { get; set; }
    }

    public class OrderItem {
        public string ItemId { get; set; }
        public int Quantity { get; set; }
    }
    
    public class Order {
        public Address ShippingAddress { get; set; }
        public CardPayment Payment { get; set; }
        public OrderItem[] Items { get; set; }
    }

    public enum OrderStatus {
        Placed,
        Shipped,
        Failed
    }
    
    public class OrderLine {
        public string ItemId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Subtotal { get; set; }
    }

    public class PlacedOrder {
        public string Id { get; set; }
        public OrderStatus Status { get; set; }
        public Address ShippingAddress { get; set; }
        public decimal Tax { get; set; }
        public decimal Total { get; set; }
    }
}
