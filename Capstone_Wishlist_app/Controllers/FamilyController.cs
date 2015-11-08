﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity.EntityFramework;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Security.Claims;
using Capstone_Wishlist_app.DAL;
using Capstone_Wishlist_app.Models;

namespace Capstone_Wishlist_app.Controllers
{
    public class FamilyController : Controller
    {
        private WishlistContext _db = new WishlistContext();

        // GET: Family
        public ActionResult Index()
        {
            return View(_db.Families.ToList());
        }

        // GET: Family/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Family family = _db.Families.Find(id);
            if (family == null)
            {
                return HttpNotFound();
            }
            return View(family);
        }

        // GET: Family/Create
        public ActionResult Create()
        {
            //List<Child> ci = new List<Child> { new Child { Child_ID = 0, Child_FirstName = "", Child_LastName = "", Age = 0 } };
            return View();
        }

        // POST: Family/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Family_ID,ParentFirstName,ParentLastName,Shipping_address,Shipping_city,Shipping_state,Shipping_zipCode,Phone,Email")] Family family)
        {
            if (ModelState.IsValid)
            {
                _db.Families.Add(family);
                _db.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(family);
        }

        // GET: Family/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Family family = _db.Families.Find(id);
            if (family == null)
            {
                return HttpNotFound();
            }
            return View(family);
        }

        // POST: Family/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "Family_ID,ParentFirstName,ParentLastName,Shipping_address,Shipping_city,Shipping_state,Shipping_zipCode,Phone,Email")] Family family)
        {
            if (ModelState.IsValid)
            {
                _db.Entry(family).State = EntityState.Modified;
                _db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(family);
        }

        [HttpGet]
        public ActionResult Register() {
            return View(new RegisterFamilyModel { ShippingAddress = new CreateAddressModel { } });
        }

        [HttpPost]
        public async Task<ActionResult> Register(RegisterFamilyModel registration) {
            if (!ModelState.IsValid) {
                return View(registration);
            }

            var family = await CreateFamilyModel(registration);
            var familyCredentials = await CreateFamilyAccount(family);

            TempData["firstTimeRegistration"] = true;
            TempData["familyCredentials"] = familyCredentials;
            
            return RedirectToAction("RegisterChild", new { id = family.Id });
        }

        private async Task<Family> CreateFamilyModel(RegisterFamilyModel registration) {
            var family = new Family {
                ParentFirstName = registration.ParentFirstName,
                ParentLastName = registration.ParentLastName,
                Phone = registration.Phone,
                Email = registration.Email
            };

            if (!registration.IsShippingToCharity) {
                family.ShippingAddress = new Address {
                    LineOne = registration.ShippingAddress.LineOne,
                    LineTwo = registration.ShippingAddress.LineTwo,
                    City = registration.ShippingAddress.City,
                    State = registration.ShippingAddress.State,
                    PostalCode = registration.ShippingAddress.PostalCode
                };
            }

            _db.Families.Add(family);
            await _db.SaveChangesAsync();
            return family;
        }

        private async Task<FamilyCredentials> CreateFamilyAccount(Family family) {
            var userNameChars = family.ParentLastName.ToLowerInvariant()
                .ToCharArray()
                .Where(c => char.IsLetter(c))
                .ToArray();
            var userName = new string(userNameChars);
            var password = GenerateRandomPassword(6);
            var userStore = new UserStore<WishlistUser>(_db);
            var userManager = new WishlistUserManager(userStore);
            await userManager.CreateAsync(new WishlistUser {
                UserName = userName,
                Email = family.Email,
                PhoneNumber = family.Phone
            }, password);

            var createdUser = await userManager.FindByNameAsync(userName);
            await userManager.AddToRoleAsync(createdUser.Id, "Family");
            await userManager.AddClaimAsync(createdUser.Id, new Claim("Family", family.Id.ToString()));

            return new FamilyCredentials {
                Username = userName,
                Password = password
            };
        }

        private static string GenerateRandomPassword(int length) {
            var cryptoProvider = new RNGCryptoServiceProvider();
            var randomBytes = new byte[length];
            cryptoProvider.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }

        [HttpGet]
        public async Task<ActionResult> RegisterChild(int id) {
            var family = await _db.Families.FindAsync(id);

            return View(new RegisterChildModel { FamilyId = id, FamilyName = family.ParentLastName });
        }

        [HttpPost]
        public async Task<ActionResult> RegisterChild(RegisterChildModel registration) {
            if (!ModelState.IsValid) {
                return View(registration);
            }

            var child = new Child {
                FamilyId = registration.FamilyId,
                FirstName = registration.FirstName,
                LastName = registration.LastName,
                Age = registration.Age,
                Gender = registration.Gender
            };

            _db.Children.Add(child);
            await _db.SaveChangesAsync();

            TempData["registeredChild"] = child;
            return RedirectToAction("RegisterChild", new { id = registration.FamilyId });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
