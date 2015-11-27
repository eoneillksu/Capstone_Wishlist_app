using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using Microsoft.AspNet.Identity.EntityFramework;
using Capstone_Wishlist_app.Models;

namespace Capstone_Wishlist_app.DAL
{
    public class WishlistContext : IdentityDbContext<WishlistUser>
    {

        public WishlistContext() : base("WishlistContext", false) { }

        public DbSet<Wishlist> WishLists { get; set; }
        public DbSet<WishlistItem> WishlistItems { get; set; }
        public DbSet<Child> Children { get; set; }
        public DbSet<Family> Families { get; set; }
        public DbSet<Address> Addresses { get; set; }
        public DbSet<ChildBiography> Biographies { get; set; }
        public DbSet<Donor> Donors { get; set; }
        public DbSet<Donation> Donations { get; set; }
        public DbSet<DonatedItem> DonatedItems { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<FoundItem> FoundItems { get; set; }
        public DbSet<ItemKeyword> ItemKeywords { get; set; }
        public DbSet<ItemSearchCategory> ItemCategories { get; set; }
        
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();
        }
    }
}