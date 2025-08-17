using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Omni_MVC_2.Models;

namespace Omni_MVC_2.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult About()
        {
            /// Type-Unsafe
            //ViewData["Title"] = "About";
            //ViewData["Message"] = "This is the About page for our MVC app.";

            /// Type-Safe
            var vm = new AboutVM
            {
                Title = "About",
                Message = "This is the About page for our MVC app."
            };
            return View(vm);
        }

        [Route("Home/UserProfile/{id:int}")]
        public IActionResult UserProfile(int id)
        {
            // For learning purposes, just return a view model with the ID
            var vm = new UserProfileVM
            {
                UserId = id,
                UserName = $"User{id}",
                Bio = $"This is the bio for user {id}."
            };

            return View(vm);
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
