using Microsoft.AspNetCore.Mvc;
using Omni_MVC_2.Areas.Products.Models;
using Omni_MVC_2.Extensions.Common;
using Omni_MVC_2.Extensions.Filters;

namespace Omni_MVC_2.Areas.Products.Controllers
{
    [ApiController]
    [Route("api/Product")]
    public class ProductApiController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        public ProductApiController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [HttpPost("upload")]
        [ProducesResponseType(typeof(ApiResponseModel), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponseModel), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponseModel), StatusCodes.Status500InternalServerError)]
        public async Task<IResult> Upload([FromForm] FileUploadModel model)
        {
            if (model.File == null)
            {
                return ApiResponseHelper.Convert(
                    IsRequestHandled: true,
                    status: false,
                    message: "File is required.",
                    statusCode: HTTPStatusCode400.BadRequest,
                    data: new { }
                );
            }

            if (model.File.Length == 0)
            {
                return ApiResponseHelper.Convert(
                    IsRequestHandled: true,
                    status: false,
                    message: "File is empty.",
                    statusCode: HTTPStatusCode400.BadRequest,
                    data: new { }
                );
            }

            string uploadDir = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadDir);
            string filePath = Path.Combine(uploadDir, model.File.FileName);

            try
            {
                using var stream = new FileStream(filePath, FileMode.Create);
                await model.File.CopyToAsync(stream);

                return ApiResponseHelper.Convert(
                    IsRequestHandled: true,
                    status: true,
                    message: "File uploaded successfully.",
                    statusCode: HTTPStatusCode200.Ok,
                    data: new { model.File.FileName }
                );
            }
            catch (Exception ex)
            {
                return ApiResponseHelper.Convert(
                    IsRequestHandled: true,
                    status: false,
                    message: "Upload failed.",
                    statusCode: HTTPStatusCode500.InternalServerError,
                    data: new { },
                    exception: ex.Message
                );
            }
        }

        [HttpGet("files")]
        [ProducesResponseType(typeof(ApiResponseModel), StatusCodes.Status200OK)]
        public IResult UploadedFiles()
        {
            string uploadDir = Path.Combine(_env.WebRootPath, "uploads");
            var files = Directory.Exists(uploadDir) ? Directory.GetFiles(uploadDir).Select(Path.GetFileName).ToList() : [];

            return ApiResponseHelper.Convert(
                IsRequestHandled: true,
                status: true,
                message: "Files fetched successfully.",
                statusCode: HTTPStatusCode200.Ok,
                data: files
            );
        }

        [ServiceFilter(typeof(LogExecutionTimeFilter))]
        [HttpGet("download")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponseModel), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponseModel), StatusCodes.Status404NotFound)]
        public IActionResult Download([FromQuery] string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                var errorResponse = ApiResponseHelper.Convert(
                    IsRequestHandled: true,
                    status: false,
                    message: "File name is required.",
                    statusCode: HTTPStatusCode400.BadRequest,
                    data: new { }
                );

                return new JsonResult(errorResponse)
                {
                    StatusCode = HTTPStatusCode400.BadRequest,
                    ContentType = "application/json"
                };
            }

            string filePath = Path.Combine(_env.WebRootPath, "uploads", fileName);

            if (!System.IO.File.Exists(filePath))
            {
                var notFoundResponse = ApiResponseHelper.Convert(
                    IsRequestHandled: true,
                    status: false,
                    message: "File not found.",
                    statusCode: HTTPStatusCode400.NotFound,
                    data: new { }
                );

                return new JsonResult(notFoundResponse)
                {
                    StatusCode = HTTPStatusCode400.NotFound,
                    ContentType = "application/json"
                };
            }

            const string mimeType = "application/octet-stream";
            return PhysicalFile(filePath, mimeType, fileName);
        }
    }
}