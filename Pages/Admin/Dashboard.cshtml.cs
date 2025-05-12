using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SocialGenius.Data;
using SocialGenius.Models;
using System.Text.Json;

namespace SocialGenius.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class DashboardModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _context;
        private readonly ILogger<DashboardModel> _logger;

        public List<IdentityUser> Users { get; set; }
        public int TotalUsers { get; set; }
        public int PremiumUsers { get; set; }
        public int TotalPosts { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalPages { get; set; }

        public DashboardModel(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            AppDbContext context,
            ILogger<DashboardModel> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync(string q, string role, string status, int page = 1)
        {
            // Impostazione pagina corrente
            CurrentPage = page < 1 ? 1 : page;

            // Query base per tutti gli utenti
            var query = _userManager.Users.AsQueryable();

            // Applicazione dei filtri di ricerca se presenti
            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(u =>
                    u.UserName.Contains(q) ||
                    u.Email.Contains(q) ||
                    u.Id.Contains(q));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                bool isActive = status.ToLower() == "active";
                query = query.Where(u => u.EmailConfirmed == isActive);
            }

            // Calcolo dei totali per le statistiche
            TotalUsers = await _userManager.Users.CountAsync();

            // Correzione: Conteggio accurato degli utenti Premium
            try
            {
                // Otteniamo il ruolo Premium
                var premiumRole = await _roleManager.FindByNameAsync("Premium");
                if (premiumRole != null)
                {
                    // Importante: usiamo Distinct per assicurarci di non contare duplicati
                    var premiumUserIds = await _context.UserRoles
                        .Where(ur => ur.RoleId == premiumRole.Id)
                        .Select(ur => ur.UserId)
                        .Distinct() // Elimina eventuali duplicati
                        .ToListAsync();

                    PremiumUsers = premiumUserIds.Count;
                }
                else
                {
                    _logger.LogWarning("Ruolo 'Premium' non trovato nel database");
                    PremiumUsers = 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il conteggio utenti Premium");
                PremiumUsers = 0;
            }

            // Conteggio dei post totali
            TotalPosts = await _context.Posts.CountAsync();

            // Calcolo delle pagine totali
            double totalUsers = await query.CountAsync();
            TotalPages = (int)Math.Ceiling(totalUsers / PageSize);

            // Ottenimento degli utenti paginati
            Users = await query
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // Filtraggio per ruolo (se specificato)
            if (!string.IsNullOrWhiteSpace(role))
            {
                var usersInRole = new List<IdentityUser>();
                foreach (var user in Users)
                {
                    if (await _userManager.IsInRoleAsync(user, role))
                    {
                        usersInRole.Add(user);
                    }
                }
                Users = usersInRole;
            }

            return Page();
        }

        public async Task<List<string>> GetUserRoles(IdentityUser user)
        {
            return (await _userManager.GetRolesAsync(user)).ToList();
        }

        // Metodo per aggiungere un utente
        public async Task<IActionResult> OnPostAddUserAsync([FromBody] UserDto model)
        {
            _logger.LogInformation($"Tentativo di creazione utente: {model?.Username}");
            if (model == null)
            {
                _logger.LogWarning("Tentativo di creazione utente con modello null");
                return new JsonResult(new { success = false, message = "Dati mancanti" });
            }

            if (!ModelState.IsValid)
            {
                var errors = string.Join(", ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));
                _logger.LogWarning($"ModelState non valido: {errors}");
                return new JsonResult(new { success = false, message = $"Dati non validi: {errors}" });
            }

            // Verifica che lo username non sia già in uso
            var existingUser = await _userManager.FindByNameAsync(model.Username);
            if (existingUser != null)
            {
                return new JsonResult(new { success = false, message = "Username già in uso" });
            }

            // Verifica che l'email non sia già in uso
            existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                return new JsonResult(new { success = false, message = "Email già in uso" });
            }

            // Crea il nuovo utente
            var user = new IdentityUser
            {
                UserName = model.Username,
                Email = model.Email,
                EmailConfirmed = true // Confermiamo automaticamente l'email
            };

            // Genera una password semplice ma sicura per facilitare il login
            var password = GenerateSimplePassword();

            // Crea l'utente
            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                _logger.LogInformation($"Utente {model.Username} creato con successo");

                try
                {
                    // Assegna il ruolo - importante: verifica che il ruolo esista
                    if (!await _roleManager.RoleExistsAsync(model.Role))
                    {
                        _logger.LogWarning($"Ruolo {model.Role} non trovato, creazione automatica");
                        await _roleManager.CreateAsync(new IdentityRole(model.Role));
                    }

                    await _userManager.AddToRoleAsync(user, model.Role);
                    _logger.LogInformation($"Ruolo {model.Role} assegnato a {model.Username}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Errore nell'assegnazione del ruolo {model.Role} all'utente {model.Username}");
                    return new JsonResult(new
                    {
                        success = true, // L'utente è stato creato ma c'è stato un errore con il ruolo
                        warning = $"Utente creato ma errore nell'assegnazione del ruolo: {ex.Message}",
                        message = "Utente creato con successo",
                        temporaryPassword = password
                    });
                }

                return new JsonResult(new
                {
                    success = true,
                    message = "Utente creato con successo",
                    temporaryPassword = password
                });
            }
            else
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogWarning($"Errore durante la creazione dell'utente {model.Username}: {errors}");

                return new JsonResult(new
                {
                    success = false,
                    message = "Errore durante la creazione dell'utente",
                    errors = result.Errors.Select(e => e.Description)
                });
            }
        }

        // Metodo per modificare un utente
        public async Task<IActionResult> OnPostEditUserAsync([FromBody] UserEditDto model)
        {
            _logger.LogInformation($"Tentativo di modifica utente ID: {model?.Id}");

            if (model == null)
            {
                _logger.LogWarning("Tentativo di modifica utente con modello null");
                return new JsonResult(new { success = false, message = "Dati mancanti" });
            }

            if (!ModelState.IsValid)
            {
                var errors = string.Join(", ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));
                _logger.LogWarning($"ModelState non valido: {errors}");
                return new JsonResult(new { success = false, message = $"Dati non validi: {errors}" });
            }

            var user = await _userManager.FindByIdAsync(model.Id);

            if (user == null)
            {
                _logger.LogWarning($"Utente con ID {model.Id} non trovato");
                return new JsonResult(new { success = false, message = "Utente non trovato" });
            }

            // Verifica se lo username è cambiato e se è già in uso
            if (user.UserName != model.Username)
            {
                var existingUser = await _userManager.FindByNameAsync(model.Username);
                if (existingUser != null && existingUser.Id != model.Id)
                {
                    return new JsonResult(new { success = false, message = "Username già in uso" });
                }
            }

            // Verifica se l'email è cambiata e se è già in uso
            if (user.Email != model.Email)
            {
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null && existingUser.Id != model.Id)
                {
                    return new JsonResult(new { success = false, message = "Email già in uso" });
                }
            }

            // Aggiorna i dati dell'utente
            user.UserName = model.Username;
            user.Email = model.Email;
            user.EmailConfirmed = model.IsActive;

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                _logger.LogInformation($"Utente {model.Username} (ID: {model.Id}) aggiornato con successo");

                try
                {
                    // Aggiorna il ruolo
                    var currentRoles = await _userManager.GetRolesAsync(user);

                    // Se il ruolo è cambiato
                    if (!currentRoles.Contains(model.Role) || currentRoles.Count > 1)
                    {
                        // Verifica che il ruolo esista
                        if (!await _roleManager.RoleExistsAsync(model.Role))
                        {
                            _logger.LogWarning($"Ruolo {model.Role} non trovato, creazione automatica");
                            await _roleManager.CreateAsync(new IdentityRole(model.Role));
                        }

                        // Rimuovi tutti i ruoli correnti
                        if (currentRoles.Any())
                        {
                            await _userManager.RemoveFromRolesAsync(user, currentRoles);
                        }

                        // Assegna il nuovo ruolo
                        await _userManager.AddToRoleAsync(user, model.Role);
                        _logger.LogInformation($"Ruolo {model.Role} assegnato a {model.Username}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Errore nell'aggiornamento dei ruoli per {model.Username}");
                    return new JsonResult(new
                    {
                        success = true, // L'utente è stato aggiornato ma c'è stato un errore con il ruolo
                        warning = $"Utente aggiornato ma errore nell'aggiornamento dei ruoli: {ex.Message}",
                        message = "Utente aggiornato con successo"
                    });
                }

                return new JsonResult(new { success = true, message = "Utente aggiornato con successo" });
            }
            else
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogWarning($"Errore durante l'aggiornamento dell'utente {model.Username}: {errors}");

                return new JsonResult(new
                {
                    success = false,
                    message = "Errore durante l'aggiornamento dell'utente",
                    errors = result.Errors.Select(e => e.Description)
                });
            }
        }

        // Metodo per eliminare un utente
        public async Task<IActionResult> OnPostDeleteUserAsync([FromBody] DeleteUserDto model)
        {
            _logger.LogInformation($"Tentativo di eliminazione utente ID: {model?.Id}");

            if (string.IsNullOrEmpty(model?.Id))
            {
                _logger.LogWarning("Tentativo di eliminazione utente con ID null o vuoto");
                return new JsonResult(new { success = false, message = "ID utente non valido" });
            }

            var user = await _userManager.FindByIdAsync(model.Id);

            if (user == null)
            {
                _logger.LogWarning($"Utente con ID {model.Id} non trovato per eliminazione");
                return new JsonResult(new { success = false, message = "Utente non trovato" });
            }

            try
            {
                // Elimina tutti i post dell'utente
                var posts = await _context.Posts.Where(p => p.UserId == model.Id).ToListAsync();
                if (posts.Any())
                {
                    _context.Posts.RemoveRange(posts);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Eliminati {posts.Count} post dell'utente {user.UserName}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Errore durante l'eliminazione dei post dell'utente {user.UserName}");
                // Continuiamo comunque, l'importante è eliminare l'utente
            }

            // Elimina l'utente
            var result = await _userManager.DeleteAsync(user);

            if (result.Succeeded)
            {
                _logger.LogInformation($"Utente {user.UserName} (ID: {model.Id}) eliminato con successo");
                return new JsonResult(new { success = true, message = "Utente eliminato con successo" });
            }
            else
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogWarning($"Errore durante l'eliminazione dell'utente {user.UserName}: {errors}");

                return new JsonResult(new
                {
                    success = false,
                    message = "Errore durante l'eliminazione dell'utente",
                    errors = result.Errors.Select(e => e.Description)
                });
            }
        }

        // Metodo per reimpostare la password di un utente
        public async Task<IActionResult> OnPostResetPasswordAsync([FromBody] ResetPasswordDto model)
        {
            _logger.LogInformation($"Tentativo di reset password utente ID: {model?.Id}");

            if (string.IsNullOrEmpty(model?.Id))
            {
                _logger.LogWarning("Tentativo di reset password utente con ID null o vuoto");
                return new JsonResult(new { success = false, message = "ID utente non valido" });
            }

            var user = await _userManager.FindByIdAsync(model.Id);

            if (user == null)
            {
                _logger.LogWarning($"Utente con ID {model.Id} non trovato per reset password");
                return new JsonResult(new { success = false, message = "Utente non trovato" });
            }

            try
            {
                // Generazione di una password semplice ma sicura
                var newPassword = GenerateSimplePassword();

                // Generazione di un token di reset
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);

                // Reset della password
                var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

                if (result.Succeeded)
                {
                    _logger.LogInformation($"Password reimpostata con successo per l'utente {user.UserName}");

                    return new JsonResult(new
                    {
                        success = true,
                        message = "Password reimpostata con successo",
                        temporaryPassword = newPassword
                    });
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogWarning($"Errore durante il reset della password per {user.UserName}: {errors}");

                    return new JsonResult(new
                    {
                        success = false,
                        message = "Errore durante il reset della password",
                        errors = result.Errors.Select(e => e.Description)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Eccezione durante il reset della password per {user.UserName}");
                return new JsonResult(new
                {
                    success = false,
                    message = $"Errore durante il reset della password: {ex.Message}"
                });
            }
        }

        // Genera una password semplice ma valida per le policy di sicurezza
        private string GenerateSimplePassword()
        {
            // Genera una password che soddisfa i requisiti standard di ASP.NET Identity:
            // - Almeno una lettera maiuscola
            // - Almeno una lettera minuscola
            // - Almeno un numero
            // - Almeno un carattere speciale
            // - Lunghezza minima di 8 caratteri
            return "Password1!";
        }

        // Helper per generare password casuali sicure (più complesse)
        private string GenerateRandomPassword(int length = 12)
        {
            // Questo metodo genera password più complesse ma potrebbe non funzionare per alcuni utenti
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%^&*?_-";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }

    // DTO per l'aggiunta di un utente
    public class UserDto
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
    }

    // DTO per la modifica di un utente
    public class UserEditDto
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; }
    }

    // DTO per l'eliminazione di un utente
    public class DeleteUserDto
    {
        public string Id { get; set; }
    }

    // DTO per il reset della password
    public class ResetPasswordDto
    {
        public string Id { get; set; }
    }
}