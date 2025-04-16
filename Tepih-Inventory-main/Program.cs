using Inventar.Data;
using Inventar.Helpers;
using Inventar.Interfaces;
using Inventar.Repository;
using Inventar.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Localization;
using System.Globalization;
using Microsoft.AspNetCore.Localization.Routing;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc.Razor;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
builder.Services.AddScoped<ITepihRepository,TepihRepository>();
builder.Services.AddScoped<IKupacRepository, KupacRepository>();
builder.Services.AddScoped<ISalesRepository,SalesRepository>();
builder.Services.AddScoped<IPhotoService, PhotoService>();
builder.Services.AddScoped<IPlacanjeRepository, PlacanjeRepository>();



builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection("CloudinarySettings"));

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("TepihDb")));

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(10); // Set session timeout
    options.Cookie.HttpOnly = true; // Make the session cookie HTTP only
    options.Cookie.IsEssential = true; // Make the session cookie essential

});

var app = builder.Build();


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStaticFiles();

app.UseSession();

app.UseRouting();

app.Use(async (context, next) =>
{
    string cookie = string.Empty;
    if (context.Request.Cookies.TryGetValue("Language", out cookie))
    {
        System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo(cookie);
        System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(cookie);
    }
    else
    {
        System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en");
        System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en");
    }
    await next.Invoke();
});

app.UseAuthorization();


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
