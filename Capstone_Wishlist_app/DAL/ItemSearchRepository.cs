using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Data.Entity;
using System.Web;
using Capstone_Wishlist_app.Services;
using Capstone_Wishlist_app.Models;

namespace Capstone_Wishlist_app.DAL {
    public class ItemSearchRepository {
        private WishlistContext _db;

        public ItemSearchRepository() {
            _db = new WishlistContext();
        }

        public async Task StoreSearchAsync(IList<Item> items, ItemCategory category, string keywords) {
            var itemIds = items.Select(i => i.Id)
                .ToList();
            var foundItems = await _db.FoundItems.Where(fi => itemIds.Contains(fi.ItemId))
                .ToListAsync();
            var newItems = BuildNewFoundItems(foundItems, items);
            _db.FoundItems.AddRange(newItems);

            await _db.SaveChangesAsync();

            foundItems.AddRange(newItems);
            var foundItemIds = foundItems.Select(fi => fi.ItemId)
                .ToList();
            var keywordList = SplitKeywords(keywords);
            await UpdateItemKeywwords(foundItemIds, keywordList);
            await UpdateItemCategories(foundItemIds, category);
        }

        public async Task<IList<string>> GetMatchingItemsAsync(ICollection<ItemCategory> categories, string keywords) {
            IList<string> keywordList = SplitKeywords(keywords);

            var itemQuery = _db.FoundItems.Where(fi => fi.Keywords.Any(ik => keywordList.Contains(ik.Keyword)));

            if (categories.Any()) {
                itemQuery = itemQuery.Where(fi => fi.Categories.Any(ic => categories.Contains(ic.Category)));
            }
            var foundItems = await itemQuery.ToListAsync();

            return foundItems.Select(fi => fi.ItemId)
                .ToList();
        }

        private IList<string> SplitKeywords(string keywords) {
            return Regex.Split(keywords, @"\s+")
                .Where(kw => !string.IsNullOrEmpty(kw))
                .ToList();
        }

        private IList<FoundItem> BuildNewFoundItems(
            IList<FoundItem> foundItems,
            IList<Item> items
        ) {
            var foundItemIds = foundItems.Select(fi => fi.ItemId);
            return items.Where(i => !foundItemIds.Contains(i.Id))
                .Select(i => new FoundItem {
                    ItemId = i.Id,
                    ListPrice = i.ListPrice,
                    MinAgeMonths = i.MinAgeMonths,
                    MaxAgeMonths = i.MaxAgeMonths
                }).ToList();
        }

        private async Task UpdateItemKeywwords(ICollection<string> itemIds, IList<string> keywords) {
            var itemKeywords = itemIds.SelectMany(id => keywords.Select(kw => new {
                ItemId = id,
                Keyword = kw
            })).ToList();
            var existingKeywords = _db.ItemKeywords.Where(ik => itemIds.Contains(ik.ItemId))
                .Select(ik => new { ItemId = ik.ItemId, Keyword = ik.Keyword })
                .ToList();
            var newKeywords = itemKeywords.Except(existingKeywords)
                .ToList();

            foreach (var ik in newKeywords) {
                _db.ItemKeywords.Add(new ItemKeyword {
                    ItemId = ik.ItemId,
                    Keyword = ik.Keyword
                });
            }

            await _db.SaveChangesAsync();
        }

        private async Task UpdateItemCategories(ICollection<string> itemIds, ItemCategory category) {
            var itemCategories = itemIds.Select(id => new {
                ItemId = id,
                Category = category
            }).ToList();
            var existingCategories = _db.ItemCategories.Where(ic => itemIds.Contains(ic.ItemId))
                .Select(ic => new { ItemId = ic.ItemId, Category = ic.Category })
                .ToList();
            var newCategories = itemCategories.Except(existingCategories);

            foreach (var ic in newCategories) {
                _db.ItemCategories.Add(new ItemSearchCategory {
                    ItemId = ic.ItemId,
                    Category = ic.Category
                });
            }

            await _db.SaveChangesAsync();
        }
    }
}