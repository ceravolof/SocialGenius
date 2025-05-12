using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SocialGenius.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly UserManager<IdentityUser> _userManager;

        public IndexModel(ILogger<IndexModel> logger, UserManager<IdentityUser> userManager)
        {
            _logger = logger;
            _userManager = userManager;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            // Se l'utente è già autenticato, reindirizzalo alla pagina appropriata
            if (User.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    var roles = await _userManager.GetRolesAsync(user);

                    if (roles.Contains("Admin"))
                    {
                        return RedirectToPage("/Admin/Dashboard");
                    }
                    else if (roles.Contains("Premium"))
                    {
                        // Usa lo stesso percorso che hai nel LoginModel
                        return RedirectToPage("/User/Premium/HomePremium");
                        // Oppure: return RedirectToPage("/User/HomePremium");
                    }
                    else // Base o nessun ruolo
                    {
                        return RedirectToPage("/User/HomeUser");
                    }
                }
            }

            // Se non è autenticato, mostra la pagina normale
            return Page();
        }
    }
}