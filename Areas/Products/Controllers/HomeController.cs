using Microsoft.AspNetCore.Mvc;

namespace Omni_MVC_2.Areas.Products.Controllers
{
    public class HomeController : Controller
    {
        [Area("Products")]
        public IActionResult Index()
        {
            return View();
        }
    }
}
