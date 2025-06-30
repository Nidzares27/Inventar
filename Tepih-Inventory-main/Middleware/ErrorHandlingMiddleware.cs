using Inventar.ViewModels.Login_Register.DTO;
using Serilog;
using System.Net;
using System.Text.Json;

namespace Inventar.Middleware
{
    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IWebHostEnvironment _env;
        private readonly Serilog.ILogger _logger;

        public ErrorHandlingMiddleware(RequestDelegate next, IWebHostEnvironment env)
        {
            _next = next;
            _env = env;
            _logger = Log.ForContext<ErrorHandlingMiddleware>();
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context); // Proceed normally
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unhandled exception for request {Path}", context.Request.Path);

                if (IsApiRequest(context.Request))
                {
                    // API request – return JSON
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    context.Response.ContentType = "application/json";

                    var errorResponse = _env.IsDevelopment()
                        ? new ErrorDetails
                        {
                            Message = ex.Message,
                            StackTrace = ex.StackTrace,
                            Path = context.Request.Path
                        }
                        : new ErrorDetails
                        {
                            Message = "An unexpected error occurred.",
                            StackTrace = null,
                            Path = context.Request.Path
                        };

                    var json = JsonSerializer.Serialize(errorResponse);
                    await context.Response.WriteAsync(json);
                }
                else
                {
                    // Non-API request – redirect to error page
                    context.Response.Redirect("/Home/Error");
                }
            }
        }

        private bool IsApiRequest(HttpRequest request)
        {
            var path = request.Path.Value ?? "";
            return request.Headers["Accept"].Any(h => h.Contains("application/json")) ||
                   path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) ||
                   path.Contains("ajax", StringComparison.OrdinalIgnoreCase);
        }

    }
}
