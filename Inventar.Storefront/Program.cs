using Inventar.Storefront.Data;
using Inventar.Storefront.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<StorefrontDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Inventar")));
builder.Services.Configure<StorefrontSettings>(builder.Configuration.GetSection(StorefrontSettings.SectionName));
builder.Services.AddHttpContextAccessor();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromHours(6);
});
builder.Services.AddScoped<ICartService, SessionCartService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
