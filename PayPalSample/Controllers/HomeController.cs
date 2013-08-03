using System.Web.Mvc;
using PayPalSample.Models;

namespace PayPalSample.Controllers
{
    public class HomeController : Controller
    {
        //
        // GET: /Home/

        public ActionResult Index()
        {
            return View(new PayPalViewData());
        }
    }
}
