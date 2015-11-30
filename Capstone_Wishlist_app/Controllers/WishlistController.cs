using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;
using System.Data.Entity.Migrations;
using System.Configuration;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Capstone_Wishlist_app.Models;
using Capstone_Wishlist_app.DAL;
using Capstone_Wishlist_app.Services;
using RetailCategory = Capstone_Wishlist_app.Services.ItemCategory;
using ItemCategory = Capstone_Wishlist_app.Models.ItemSearchCategory;

namespace Capstone_Wishlist_app.Controllers {
    public class WishlistController : Controller {
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

        private WishlistContext _db;
        private IRetailer _retailer;

        public WishlistController()
            : base() {
            _db = new WishlistContext();
            _retailer = new AmazonRetailer(AmazonAssociateTag, AmazonAccessKey, "AWSECommerceServicePort");
        }

        [Authorize(Roles="Admin")]
        public async Task<ActionResult> Index() {
            var wishlists = await _db.WishLists.Include(wl => wl.Child.Family)
                .Include(wl => wl.Items)
                .ToListAsync();

            var wishlistViews = wishlists.Select(wl => new ManageWishlistViewModel {
                WishlistId = wl.Id,
                FamilyId = wl.Child.FamilyId,
                ChildFirstName = wl.Child.FirstName,
                ParentFirstName = wl.Child.Family.ParentFirstName,
                ParentLastName = wl.Child.Family.ParentLastName,
                ItemCount = wl.Items.Count,
                UnapprovedCount = wl.Items.CountUnapproved(),
                AvailableCount = wl.Items.CountAvailable(),
                DonatedCount = wl.Items.CountDonated()
            });

            return View(wishlistViews);
        }

        [HttpGet]
        [FamilyAuthorize(Entity="Wishlist")]
        public ActionResult FindGifts(int id) {
            var wishlist = _db.WishLists.Find(id);

            return View(new FindGiftsViewModel {
                WishlistId = id,
                ChildFirstName = wishlist.Child.FirstName
            });
        }

        [HttpGet]
        [FamilyAuthorize(Entity="Wishlist")]
        public async Task<ActionResult> SearchItems(int id, RetailCategory category, string keywords) {
            var items = await _retailer.FindItemsAsync(category, keywords);
            await UpdateFoundItems(items, category, keywords);
            var existingItemIds = await (
                from wi in _db.WishlistItems
                where wi.WishlistId == id
                select wi.ItemId).ToListAsync();
            var viewModel = new FindGiftsResultsViewModel {
                WishlistId = id,
                Results = items.ToList(),
                ExistingItemIds = existingItemIds
            };

            return PartialView("_SearchResults", viewModel);
        }

