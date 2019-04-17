using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PBIEmbedPOC.Models;
using PBIEmbedPOC.Services;

namespace PBIEmbedPOC.Pages
{
    public class Embed : PageModel
    {
        private readonly IEmbedService _embedService;

        public EmbedConfig EmbedConfig { get; set; }

        public Embed(IEmbedService embedService)
        {
            _embedService = embedService;
        }

        public async Task OnGet()
        {
//            await _embedService.EmbedReport("hrxanalyze.dev@northgateisltd.onmicrosoft.com", "User_Read");
            await _embedService.EmbedReport("89bc4df8-0d2b-4f78-ad55-e4a0e200a5d0", "User_Read");
//            await _embedService.EmbedReport("", "");

            EmbedConfig = _embedService.EmbedConfig;
        }
    }
}