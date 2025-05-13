using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace SocialGenius.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(SignInManager<IdentityUser> signInManager,
            ILogger<LoginModel> logger,
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager = null)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Il nome utente è obbligatorio")]
            [Display(Name = "Nome Utente")]
            public string Username { get; set; }

            [Required(ErrorMessage = "La password è obbligatoria")]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Display(Name = "Ricordami?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            // Cancella eventuali cookie esistenti per garantire un login pulito
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            // Se ci sono cookie di autenticazione corrotti, cancelliamoli
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                _logger.LogInformation($"Tentativo di login per l'utente: {Input.Username}");

                // Prima di tentare il login, assicuriamoci che non ci siano sessioni esistenti
                await _signInManager.SignOutAsync();

                // Tentativo di login con l'username (ora utilizziamo direttamente l'username)
                var result = await _signInManager.PasswordSignInAsync(
                    Input.Username,
                    Input.Password,
                    Input.RememberMe,
                    lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    var user = await _userManager.FindByNameAsync(Input.Username);
                    if (user != null)
                    {
                        var roles = await _userManager.GetRolesAsync(user);
                        _logger.LogInformation($"Utente {user.UserName} autenticato con ruoli: {string.Join(", ", roles)}");

                        // Ora verifichiamo dove dovrebbe andare l'utente in base al ruolo
                        if (roles.Contains("Admin"))
                        {
                            // Redirect alla dashboard admin
                            return RedirectToPage("/Admin/Dashboard");
                        }
                        else if (roles.Contains("Premium"))
                        {
                            // Redirect alla home premium
                            return RedirectToPage("/User/Premium/HomePremium");
                        }
                        else if(roles.Contains("Base"))
                        {
                            // Redirect alla home utente standard
                            return RedirectToPage("/User/HomeUser");
                        }
                        else
                        {
                            // Fallback per utenti senza ruolo specifico
                            _logger.LogWarning($"Utente {user.UserName} non ha ruoli specifici");
                            return RedirectToPage("/User/HomeUser");
                        }
                    }

                   
                }

                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }

                if (result.IsLockedOut)
                {
                    _logger.LogWarning("Account utente bloccato.");
                    return RedirectToPage("./AccesDenied");
                }
                else
                {
                    _logger.LogWarning($"Login fallito per {Input.Username}. Risultato: {result}");
                    ModelState.AddModelError(string.Empty, "Nome utente o password non validi.");
                    return Page();
                }
            }

            // Se siamo arrivati fin qui, c'è stato un errore, mostriamo nuovamente il form
            return Page();
        }
    }
}