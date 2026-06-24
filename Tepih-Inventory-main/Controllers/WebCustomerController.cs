using Inventar.Data;
using Inventar.Models;
using Inventar.Utils;
using Inventar.ViewModels.StorefrontAdmin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventar.Controllers;

[Authorize(Roles = "admin,superadmin")]
public class WebCustomerController : Controller
{
    private readonly ApplicationDbContext _context;

    public WebCustomerController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var completedOrders = await _context.WebOrders
            .AsNoTracking()
            .Where(order => order.Status == WebOrderStatuses.Completed)
            .Select(order => new
            {
                CustomerEmail = string.IsNullOrWhiteSpace(order.CustomerEmail)
                    ? string.Empty
                    : order.CustomerEmail.Trim(),
                CustomerName = $"{order.CustomerFirstName} {order.CustomerLastName}".Trim(),
                CustomerPhone = order.CustomerPhone,
                OrderId = order.Id,
                TotalQuantity = order.Items.Select(item => (int?)item.Quantity).Sum() ?? 0,
                GrandTotal = order.GrandTotal,
                CompletedUtc = order.CompletedUtc ?? order.CreatedUtc
            })
            .ToListAsync();

        completedOrders = completedOrders
            .Select(order => new
            {
                order.CustomerEmail,
                CustomerName = TextEncodingHelper.Decode(order.CustomerName) ?? order.CustomerName,
                CustomerPhone = TextEncodingHelper.Decode(order.CustomerPhone),
                order.OrderId,
                order.TotalQuantity,
                order.GrandTotal,
                order.CompletedUtc
            })
            .ToList();

        var groupedCustomers = completedOrders
            .GroupBy(order => new
            {
                order.CustomerEmail,
                order.CustomerName
            })
            .Select(group =>
            {
                var latestOrder = group
                    .OrderByDescending(order => order.CompletedUtc)
                    .First();

                return new WebCustomerAdminListItemViewModel
                {
                    CustomerEmail = group.Key.CustomerEmail,
                    CustomerName = group.Key.CustomerName,
                    CustomerPhone = latestOrder.CustomerPhone,
                    OrderCount = group.Select(order => order.OrderId).Distinct().Count(),
                    TotalItemsOrdered = group.Sum(order => order.TotalQuantity),
                    TotalMoneySpent = group.Sum(order => order.GrandTotal)
                };
            })
            .OrderByDescending(customer => customer.TotalMoneySpent)
            .ThenBy(customer => customer.CustomerEmail)
            .ThenBy(customer => customer.CustomerName)
            .ToList();

        var emailGroups = groupedCustomers
            .GroupBy(customer => customer.CustomerEmail)
            .Select(group => new WebCustomerAdminEmailGroupViewModel
            {
                CustomerEmail = group.Key,
                Customers = group
                    .OrderByDescending(customer => customer.TotalMoneySpent)
                    .ThenBy(customer => customer.CustomerName)
                    .ToList()
            })
            .OrderByDescending(group => group.Customers.Sum(customer => customer.TotalMoneySpent))
            .ThenBy(group => group.CustomerEmail)
            .ToList();

        return View(new WebCustomerAdminIndexViewModel
        {
            EmailGroups = emailGroups
        });
    }
}