        private async Task UpdateFoundItems(IList<Item> items, RetailCategory category, string keywords) {
            var itemIds = items.Select(i => i.Id)
                .ToList();

            using (var db = new WishlistContext()) {
                var foundItems = await db.FoundItems.Where(fi => itemIds.Contains(fi.ItemId))
                    .ToListAsync();
                var newItems = BuildNewFoundItems(foundItems, items);

                foreach(var ni in newItems) {
                    db.FoundItems.Add(ni);
                }

                await db.SaveChangesAsync();                
                
                foundItems.AddRange(newItems);
                var foundItemIds = foundItems.Select(fi => fi.ItemId)
                    .ToList();
                var keywordList = SplitKeywords(keywords);
                await UpdateItemKeywwords(db, foundItemIds, keywordList);
                await UpdateItemCategories(db, foundItemIds, category);
            }
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
        
        private async Task UpdateItemKeywwords(WishlistContext db, ICollection<string> itemIds, IList<string> keywords) {
            var itemKeywords = itemIds.SelectMany(id => keywords.Select(kw => new {
                ItemId = id,
                Keyword = kw
            })).ToList();
            var existingKeywords = db.ItemKeywords.Where(ik => itemIds.Contains(ik.ItemId))
                .Select(ik => new { ItemId = ik.ItemId, Keyword = ik.Keyword })
                .ToList();
            var newKeywords = itemKeywords.Except(existingKeywords)
                .ToList();

            foreach (var ik in newKeywords) {
                db.ItemKeywords.Add(new ItemKeyword {
                    ItemId = ik.ItemId,
                    Keyword = ik.Keyword
                });
            }

            await db.SaveChangesAsync();
        }

        private async Task UpdateItemCategories(WishlistContext db, ICollection<string> itemIds, RetailCategory category) {
            var itemCategories = itemIds.Select(id => new {
                ItemId = id,
                Category = category
            }).ToList();
            var existingCategories = db.ItemCategories.Where(ic => itemIds.Contains(ic.ItemId))
                .Select(ic => new { ItemId = ic.ItemId, Category = ic.Category })
                .ToList();
            var newCategories = itemCategories.Except(existingCategories);

            foreach (var ic in newCategories) {
                db.ItemCategories.Add(new ItemCategory {
                    ItemId = ic.ItemId,
                    Category = ic.Category
                });
            }

            await db.SaveChangesAsync();
        }

        [HttpPost]
        [FamilyAuthorize(Entity="Wishlist")]
        public async Task<ActionResult> AddItem(int id, string itemId) {
            var isItemOnWishlist = await (
                from wi in _db.WishlistItems
                where wi.WishlistId == id && wi.ItemId == itemId
                select wi
                ).AnyAsync();

            if (isItemOnWishlist) {
                return Json(new { IsOnWishlist = true });
            }

            var wishItem = new WishlistItem {
                WishlistId = id,
                ItemId = itemId,
                Status = WishlistItemStatus.Unapproved,
            };

            _db.WishlistItems.Add(wishItem);
            await _db.SaveChangesAsync();

            return Json(new { IsOnWishlist = true });
        }

        [HttpPost]
        [FamilyAuthorize(Entity="Wishlist")]
        public async Task<ActionResult> RemoveItem(int id, int itemId) {
            var wishItem = await _db.WishlistItems.Where(wi => wi.Id == itemId)
                .FirstOrDefaultAsync();

            if (wishItem != null) {
                _db.WishlistItems.Remove(wishItem);
                await _db.SaveChangesAsync();
                TempData["itemRemoved"] = wishItem.ItemId;
            }
            
            var wishlist = await _db.WishLists.Where(w => w.Id == id)
                .Include(w => w.Items)
                .FirstAsync();
            var items = await GetViewableItems(wishlist.Items.ToList());

            return PartialView("_OwnItems", items);
        }

        [HttpGet]
        [FamilyAuthorize(Entity="Wishlist")]
        public async Task<ActionResult> ViewOwn(int id) {
            var wishlist = _db.WishLists.Find(id);
            var items = await GetViewableItems(wishlist.Items.ToList());

            return View(new OwnWishlistViewModel {
                WishlistId = wishlist.Id,
                ChildId = wishlist.ChildId,
                FamilyId = wishlist.Child.FamilyId,
                ChildFirstName = wishlist.Child.FirstName,
                ChildLastName = wishlist.Child.LastName,
                Items = items
            });
        }

        [HttpGet]
        [Authorize(Roles="Admin")]
        public async Task<ActionResult> Unapproved() {
            var wishlists = await _db.WishLists.Where(
                wl => wl.Items.Any(wi => wi.Status == WishlistItemStatus.Unapproved))
                .Include(wl => wl.Child)
                .Include(wl => wl.Items)
                .ToListAsync();
            var wishlistViews = wishlists.Select(wl => new UnapprovedWishlistViewModel {
                WishlistId = wl.Id,
                ChildFirstName = wl.Child.FirstName,
                UnapprovedCount = wl.Items.Count(wi => wi.Status == WishlistItemStatus.Unapproved)
            });

            return View(wishlistViews);
        }

        [HttpGet]
        [Authorize(Roles="Admin")]
        public async Task<ActionResult> Approve(int id) {
            var wishlist = await _db.WishLists.Where(w => w.Id == id)
                .Include(w => w.Items)
                .Include(w => w.Child)
                .FirstOrDefaultAsync();
            var items = wishlist.Items.Select(wi => new ApproveItemViewModel {
                Id = wi.Id,
                WishlistId = wi.WishlistId,
                ItemId = wi.ItemId,
                Status = wi.Status,
                IsSelected = false
            }).OrderBy(ia => ia.Status)
            .ToList();

            await AddRetailerItemProperties(items);

            return View(new ApproveWishlistViewModel {
                WishlistId = wishlist.Id,
                ChildId = wishlist.ChildId,
                FamilyId = wishlist.Child.FamilyId,
                ChildFirstName = wishlist.Child.FirstName,
                ChildLastName = wishlist.Child.LastName,
                Items = items
            });
        }

        [HttpPost]
        [Authorize(Roles="Admin")]
        public async Task<ActionResult> Approve(int id, ApproveWishlistViewModel approval) {
            var items = await _db.WishlistItems.Where(wi => wi.WishlistId == id)
                .ToListAsync();
            var itemApprovals = items.Join(approval.Items, wi => wi.Id, ai => ai.Id,
                (wi, ai) => new { Item = wi, IsApproved = ai.IsSelected });

            foreach (var ia in itemApprovals) {
                if (ia.IsApproved && ia.Item.Status == WishlistItemStatus.Unapproved) {
                    ia.Item.Status = WishlistItemStatus.Available;
                }
            }

            await _db.SaveChangesAsync();
            return RedirectToAction("Approve", new { id = id });
        }

        [HttpPost]
        [Authorize(Roles="Admin")]
        public async Task<ActionResult> DisapproveItems(int id, ApproveWishlistViewModel disapproval) {
            var items = await _db.WishlistItems.Where(wi => wi.WishlistId == id)
                .ToListAsync();
            var itemApprovals = items.Join(disapproval.Items, wi => wi.Id, ai => ai.Id,
                (wi, ai) => new { Item = wi, IsApproved = ai.IsSelected });

            foreach (var ia in itemApprovals) {
                if (ia.IsApproved && ia.Item.Status == WishlistItemStatus.Available) {
                    ia.Item.Status = WishlistItemStatus.Unapproved;
                }
            }

            await _db.SaveChangesAsync();
            return RedirectToAction("Approve", new { id = id });
        }

        private async Task AddRetailerItemProperties(IList<ApproveItemViewModel> items) {
            var itemIds = items.Select(i => i.ItemId).ToArray();
            var retailItems = await _retailer.LookupItemsAsync(itemIds);
            var itemMatches = items.Join(retailItems, wi => wi.ItemId, ri => ri.Id,
                (wi, ri) => new{ Item = wi, RetailItem = ri});

            foreach (var match in itemMatches) {
                var wi = match.Item;
                var ri = match.RetailItem;
                wi.Title = ri.Title;
                wi.ListingUrl = ri.ListingUrl;
                wi.ImageUrl = ri.ImageUrl;
                wi.ListPrice = ri.ListPrice;
                wi.MinAgeMonths = ri.MinAgeMonths;
                wi.MaxAgeMonths = ri.MaxAgeMonths;
            }
        }

        private async Task<IList<WishlistItemViewModel>> GetViewableItems(IList<WishlistItem> items) {
            var itemIds = items.Select(i => i.ItemId).ToArray();
            var retailItems = await _retailer.LookupItemsAsync(itemIds);

            return items.Join(retailItems, wi => wi.ItemId, ri => ri.Id,
                (wi, ri) => new WishlistItemViewModel {
                    Id = wi.Id,
                    WishlistId = wi.WishlistId,
                    ItemId = wi.ItemId,
                    Status = wi.Status,
                    Title = ri.Title,
                    ListingUrl = ri.ListingUrl,
                    ImageUrl = ri.ImageUrl,
                    ListPrice = ri.ListPrice,
                    MinAgeMonths = ri.MinAgeMonths,
                    MaxAgeMonths = ri.MaxAgeMonths
                }).ToList();
        }
    }
}