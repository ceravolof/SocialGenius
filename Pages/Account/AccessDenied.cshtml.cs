using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SocialGenius.Pages.Account
{
    [AllowAnonymous]
    public class AccessDeniedModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<AccessDeniedModel> _logger;

        public string CurrentUserName { get; set; }
        public List<string> UserRoles { get; set; }
        public string RequestedPath { get; set; }

        public AccessDeniedModel(
            UserManager<IdentityUser> userManager,
            ILogger<AccessDeniedModel> logger)
        {
            _userManager = userManager;
            _logger = logger;
            UserRoles = new List<string>();
        }

        public async Task<IActionResult> OnGetAsync()
        {
            RequestedPath = HttpContext.Request.Query["ReturnUrl"].ToString() ?? "Non specificato";

            if (User.Identity.IsAuthenticated)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    CurrentUserName = user.UserName;
                    UserRoles = (List<string>)await _userManager.GetRolesAsync(user);

                    _logger.LogWarning(
                        $"Accesso negato all'utente {CurrentUserName} con ruoli [{string.Join(", ", UserRoles)}] " +
                        $"per il percorso: {RequestedPath}");
                }
            }

            return Page();
        }
    }
}