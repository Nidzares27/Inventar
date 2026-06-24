using Inventar.Storefront.Services;
using Inventar.Storefront.ViewModels.Layout;
using Microsoft.AspNetCore.Mvc;

namespace Inventar.Storefront.ViewComponents;

public class CategoryMenuViewComponent : ViewComponent
{
    private readonly ICategoryNavigationService _categoryNavigationService;

    public CategoryMenuViewComponent(ICategoryNavigationService categoryNavigationService)
    {
        _categoryNavigationService = categoryNavigationService;
    }

    public async Task<IViewComponentResult> InvokeAsync(bool isMobile = false)
    {
        var viewModel = new CategoryMenuViewModel
        {
            IsMobile = isMobile,
            CategoryGroups = await _categoryNavigationService.GetCategoryGroupsAsync()
        };

        return View(viewModel);
    }
}
