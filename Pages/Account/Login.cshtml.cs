using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

namespace SocialGenius.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager,
            ILogger<LoginModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Il nome utente è obbligatorio")]
            public string Username { get; set; }

            [Required(ErrorMessage = "La password è obbligatoria"), DataType(DataType.Password)]
            public string Password { get; set; }

            public bool RememberMe { get; set; }
        }

        public void OnGet(string returnUrl = null) { }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            if (!ModelState.IsValid)
                return Page();

            try
            {
                // Prima facciamo il sign out per garantire una sessione pulita
                await _signInManager.SignOutAsync();

                // Ora effettuiamo il login
                var result = await _signInManager.PasswordSignInAsync(
                    Input.Username,
                    Input.Password,
                    Input.RememberMe,
                    lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    var user = await _userManager.FindByNameAsync(Input.Username);
                    if (user == null)
                    {
                        _logger.LogWarning("Utente trovato per il login ma non recuperabile dopo l'autenticazione");
                        ModelState.AddModelError(string.Empty, "Si è verificato un errore durante l'accesso. Riprova.");
                        return Page();
                    }

                    // Ottieni i ruoli freschi
                    var roles = await _userManager.GetRolesAsync(user);

                    // Refresh completo del sign-in per assicurarci che i claim siano aggiornati
                    await _signInManager.RefreshSignInAsync(user);

                    _logger.LogInformation("Utente {Username} autenticato con ruoli: {Roles}",
                        user.UserName, string.Join(", ", roles));

                    // Decidi dove indirizzare l'utente in base ai ruoli
                    if (roles.Contains("Admin"))
                    {
                        return RedirectToPage("/Admin/Dashboard");
                    }
                    else if (roles.Contains("Premium"))
                    {
                        return Redirect("~/User/Premium/HomePremium");
                    }
                    else // Base o nessun ruolo
                    {
                        return RedirectToPage("/User/HomeUser");
                    }
                }
                else if (result.IsLockedOut)
                {
                    _logger.LogWarning("Account utente bloccato");
                    ModelState.AddModelError(string.Empty, "Account bloccato. Riprova più tardi.");
                }
                else if (result.RequiresTwoFactor)
                {
                    // Se implementerai l'autenticazione a due fattori in futuro
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }
                else
                {
                    _logger.LogWarning("Tentativo di accesso fallito per {Username}", Input.Username);
                    ModelState.AddModelError(string.Empty, "Nome utente o password non validi.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il login per {Username}", Input.Username);
                ModelState.AddModelError(string.Empty, "Si è verificato un errore durante l'accesso. Contatta l'assistenza.");
            }

            return Page();
        }
    }
}