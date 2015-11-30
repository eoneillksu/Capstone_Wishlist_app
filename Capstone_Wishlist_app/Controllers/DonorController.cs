using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;
using System.Configuration;
using System.Threading.Tasks;
using System.Data.Entity.Infrastructure;
using System.Web.SessionState;
using System.Security.Claims;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using Microsoft.AspNet.Identity.EntityFramework;
using Capstone_Wishlist_app.Services;
using Capstone_Wishlist_app.Models;
using Capstone_Wishlist_app.DAL;

namespace Capstone_Wishlist_app.Controllers {
    [SessionState(SessionStateBehavior.Required)]
    public class DonorController : Controller {
        private const string IdentityProviderClaimType = "http://schemas.microsoft.com/accesscontrolservice/2010/07/claims/identityprovider";

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

        public DonorController()
            : base() {
            _db = new WishlistContext();
        }

        [Authorize(Roles="Admin")]
        public ActionResult Index() {
            return View();
        }

        [HttpGet]
        public ActionResult Register(int? donorId) {
            return View(new RegisterDonorViewModel { DonorId = donorId });
        }

        [HttpPost]
        public async Task<ActionResult> Register(RegisterDonorViewModel registration) {
            var userManager = HttpContext.GetOwinContext().GetUserManager<WishlistUserManager>();
            var user = await CreateDonor(registration, userManager);

            if (registration.DonorId.HasValue) {
                await AuthorizeDonorForUser(user, registration.DonorId.Value, userManager);
                await SendConfirmationEmail(user, userManager);
                await SignDonorIn(user, userManager);

                return RedirectToAction("Index", "Home");
            }

            var donor = new Donor();
            _db.Donors.Add(donor);

            var cart = new Cart {
                Donor = donor,
                ModifiedDate = DateTime.Now
            };
            _db.Carts.Add(cart);
            await _db.SaveChangesAsync();

            await AuthorizeDonorForUser(user, donor.Id, userManager);
            await SendConfirmationEmail(user, userManager);
            await SignDonorIn(user, userManager);
            return RedirectToAction("Index", "Home");
        }

        private async Task<WishlistUser> CreateDonor(RegisterDonorViewModel registration, WishlistUserManager manager) {
            await manager.CreateAsync(new WishlistUser {
                Name = registration.Name,
                UserName = registration.Email,
                Email = registration.Email
            }, registration.Password);

            return await manager.FindByNameAsync(registration.Email);
        }

        private async Task AuthorizeDonorForUser(WishlistUser user, int donorId, WishlistUserManager manager) {
            await manager.AddToRoleAsync(user.Id, "Donor");
            await manager.AddClaimAsync(user.Id, new Claim("Donor", donorId.ToString()));
        }

        private async Task SendConfirmationEmail(WishlistUser user, WishlistUserManager manager) {
            string code = await manager.GenerateEmailConfirmationTokenAsync(user.Id);
            var callbackUrl = Url.Action("ConfirmEmail", "Account", new {
                userId = user.Id, code = code
            }, protocol: Request.Url.Scheme);
            await manager.SendEmailAsync(user.Id, "Confirm Your Email for Santa's Wishlist",
               "Please confirm your account by clicking <a href=\"" + callbackUrl + "\">here</a>");
        }

        private async Task SignDonorIn(WishlistUser user, WishlistUserManager manager) {
            var authManager = HttpContext.GetOwinContext().Authentication;
            authManager.SignIn(new AuthenticationProperties { IsPersistent = true },
                await user.GenerateUserIdentityAsync(manager));
        }

