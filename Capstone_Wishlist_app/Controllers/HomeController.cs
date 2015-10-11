﻿using Capstone_Wishlist_app.Models;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Configuration;
using RetailService;
using System.ServiceModel.Web;

namespace Capstone_Wishlist_app.Controllers {
    public class HomeController : Controller {

        private IRetailer _retailer;

        private IRetailer Retailer {
            get { return _retailer ?? new WebChannelFactory<IRetailer>("RetailService").CreateChannel(); }
        }

        public ActionResult Index() {
            //*********************************This section of code is only used in the initial launch to assign the first user to use
            //********************************** the registration as an Admin. step 1 run the program with this section commented out
            //********************************** step 2: re-run the program with this comment not commented out. Stop the program and comment this section out forever more.
            //using (var context = new ApplicationDbContext())
            //{
            //    var roleStore = new RoleStore<IdentityRole>(context);
            //    var roleManager = new RoleManager<IdentityRole>(roleStore);

            //    roleManager.Create(new IdentityRole("Admin"));

            //    var userStore = new UserStore<ApplicationUser>(context);
            //    var userManager = new UserManager<ApplicationUser>(userStore);

            //    var user = userManager.FindByEmail("mesbrook@packardonline.com");
            //    userManager.AddToRole(user.Id, "Admin");
            //    context.SaveChanges();
            //}

            return View();
        }

        public ActionResult About() {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact() {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        public ActionResult Items() {
            var items = Retailer.FindItems(ItemCategory.Toys, "minecraft lego set");
            return View(items);
        }
    }
}