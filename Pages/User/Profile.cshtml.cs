using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace SocialGenius.Pages.User
{
    [Authorize]
    public class ProfileModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;

        public ProfileModel(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public string UserEmail { get; set; } = string.Empty;
        public string UserBio { get; set; } = string.Empty;
        public bool IsPremium { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            UserEmail = user.Email ?? string.Empty;

            // In un'implementazione reale, questi dati verrebbero recuperati da una tabella di profilo utente
            UserBio = "Sono appassionato/a di tecnologia e innovazione. Mi piace condividere idee e collaborare con persone creative.";

            // Controlla se l'utente è premium
            IsPremium = User.IsInRole("Premium") || User.IsInRole("Admin");

            return Page();
        }

        public async Task<IActionResult> OnPostUpdateProfileAsync(string email, string bio)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            // Validazione email
            if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email))
            {
                TempData["ErrorMessage"] = "Inserisci un indirizzo email valido.";
                return RedirectToPage();
            }

            // Verifica se l'email è cambiata
            if (email != user.Email)
            {
                // Verifica se l'email è già in uso
                var existingUser = await _userManager.FindByEmailAsync(email);
                if (existingUser != null && existingUser.Id != user.Id)
                {
                    TempData["ErrorMessage"] = "Questa email è già in uso.";
                    return RedirectToPage();
                }

                // Aggiorna l'email
                user.Email = email;
                user.NormalizedEmail = email.ToUpper();
                var updateResult = await _userManager.UpdateAsync(user);

                if (!updateResult.Succeeded)
                {
                    TempData["ErrorMessage"] = "Si è verificato un errore durante l'aggiornamento dell'email.";
                    return RedirectToPage();
                }
            }

            // In una implementazione reale, questi dati andrebbero salvati in una tabella profilo
            UserBio = bio;

            TempData["SuccessToast"] = "Profilo aggiornato con successo!";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostChangePasswordAsync(string currentPassword, string newPassword, string confirmPassword)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            // Validazione input
            if (string.IsNullOrWhiteSpace(currentPassword) ||
                string.IsNullOrWhiteSpace(newPassword) ||
                string.IsNullOrWhiteSpace(confirmPassword))
            {
                TempData["ErrorMessage"] = "Tutti i campi password sono obbligatori.";
                return RedirectToPage();
            }

            // Verifica se le password corrispondono
            if (newPassword != confirmPassword)
            {
                TempData["ErrorMessage"] = "La nuova password e la conferma non corrispondono.";
                return RedirectToPage();
            }

            // Validazione della nuova password
            if (!IsPasswordValid(newPassword))
            {
                TempData["ErrorMessage"] = "La nuova password non soddisfa i requisiti di sicurezza.";
                return RedirectToPage();
            }

            // Cambio password
            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            if (result.Succeeded)
            {
                // Aggiorna il cookie di autenticazione
                await _signInManager.RefreshSignInAsync(user);
                TempData["SuccessMessage"] = "Password modificata con successo!";
                TempData["SuccessToast"] = "Password modificata con successo!";
                return RedirectToPage();
            }
            else
            {
                // Determina l'errore
                if (result.Errors.Any(e => e.Code == "PasswordMismatch"))
                {
                    TempData["ErrorMessage"] = "La password attuale non è corretta.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Si è verificato un errore durante il cambio della password.";
                }
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnPostUpgradeToPremiumAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                // In produzione qui dovresti gestire il pagamento
                await _userManager.AddToRoleAsync(user, "Premium");
                await _signInManager.RefreshSignInAsync(user);
                TempData["SuccessMessage"] = "Complimenti! Ora sei un utente Premium.";
                TempData["SuccessToast"] = "Upgrade a Premium completato!";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostCancelPremiumAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                await _userManager.RemoveFromRoleAsync(user, "Premium");
                await _signInManager.RefreshSignInAsync(user);
                TempData["InfoMessage"] = "Il tuo abbonamento Premium è stato annullato.";
                TempData["SuccessToast"] = "Abbonamento Premium annullato";
            }

            return RedirectToPage();
        }

        private bool IsPasswordValid(string password)
        {
            // Verifica lunghezza minima
            if (password.Length < 8) return false;

            // Verifica presenza di maiuscole
            if (!Regex.IsMatch(password, "[A-Z]")) return false;

            // Verifica presenza di minuscole
            if (!Regex.IsMatch(password, "[a-z]")) return false;

            // Verifica presenza di numeri
            if (!Regex.IsMatch(password, "[0-9]")) return false;

            // Verifica presenza di caratteri speciali
            if (!Regex.IsMatch(password, "[^A-Za-z0-9]")) return false;

            return true;
        }
    }
}