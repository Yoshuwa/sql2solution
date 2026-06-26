using MiningFleetOps.Client.Models;
using MiningFleetOps.Client.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MiningFleetOps.Client.Pages;

public sealed partial class IndexModel : PageModel
{
    private readonly ApiSession _api;

    public IndexModel(ApiSession api)
    {
        _api = api;
    }

    public IReadOnlyList<ApiResource> Resources => ApiCatalog.Resources.Where(_api.CanRead).ToList();
}