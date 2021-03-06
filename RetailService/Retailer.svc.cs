﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using System.Configuration;
using RetailService.Amazon;
using AmazonItem = RetailService.Amazon.Item;

namespace RetailService {
    [ServiceBehavior]
    public class Retailer : IRetailer {
        private static string AmazonAccessKey {
            get {
                return ConfigurationManager.AppSettings["AWSAccessKeyId"];
            }
        }

        private static string AmazonAssociateTag {
            get {
                return ConfigurationManager.AppSettings["AWSAssociatesId"];
            }
        }

        private AWSECommerceServicePortTypeClient _amazonClient;

        public Retailer() {
            _amazonClient = new AWSECommerceServicePortTypeClient("AWSECommerceServicePort");
        }

        public Item[] FindItems(ItemCategory category, string keywords) {
            var searchResult = _amazonClient.ItemSearch(
                new ItemSearch {
                    AssociateTag = AmazonAssociateTag,
                    AWSAccessKeyId = AmazonAccessKey,
                    Request = new ItemSearchRequest[] { 
                        new ItemSearchRequest {
                            SearchIndex = Enum.GetName(typeof(ItemCategory), category),
                            Keywords = keywords,
                            ItemPage = "1",
                            ResponseGroup = new string[] { "Images", "ItemAttributes" }
                        }
                    }
                });

            var items = new List<Item>();

            foreach (var amazonItem in searchResult.Items[0].Item) {
                var item = new Item {
                    Id = amazonItem.ASIN,
                    ListPrice = decimal.Parse(amazonItem.ItemAttributes.ListPrice.Amount),
                    Title = amazonItem.ItemAttributes.Title,
                    ListingUrl = amazonItem.DetailPageURL,
                    ImageUrl = amazonItem.SmallImage == null ? "" : amazonItem.SmallImage.URL
                };
                items.Add(item);
            }

            return items.ToArray();
        }

        public Item[] LookupItems(string itemIds) {
            var ids = itemIds.Split(',');

            if (ids.Length == 0) {
                return new Item[] { };
            }

            var itemLookup = new ItemLookup {
                    AssociateTag = AmazonAssociateTag,
                    AWSAccessKeyId = AmazonAccessKey,
                    Request = new ItemLookupRequest[] {
                        new ItemLookupRequest {
                            ItemId = ids.Length > 10 ? ids.Take(10).ToArray() : ids,
                            IdType = ItemLookupRequestIdType.ASIN,
                            IdTypeSpecified = true,
                            ResponseGroup = new string[]{ "Images", "ItemAttributes" }
                        }
                    }
                };

            var lookupResult = _amazonClient.ItemLookup(itemLookup);
            var items = new List<Item>();

            foreach (var amazonItem in lookupResult.Items[0].Item) {
                var item = new Item {
                    Id = amazonItem.ASIN,
                    ListPrice = decimal.Parse(amazonItem.ItemAttributes.ListPrice.Amount),
                    Title = amazonItem.ItemAttributes.Title,
                    ListingUrl = amazonItem.DetailPageURL,
                    ImageUrl = amazonItem.SmallImage == null ? "" : amazonItem.SmallImage.URL
                };
                items.Add(item);
            }

            return items.ToArray();
        }

        public PlacedOrder PlaceOrder(Order order) {
            throw new NotImplementedException();
        }

        public PlacedOrder GetOrder(string id) {
            throw new NotImplementedException();
        }

        public OrderStatus GetOrderStatus(string id) {
            throw new NotImplementedException();
        }
    }
}
