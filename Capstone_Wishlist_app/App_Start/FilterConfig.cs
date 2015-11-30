using System.Security.Claims;
using System.Web;
using System.Web.Mvc;

namespace Capstone_Wishlist_app
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
        }
    }

    public class InjectDonorIdentityAttribute : FilterAttribute, IResultFilter {
        public InjectDonorIdentityAttribute() : base() { }

        public void OnResultExecuted(ResultExecutedContext filterContext) {
            return;
        }

        public void OnResultExecuting(ResultExecutingContext context) {
            if (!(context.Result is ViewResultBase)) {
                return;
            }

            var identity = context.HttpContext.User.Identity as ClaimsIdentity;
            var donorClaim = identity.FindFirst("Donor");

            if (donorClaim == null) {
                return;
            }

            var donorId = int.Parse(donorClaim.Value);
            var result = context.Result as ViewResultBase;
            result.ViewData.Add("DonorId", donorId);
        }
    }
}