        [HttpPost]
        public async Task<ActionResult> SignInAnonymously() {
            var donor = new Donor();
            _db.Donors.Add(donor);

            var cart = new Cart { Donor = donor, ModifiedDate = DateTime.Now };
            _db.Carts.Add(cart);

            await _db.SaveChangesAsync();

            var identity = new ClaimsIdentity(DefaultAuthenticationTypes.ApplicationCookie);
            var anonymousIdentifier = Guid.NewGuid().ToString();
            identity.AddClaim(new Claim("Donor", donor.Id.ToString()));
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, anonymousIdentifier));
            identity.AddClaim(new Claim(IdentityProviderClaimType, anonymousIdentifier));
            var authManager = HttpContext.GetOwinContext().Authentication;
            authManager.SignIn(new AuthenticationProperties { IsPersistent = true }, identity);

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        [DonorAuthorize]
        [InjectDonorIdentity]
        public async Task<ActionResult> ViewWishlists(int id) {
            var wishlists = await GetAvailableWishlists();
            var allAvailableItems = wishlists.SelectMany(wl => wl.Items)
                .Where(wi => wi.Status == WishlistItemStatus.Available);
            var retailItems = await LookupItems(allAvailableItems);
            var itemsInCart = await GetItemsInCart(id);

            var wishlistViews = new List<DonorListViewModel>();

            foreach (var wl in wishlists) {
                var availableItems = wl.Items.Where(wi => wi.Status == WishlistItemStatus.Available)
                    .ToList();
                var biographyText = GetCurrentBiography(wl.Child);

                wishlistViews.Add(new DonorListViewModel {
                    ChildId = wl.ChildId,
                    WishlistId = wl.Id,
                    FirstName = wl.Child.FirstName,
                    Age = wl.Child.Age,
                    Gender = wl.Child.Gender,
                    Biography = wl.Child.Biographies.OrderBy(b => b.CreationDate).Select(b => b.Text).FirstOrDefault(),
                    Items = JoinIntoViewableItems(availableItems, itemsInCart, retailItems),
                });
            }

            return View(wishlistViews);
        }

        private async Task<IList<Wishlist>> GetAvailableWishlists() {
            return await _db.WishLists.Where(wl => wl.Items.Any(wi => wi.Status == WishlistItemStatus.Available))
                .Include(i => i.Items)
                .Include(wl => wl.Child.Biographies)
                .ToListAsync();
        }

        private async Task<IList<CartItem>> GetItemsInCart(int id) {
            var cart = await _db.Carts.Where(c => c.DonorId == id)
                .Include(c => c.Items)
                .SingleAsync();
            return cart.Items.ToList();
        }

        private string GetCurrentBiography(Child child) {
            return child.Biographies.OrderBy(b => b.CreationDate)
                    .Select(b => b.Text)
                    .FirstOrDefault();
        }

        private async Task<IList<Item>> LookupItems(IEnumerable<WishlistItem> wishlistItems) {
            const int MaxLookupCount = 10;

            var itemIds = wishlistItems.Select(wi => wi.ItemId)
                .ToArray();
            var items = new List<Item>();
            var retailer = new AmazonRetailer(AmazonAssociateTag, AmazonAccessKey, "AWSECommerceServicePort");

            while (itemIds.Any()) {
                var lookupIds = itemIds.Take(MaxLookupCount)
                    .ToArray();
                var resultItems = await retailer.LookupItemsAsync(lookupIds);
                items.AddRange(resultItems);
                itemIds = itemIds.Skip(lookupIds.Length)
                    .ToArray();
            }

            return items;
        }

        private IList<DonorWishlistItemViewModel> JoinIntoViewableItems(
            IList<WishlistItem> items,
            IList<CartItem> cartItems,
            IList<Item> retailItems
        ) {
            var inCartIds = cartItems.Select(ci => ci.WishlistItemId)
                .ToList();

            return items.Join(retailItems, wi => wi.ItemId, ri => ri.Id,
                (wi, ri) => new DonorWishlistItemViewModel {
                Id = wi.Id,
                WishlistId = wi.WishlistId,
                ItemId = wi.ItemId,
                Status = wi.Status,
                IsInCart = inCartIds.Contains(wi.Id),
                Title = ri.Title,
                ListingUrl = ri.ListingUrl,
                ImageUrl = ri.ImageUrl,
                ListPrice = ri.ListPrice,
                MinAgeMonths = ri.MinAgeMonths,
                MaxAgeMonths = ri.MaxAgeMonths
            }).ToList();
        }

        [HttpGet]
        [DonorAuthorize]
        public ActionResult ViewCart(int id) {
            var cart = _db.Carts.Where(c => c.DonorId == id)
                .Include(c => c.Donor)
                .Include(c => c.Items.Select(ci => ci.Item.Wishlist.Child))
                .Single();
            var items = cart.Items.Select(ci => new CartItemViewModel {
                CartId = ci.CartId,
                WishlistItemId = ci.WishlistItemId,
                ItemId = ci.Item.ItemId,
                ChildName = ci.Item.Wishlist.Child.FirstName,
                Price = ci.Price,
                Title = ci.Title
            }).ToList();

            return View(new CartViewModel {
                DonorId = id,
                Items = items
            });
        }

