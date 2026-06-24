using Inventar.ViewModels.Login_Register.DTO;
using Microsoft.AspNetCore.Antiforgery;
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
                await _next(context);
            }
            catch (AntiforgeryValidationException ex)
            {
                _logger.Warning(ex, "Antiforgery validation failed for request {Path}", context.Request.Path);

                foreach (var cookieName in context.Request.Cookies.Keys.Where(cookieName =>
                             cookieName.StartsWith(".AspNetCore.Antiforgery", StringComparison.OrdinalIgnoreCase) ||
                             cookieName.Equals("Inventar.Antiforgery", StringComparison.OrdinalIgnoreCase)))
                {
                    context.Response.Cookies.Delete(cookieName);
                }

                if (IsApiRequest(context.Request))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    context.Response.ContentType = "application/json";

                    var errorResponse = new ErrorDetails
                    {
                        Message = "The form expired or became invalid. Please refresh the page and try again.",
                        StackTrace = _env.IsDevelopment() ? ex.StackTrace : null,
                        Path = context.Request.Path
                    };

                    await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
                    return;
                }

                context.Response.Redirect(GetSafeRedirectPath(context));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unhandled exception for request {Path}", context.Request.Path);

                if (IsApiRequest(context.Request))
                {
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

                    await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
                }
                else
                {
                    context.Response.Redirect("/Home/Error");
                }
            }
        }

        private bool IsApiRequest(HttpRequest request)
        {
            var path = request.Path.Value ?? string.Empty;
            return request.Headers["Accept"].Any(h => h.Contains("application/json")) ||
                   path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) ||
                   path.Contains("ajax", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetSafeRedirectPath(HttpContext context)
        {
            var referer = context.Request.Headers.Referer.ToString();
            if (Uri.TryCreate(referer, UriKind.Absolute, out var refererUri) &&
                Uri.TryCreate($"{context.Request.Scheme}://{context.Request.Host}", UriKind.Absolute, out var currentUri) &&
                string.Equals(refererUri.Host, currentUri.Host, StringComparison.OrdinalIgnoreCase))
            {
                return refererUri.PathAndQuery + refererUri.Fragment;
            }

            return "/Home/Index";
        }
    }
}
