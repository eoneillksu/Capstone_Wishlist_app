using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Threading.Tasks;
using Capstone_Wishlist_app.Services.Amazon;
using AmazonItem = Capstone_Wishlist_app.Services.Amazon.Item;
using System.Text.RegularExpressions;

namespace Capstone_Wishlist_app.Services {
    public class AmazonRetailer : IRetailer {
        private string _associateTag;
        private string _accessKey;
        private AWSECommerceServicePortType _amazonClient;

        public AmazonRetailer(string associateTag, string accessKey, string endpointConfigName) {
            _associateTag = associateTag;
            _accessKey = accessKey;
            _amazonClient = new AWSECommerceServicePortTypeClient(endpointConfigName);
        }

        public async Task<Item[]> FindItemsAsync(ItemCategory category, string keywords) {
            var itemSearch = new ItemSearch {
                AssociateTag = _associateTag,
                AWSAccessKeyId = _accessKey,
                Request = new ItemSearchRequest[] { 
                    new ItemSearchRequest {
                        SearchIndex = Enum.GetName(typeof(ItemCategory), category),
                        Keywords = keywords,
                        ItemPage = "1",
                        ResponseGroup = new string[] { "Images", "ItemAttributes" }
                    }
                }
            };
            var searchResult = await _amazonClient.ItemSearchAsync(new ItemSearchRequest1 { ItemSearch = itemSearch });

            var items = searchResult.ItemSearchResponse.Items[0].Item;

            if (items == null) {
                return new Item[0];
            }

            return items.Select(item => ToRetailerItem(item)).ToArray();
        }


        public async Task<Item[]> LookupItemsAsync(string[] itemIds) {
            if (itemIds.Length == 0) {
                return new Item[] { };
            }

            var itemLookup = new ItemLookup {
                AssociateTag = _associateTag,
                AWSAccessKeyId = _accessKey,
                Request = new ItemLookupRequest[] {
                    new ItemLookupRequest {
                        ItemId = itemIds.Length > 10 ? itemIds.Take(10).ToArray() : itemIds,
                        IdType = ItemLookupRequestIdType.ASIN,
                        IdTypeSpecified = true,
                        ResponseGroup = new string[]{ "Images", "ItemAttributes" }
                    }
                }
            };

            var lookupResult = await _amazonClient.ItemLookupAsync(new ItemLookupRequest1 { ItemLookup = itemLookup });
            var items = lookupResult.ItemLookupResponse.Items[0].Item;
            return items.Select(item => ToRetailerItem(item)).ToArray();
        }

        private static Item ToRetailerItem(AmazonItem item) {
            return new Item {
                Id = item.ASIN,
                ListPrice = ToDecimalPrice(item.ItemAttributes.ListPrice),
                Title = item.ItemAttributes.Title,
                ListingUrl = item.DetailPageURL,
                ImageUrl = item.SmallImage == null ? "" : item.SmallImage.URL,
                MinAgeMonths = ToAgeMonths(item.ItemAttributes.ManufacturerMinimumAge),
                MaxAgeMonths = ToAgeMonths(item.ItemAttributes.ManufacturerMaximumAge),
            };
        }

        private static decimal ToDecimalPrice(Price price) {
            return price == null ? 0 : decimal.Parse(price.Amount) / 100;
        }

        private static int ToAgeMonths(DecimalWithUnits age) {
            return age == null ? 0 : (int) age.Value;
        }


        public async Task<Item> LookupItemFromUrlAsync(string url) {
            var itemId = GetItemIdFromUrl(url);

            if (itemId == null) {
                return null;
            }

            var items = await LookupItemsAsync(new[] { itemId });

            return items.FirstOrDefault();
        }

        public string GetItemIdFromUrl(string url) {
            const string asinPattern = @"/dp/([a-zA-Z0-9]{1,20})";
            var regex = new Regex(asinPattern, RegexOptions.IgnoreCase);
            var match = regex.Match(url);

            if (!match.Success) {
                return null;
            }

            return match.Groups[1].Value;
        }
    }
}