        [HttpPost]
        [DonorAuthorize]
        public async Task<ActionResult> AddItemToCart(int id, int wishlistItemId) {
            var isInCart = await _db.CartItems.AnyAsync(ci => ci.CartId == id && ci.Item.Id == wishlistItemId);

            if (isInCart) {
                return Json(new { WasInCart = true });
            }

            var item = await _db.WishlistItems.FindAsync(wishlistItemId);
            var retailer = new AmazonRetailer(AmazonAssociateTag, AmazonAccessKey, "AWSECommerceServicePort");
            var retailItem = (await retailer.LookupItemsAsync(new[] { item.ItemId })).FirstOrDefault();
            var cart = await _db.Carts.FindAsync(id);

            var cartItem = new CartItem {
                Cart = cart,
                Item = item,
                Price = retailItem.ListPrice,
                Title = retailItem.Title
            };

            _db.CartItems.Add(cartItem);
            await _db.SaveChangesAsync();

            return Json(new { IsInCart = true });
        }

        [HttpPost]
        [DonorAuthorize]
        public async Task<ActionResult> RemoveItemFromCart(int id, int wishlistItemId) {
            var item = await _db.CartItems.FindAsync(id, wishlistItemId);
            _db.CartItems.Remove(item);
            await _db.SaveChangesAsync();

            var items = await GetViewableItemsFromCart(id);

            return PartialView("_CartItems", new CartViewModel {
                DonorId = id,
                Items = items
            });
        }

        public async Task<IList<CartItemViewModel>> GetViewableItemsFromCart(int donorId) {
            var cart = await _db.Carts.Where(c => c.DonorId == donorId)
                .Include(c => c.Donor)
                .Include(c => c.Items.Select(ci => ci.Item.Wishlist.Child))
                .SingleAsync();
            return cart.Items.Select(ci => new CartItemViewModel {
                CartId = ci.CartId,
                WishlistItemId = ci.WishlistItemId,
                ItemId = ci.Item.ItemId,
                ChildName = ci.Item.Wishlist.Child.FirstName,
                Price = ci.Price,
                Title = ci.Title
            }).ToList();
        }

        [HttpGet]
        [DonorAuthorize]
        public ActionResult CountItemsInCart(int id) {
            var count = _db.CartItems.Count(ci => ci.CartId == id);

            return PartialView("_CartCount", new CartCountViewModel {
                DonorId = id,
                Count = count
            });
        }

        [HttpGet]
        [DonorAuthorize]
        public async Task<ActionResult> PurchaseCart(int id) {
            var items = await _db.CartItems.Where(ci => ci.CartId == id)
                .Include(ci => ci.Item.Wishlist.Child)
                .ToListAsync();

            return View(new PurchaseCartViewModel {
                DonorId = id,
                BillingAddress = new CreateAddressModel(),
                Items = ToViewableItems(items)
            });
        }

        private async Task RemoveUnavailableItems(IList<CartItem> items) {
            foreach (var ci in items) {
                _db.CartItems.Remove(ci);
            }

            await _db.SaveChangesAsync();
        }

        private IList<CartItemViewModel> ToViewableItems(IList<CartItem> items) {
            return items.Select(ci => new CartItemViewModel {
                CartId = ci.CartId,
                WishlistItemId = ci.WishlistItemId,
                ItemId = ci.Item.ItemId,
                ChildName = ci.Item.Wishlist.Child.FirstName,
                Price = ci.Price,
                Title = ci.Title
            }).ToList();
        }

        [HttpPost]
        [DonorAuthorize]
        public async Task<ActionResult> PurchaseCart(int id, PurchaseCartViewModel purchase) {
            var items = await _db.CartItems.Where(ci => ci.CartId == id)
                .Include(ci => ci.Item.Wishlist.Child)
                .ToListAsync();
            var availableItems = items.Where(ci => ci.Item.Status == WishlistItemStatus.Available)
                .ToList();
            var unavailableItems = items.Where(ci => ci.Item.Status != WishlistItemStatus.Available)
                .ToList();

            if (unavailableItems.Any()) {
                await RemoveUnavailableItems(unavailableItems);
                TempData["unavailableItems"] = ToViewableItems(unavailableItems);
            }

            Session["order"] = BuildOrderModel(id, items);

            return RedirectToAction("ConfirmOrder", new { id = id });
        }

        private OrderViewModel BuildOrderModel(int donorId, IList<CartItem> items) {
            var orderItems = items.Select(ci => new OrderItemViewModel {
                WishlistItemId = ci.Item.Id,
                ItemId = ci.Item.ItemId,
                Price = ci.Price,
                Title = ci.Title
            }).ToList();

            var subtotal = items.Sum(oi => oi.Price);
            var shipping = subtotal * (decimal) (0.05 + new Random().NextDouble() * 0.2);
            var tax = (subtotal + shipping) * 0.05m;
            var total = subtotal + shipping + tax;

            var order = new OrderViewModel {
                DonorId = donorId,
                OrderId = Guid.NewGuid().ToString(),
                Subtotal = subtotal,
                Shipping = shipping,
                SalesTax = tax,
                Total = total,
                Items = orderItems
            };

            return order;
        }

