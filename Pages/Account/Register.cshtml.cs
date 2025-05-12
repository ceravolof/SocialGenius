using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace SocialGenius.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;

        public RegisterModel(UserManager<IdentityUser> userManager,
                            SignInManager<IdentityUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required, EmailAddress]
            public string Email { get; set; }

            [Required]
            [Display(Name = "Nome utente")]
            public string Username { get; set; }

            [Required, DataType(DataType.Password)]
            public string Password { get; set; }

            [DataType(DataType.Password), Compare("Password")]
            public string ConfirmPassword { get; set; }
        }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            // Utilizziamo il nome utente specificato anziché l'email
            var user = new IdentityUser { UserName = Input.Username, Email = Input.Email };
            var result = await _userManager.CreateAsync(user, Input.Password);

            if (result.Succeeded)
            {
                // Assegna ruolo di default "Base"
                var roleResult = await _userManager.AddToRoleAsync(user, "Base");

                if (!roleResult.Succeeded)
                {
                    // Gestione dell'errore di assegnazione del ruolo
                    foreach (var error in roleResult.Errors)
                        ModelState.AddModelError(string.Empty, $"Errore nell'assegnazione del ruolo: {error.Description}");

                    // Elimina l'utente se non è stato possibile assegnare il ruolo
                    await _userManager.DeleteAsync(user);
                    return Page();
                }

                await _signInManager.SignInAsync(user, isPersistent: false);
                return LocalRedirect("/User/HomeUser");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return Page();
        }
    }
}