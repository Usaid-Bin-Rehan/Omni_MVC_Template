using Microsoft.AspNetCore.Mvc;
using Omni_MVC_2.Extensions.Attributes;
using Omni_MVC_2.Extensions.Common;
using Omni_MVC_2.Models;

namespace Omni_MVC_2.Controllers
{
    [ApiController]
    [Route("api")]
    public class HomeApiController : ControllerBase
    {
        [HttpGet("GetUser/{id:int}")]
        [ProducesResponseType(typeof(ApiResponseModel), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponseModel), StatusCodes.Status404NotFound)]
        public IResult GetUser(int id)
        {
            var vm = new UserProfileVM
            {
                UserId = id,
                UserName = $"User{id}",
                Bio = $"This is the bio for user {id}."
            };

            if (vm == null)
            {
                return ApiResponseHelper.Convert(
                    IsRequestHandled: true,
                    status: false,
                    message: "User not found",
                    statusCode: HTTPStatusCode400.NotFound,
                    data: new { }
                );
            }

            return ApiResponseHelper.Convert(
                IsRequestHandled: true,
                status: true,
                message: "User fetched successfully",
                statusCode: HTTPStatusCode200.Ok,
                data: vm
            );
        }

        [SwaggerApiKey(isRequired: true, expectedApiKey: "myDummyKey1")]
        [ApiKeyAuth]
        [HttpPost("CreateUser")]
        [ProducesResponseType(typeof(ApiResponseModel), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponseModel), StatusCodes.Status400BadRequest)]
        public IResult CreateUser([FromBody] UserProfileInputVM input)
        {
            if (!ModelState.IsValid)
            {
                return ApiResponseHelper.Convert(
                    IsRequestHandled: true,
                    status: false,
                    message: "Validation failed",
                    statusCode: HTTPStatusCode400.BadRequest,
                    data: new { },
                    exception: ModelState
                );
            }

            return ApiResponseHelper.Convert(
                IsRequestHandled: true,
                status: true,
                message: "User created successfully",
                statusCode: HTTPStatusCode200.Created,
                data: input
            );
        }

        [HttpGet("Login")]
        [ProducesResponseType(typeof(ApiResponseModel), StatusCodes.Status200OK)]
        public IResult Login()
        {
            return ApiResponseHelper.Convert(
                IsRequestHandled: true,
                status: true,
                message: "Please POST to this endpoint with username and password to login.",
                statusCode: HTTPStatusCode200.Ok,
                data: new { }
            );
        }

        [HttpPost("Login")]
        [ProducesResponseType(typeof(ApiResponseModel), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponseModel), StatusCodes.Status401Unauthorized)]
        public IResult Login([FromBody] RequestLogin login)
        {
            if (login.Username == "admin" && login.Password == "password")
            {
                return ApiResponseHelper.Convert(
                    IsRequestHandled: true,
                    status: true,
                    message: "Login successful",
                    statusCode: HTTPStatusCode200.Ok,
                    data: new { login.Username }
                );
            }

            return ApiResponseHelper.Convert(
                IsRequestHandled: true,
                status: false,
                message: "Invalid username or password",
                statusCode: HTTPStatusCode400.Unauthorized,
                data: new { }
            );
        }

        [HttpGet("Dashboard")]
        [ProducesResponseType(typeof(ApiResponseModel), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponseModel), StatusCodes.Status401Unauthorized)]
        public IResult Dashboard([FromHeader(Name = "Authorization")] string authHeader)
        {
            if (string.IsNullOrEmpty(authHeader))
            {
                return ApiResponseHelper.Convert(
                    IsRequestHandled: true,
                    status: false,
                    message: "Missing Authorization header",
                    statusCode: HTTPStatusCode400.Unauthorized,
                    data: new { }
                );
            }

            return ApiResponseHelper.Convert(
                IsRequestHandled: true,
                status: true,
                message: "Welcome to the dashboard",
                statusCode: HTTPStatusCode200.Ok,
                data: new { Username = "admin" }
            );
        }
    }
}