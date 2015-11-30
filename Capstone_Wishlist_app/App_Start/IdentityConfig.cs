using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;
using SendGrid;
using System.Net;
using System.Configuration;
using System.Diagnostics;
using System.Web.Mvc;
using System.Security.Claims;
using Capstone_Wishlist_app.DAL;
using Capstone_Wishlist_app.Models;

namespace Capstone_Wishlist_app
{
    // Configure the application user manager used in this application. UserManager is defined in ASP.NET Identity and is used by the application.

    public class WishlistUserManager : UserManager<WishlistUser>
    {
        public WishlistUserManager(IUserStore<WishlistUser> store)
            : base(store)
        {
        }

        public static WishlistUserManager Create(IdentityFactoryOptions<WishlistUserManager> options, IOwinContext context) 
        {
            var manager = new WishlistUserManager(new UserStore<WishlistUser>(context.Get<WishlistContext>()));
            // Configure validation logic for usernames
            manager.UserValidator = new UserValidator<WishlistUser>(manager)
            {
                AllowOnlyAlphanumericUserNames = false,
                RequireUniqueEmail = false
            };
            // Configure validation logic for passwords
            manager.PasswordValidator = new PasswordValidator
            {
                RequiredLength = 6,
                RequireNonLetterOrDigit = false,
                RequireDigit = false,
                RequireLowercase = true,
                RequireUppercase = true,
            };
            // Register two factor authentication providers. This application uses Phone and Emails as a step of receiving a code for verifying the user
            // You can write your own provider and plug in here.
            manager.RegisterTwoFactorProvider("PhoneCode", new PhoneNumberTokenProvider<WishlistUser>
            {
                MessageFormat = "Your security code is: {0}"
            });
            manager.RegisterTwoFactorProvider("EmailCode", new EmailTokenProvider<WishlistUser>
            {
                Subject = "Security Code",
                BodyFormat = "Your security code is: {0}"
            });
            manager.EmailService = new EmailService();
            manager.SmsService = new SmsService();
            var dataProtectionProvider = options.DataProtectionProvider;
            if (dataProtectionProvider != null)
            {
                manager.UserTokenProvider = new DataProtectorTokenProvider<WishlistUser>(dataProtectionProvider.Create("ASP.NET Identity"));
            }
            return manager;
        }
    }

    public class EmailService : IIdentityMessageService
    {
        public async Task SendAsync(IdentityMessage message)
        {
            await configSendGridasync(message);
        }

        // Use NuGet to install SendGrid (Basic C# client lib) 
        private async Task configSendGridasync(IdentityMessage message)
        {
            var myMessage = new SendGridMessage();
            myMessage.AddTo(message.Destination);
            myMessage.From = new System.Net.Mail.MailAddress(
                                "Santa@Wishlist.com", "Santa's Wishlist");
            myMessage.Subject = message.Subject;
            myMessage.Text = message.Body;
            myMessage.Html = message.Body;

            var credentials = new NetworkCredential(
                       ConfigurationManager.AppSettings["mailAccount"],
                       ConfigurationManager.AppSettings["mailPassword"]
                       );

            // Create a Web transport for sending email.
            var transportWeb = new Web(credentials);

            // Send the email.
            if (transportWeb != null)
            {
                await transportWeb.DeliverAsync(myMessage);
            }
            else
            {
                Trace.TraceError("Failed to create Web transport.");
                await Task.FromResult(0);
            }
        }
    }

    public class SmsService : IIdentityMessageService
    {
        public Task SendAsync(IdentityMessage message)
        {
            // Plug in your sms service here to send a text message.
            return Task.FromResult(0);
        }
    }

    public class FamilyAuthorizeAttribute : AuthorizeAttribute {
        public string Entity { get; set; }

        public override void OnAuthorization(AuthorizationContext context) {
            var id = context.RequestContext.RouteData.Values["id"];
            var claimsUser = context.HttpContext.User as ClaimsPrincipal;

            if (!claimsUser.HasClaim(Entity, id.ToString()) && !claimsUser.IsInRole(WishlistUser.AdminRole)) {
                HandleUnauthorizedRequest(context);
            }
            base.OnAuthorization(context);
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext context) {
            base.HandleUnauthorizedRequest(context);

            if (context.HttpContext.User.Identity.IsAuthenticated) {
                context.Result = new RedirectResult("~/Home/Unauthorized");
            }
        }
    }

    public class DonorAuthorizeAttribute : AuthorizeAttribute {
        public override void OnAuthorization(AuthorizationContext context) {
            var id = context.RequestContext.RouteData.Values["id"];
            var claimsUser = context.HttpContext.User as ClaimsPrincipal;

            if (!claimsUser.HasClaim("Donor", id.ToString())) {
                HandleUnauthorizedRequest(context);
            }
            base.OnAuthorization(context);
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext context) {
            base.HandleUnauthorizedRequest(context);

            if (context.HttpContext.User.Identity.IsAuthenticated) {
                context.Result = new RedirectResult("~/Home/Unauthorized");
            }
        }
    }
}
