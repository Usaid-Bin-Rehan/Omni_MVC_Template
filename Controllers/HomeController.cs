using System.Diagnostics;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Omni_MVC_2.Models;

namespace Omni_MVC_2.Controllers
{
    public class HomeController : Controller
    {
        readonly IValidator<UserProfileInputVM> _validator;
        readonly ILogger<HomeController> _logger;
        readonly IWebHostEnvironment _env;
        public HomeController(ILogger<HomeController> logger, IWebHostEnvironment env, IValidator<UserProfileInputVM> validator)
        {
            _logger = logger;
            _env = env;
            _validator = validator;
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
            string title = "About";
            string message = "This is the About page for our MVC app.";
            if (_env.IsDevelopment()) message = "DEVELOPER MODE";

            /// Type-Unsafe
            //ViewData["Title"] = "About";
            //ViewData["Message"] = "This is the About page for our MVC app.";

            /// Type-Safe
            var vm = new AboutVM
            {
                Title = title,
                Message = message,
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

        [HttpGet]
        public IActionResult CreateUser()
        {
            return View();
        }

        [HttpPost]
        public IActionResult CreateUser(UserProfileInputVM input)
        {
            var validationResult = _validator.Validate(input);

            if (!validationResult.IsValid)
            {
                foreach (var failure in validationResult.Errors) ModelState.AddModelError(failure.PropertyName, failure.ErrorMessage);

                return View(input);
            }

            ViewBag.Message = $"User {input.UserName} created successfully!";
            return View("Success");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
