using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SocialGenius.Services
{
    public class AuthenticationService
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ILogger<AuthenticationService> _logger;

        public AuthenticationService(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            ILogger<AuthenticationService> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        public async Task<bool> IsUserAuthenticated(ClaimsPrincipal user)
        {
            if (user?.Identity?.IsAuthenticated != true)
            {
                _logger.LogInformation("Utente non autenticato");
                return false;
            }

            try
            {
                var userId = _userManager.GetUserId(user);
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("UserId null o vuoto per un utente che dovrebbe essere autenticato");
                    return false;
                }

                var idUser = await _userManager.FindByIdAsync(userId);
                if (idUser == null)
                {
                    _logger.LogWarning($"Utente con ID {userId} non trovato nel database");
                    return false;
                }

                var roles = await _userManager.GetRolesAsync(idUser);
                _logger.LogInformation($"Utente {idUser.UserName} ha i ruoli: {string.Join(", ", roles)}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la verifica dell'autenticazione dell'utente");
                return false;
            }
        }

        public async Task EnsureAuthenticationClearAndValid(ClaimsPrincipal user)
        {
            if (!await IsUserAuthenticated(user))
            {
                // Se l'utente non è autenticato correttamente, forza un logout
                await _signInManager.SignOutAsync();
                _logger.LogInformation("Autenticazione resettata");
            }
        }
    }
}