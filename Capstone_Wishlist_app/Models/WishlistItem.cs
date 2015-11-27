using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using Capstone_Wishlist_app.Services;

namespace Capstone_Wishlist_app.Models {
    public class WishlistItem {
        [Key]
        public int Id { get; set; }

        public int WishlistId { get; set; }
        public string ItemId { get; set; }

        [ForeignKey("WishlistId")]
        public virtual Wishlist Wishlist { get; set; }

        //The order status of the item
        public WishlistItemStatus Status { get; set; }
    }

    //The possible order statuses for all items
    public enum WishlistItemStatus {
        Unapproved,
        Avaliable,
        Ordered
    };

    public class FoundItem {
        [Key]
        public string ItemId { get; set; }

        public decimal ListPrice { get; set; }
        public int MinAgeMonths { get; set; }
        public int MaxAgeMonths { get; set; }

        public virtual ICollection<ItemSearchCategory> Categories { get; set; }
        public virtual ICollection<string> Keywords { get; set; }
    }

    public class ItemKeyword {
        [Key]
        [Column(Order = 1)]
        public string ItemId { get; set; }

        [Key]
        [Column(Order = 2)]
        public string Keyword { get; set; }

        [ForeignKey("ItemId")]
        public virtual FoundItem Item { get; set; }
    }

    public class ItemSearchCategory {
        [Key]
        [Column(Order = 1)]
        public string ItemId { get; set; }

        [Key]
        [Column(Order = 2)]
        public ItemCategory Category { get; set; }

        [ForeignKey("ItemId")]
        public virtual FoundItem Item { get; set; }
    }
}