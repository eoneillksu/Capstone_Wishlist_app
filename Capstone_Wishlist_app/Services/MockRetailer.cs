using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;

namespace Capstone_Wishlist_app.Services {
    public class MockRetailer : IRetailer {
        private string _itemFileName;

        public MockRetailer(string itemFileName) {
            _itemFileName = itemFileName;
        }

        public async Task<Item[]> FindItemsAsync(ItemCategory category, string keywords) {
            using (var reader = new StreamReader(_itemFileName)) {
                string serializedItems = reader.ReadToEnd();
                return JsonConvert.DeserializeObject<Item[]>(serializedItems);
            }
        }

        public Task<Item[]> LookupItemsAsync(string[] itemIds) {
            throw new NotImplementedException();
        }

        public Task<Item> LookupItemFromUrlAsync(string url) {
            throw new NotImplementedException();
        }
    }
}