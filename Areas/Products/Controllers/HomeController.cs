using Microsoft.AspNetCore.Mvc;
using Omni_MVC_2.Areas.Products.Models;
using Omni_MVC_2.Extensions.Filters;

namespace Omni_MVC_2.Areas.Products.Controllers
{
    public class HomeController : Controller
    {
        readonly IWebHostEnvironment _env;
        public HomeController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [Area("Products")]
        public IActionResult Index()
        {
            return View();
        }

        [Area("Products")]
        [HttpGet]
        public IActionResult Upload()
        {
            return View();
        }

        [HttpPost]
        [Area("Products")]
        public async Task<IActionResult> Upload(FileUploadModel model)
        {
            Console.WriteLine("===> Upload action triggered");
            if (model.File == null)
            {
                Console.WriteLine("===> File is NULL");
                ViewBag.Message = "File is null.";
                return View();
            }
            Console.WriteLine($"===> File received: {model.File.FileName}, Size: {model.File.Length}");
            if (model.File.Length > 0)
            {
                var uploadDir = Path.Combine(_env.WebRootPath, "uploads");
                Console.WriteLine("===> Upload Directory: " + uploadDir);
                try
                {
                    Directory.CreateDirectory(uploadDir);
                    Console.WriteLine("===> Directory created");
                    var filePath = Path.Combine(uploadDir, model.File.FileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.File.CopyToAsync(stream);
                        Console.WriteLine("===> File written to disk");
                    }
                    ViewBag.Message = "File uploaded successfully!";
                }
                catch (Exception ex)
                {
                    Console.WriteLine("===> EXCEPTION: " + ex.Message);
                    ViewBag.Message = "Upload failed: " + ex.Message;
                }
            }
            else
            {
                Console.WriteLine("===> File is EMPTY");
                ViewBag.Message = "File is empty.";
            }
            return View();
        }


        [Area("Products")]
        [HttpGet]
        public IActionResult UploadedFiles()
        {
            string uploadDir = Path.Combine(_env.WebRootPath, "uploads");
            List<string?> files = Directory.Exists(uploadDir) ? Directory.GetFiles(uploadDir).Select(Path.GetFileName).ToList() : [];
            return View(files);
        }

        [ServiceFilter(typeof(LogExecutionTimeFilter))]
        [Area("Products")]
        [HttpGet]
        public IActionResult Download(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return BadRequest("File name is required.");
            string filePath = Path.Combine(_env.WebRootPath, "uploads", fileName);
            if (!System.IO.File.Exists(filePath)) return NotFound();
            string mimeType = "application/octet-stream";
            return PhysicalFile(filePath, mimeType, fileName);
        }

    }
}