        [HttpGet]
        [DonorAuthorize]
        public ActionResult ConfirmOrder(int id) {
            var order = Session["order"] as OrderViewModel;

            return View(order);
        }

        [HttpPost]
        [DonorAuthorize]
        public async Task<ActionResult> CompleteOrder(int id) {
            var order = TakeOrderFromSession();

            var orderedItemIds = order.Items.Select(oi => oi.WishlistItemId);
            var wishlistItems = await _db.WishlistItems.Where(wi => orderedItemIds.Contains(wi.Id))
                .ToListAsync();

            var hasReserved = await ReserveItemsForDonation(wishlistItems);

            if (!hasReserved) {
                return RedirectToAction("PurchaseCart", new { id = id });
            }

            var donationId = await BuildDonationModel(order, wishlistItems);

            await ClearCart(wishlistItems);

            return RedirectToAction("ThankYou", new { id = id, donationId = donationId });
        }

        private OrderViewModel TakeOrderFromSession() {
            var order = Session["order"] as OrderViewModel;
            Session.Remove("order");

            return order;
        }

        private async Task ClearCart(List<WishlistItem> wishlistItems) {
            var donatedIds = wishlistItems.Select(wi => wi.Id);
            var clearItems = await _db.CartItems.Where(ci => donatedIds.Contains(ci.WishlistItemId))
                .ToListAsync();

            foreach(var ci in clearItems) {
                _db.CartItems.Remove(ci);
            }

            await _db.SaveChangesAsync();
        }

        private async Task<int> BuildDonationModel(OrderViewModel order, IList<WishlistItem> wishlistItems) {
            var donation = new Donation {
                DonorId = order.DonorId,
                OrderId = order.OrderId,
                Date = DateTime.Now,
                Subtotal = order.Subtotal,
                SalesTax = order.SalesTax,
                Total = order.Total
            };

            _db.Donations.Add(donation);

            var donatedItems = order.Items.Join(wishlistItems, oi => oi.WishlistItemId, wi => wi.Id,
                (oi, wi) => new DonatedItem {
                    Donation = donation,
                    Item = wi,
                    PurchasePrice = oi.Price,
                    Title = oi.Title,
                });

            foreach (var di in donatedItems) {
                _db.DonatedItems.Add(di);
            }

            await _db.SaveChangesAsync();

            return donation.Id;
        }

        private async Task<bool> ReserveItemsForDonation(IList<WishlistItem> items) {
            foreach (var wi in items) {
                wi.Status = WishlistItemStatus.Ordered;
            }

            try {
                await _db.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException) {
                return false;
            }
        }

        [HttpGet]
        [DonorAuthorize]
        public async Task<ActionResult> ThankYou(int id, int donationId) {
            var donation = await _db.Donations.Where(dn => dn.Id == donationId && dn.DonorId == id)
                .Include(dn => dn.Donor)
                .Include(dn => dn.Items.Select(di => di.Item.Wishlist.Child))
                .FirstAsync();
            var childNames = donation.Items.Select(di => di.Item.Wishlist.Child.FirstName)
                .Distinct()
                .ToList();

            return View(new ThankYouViewModel {
                DonorId = id,
                DonationId = donation.Id,
                Total = donation.Total,
                ChildNames = childNames
            });
        }

        [HttpGet]
        [DonorAuthorize]
        [InjectDonorIdentity]
        public async Task<ActionResult> History(int id) {
            var donations = await _db.Donations.Where(dn => dn.DonorId == id)
                .Include(dn => dn.Items.Select(di => di.Item.Wishlist.Child))
                .ToListAsync();
            var donationView = donations.Select(dn => new DonationViewModel {
                DonorId = dn.DonorId,
                DonationId = dn.Id,
                Date = dn.Date,
                OrderId = dn.OrderId,
                Subtotal = dn.Subtotal,
                Total = dn.Total,
                Items = dn.Items.Select(di => new DonatedItemViewModel {
                    ItemId = di.Item.ItemId,
                    Title = di.Title,
                    PurhcasePrice = di.PurchasePrice,
                    ChildFirsName = di.Item.Wishlist.Child.FirstName
                }).ToList()
            }).ToList();

            return View(donationView);
        }
    }
}