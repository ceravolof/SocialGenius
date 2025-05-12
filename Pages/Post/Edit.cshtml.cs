using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SocialGenius.Data;
using System.Security.Claims;

namespace SocialGenius.Pages.Post
{
    [Authorize]
    public class EditModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        [BindProperty]
        public Models.Post Post { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        [TempData]
        public string SuccessMessage { get; set; }

        public EditModel(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            Post = await _context.Posts
                .Where(p => p.IdPost == id)
                .FirstOrDefaultAsync();

            if (Post == null)
            {
                return NotFound();
            }

            // Verifica che l'utente sia il proprietario del post o un amministratore
            if (Post.UserId != userId && !User.IsInRole("Admin"))
            {
                ErrorMessage = "Non hai il permesso di modificare questo post";
                return RedirectToPage(User.IsInRole("Premium") ? "/User/Premium/HomePremium" : "/User/HomeUser");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(IFormFile NewImage = null, bool keepCurrentImage = true)
        {
            // Importante: rimuoviamo l'errore di ModelState specifico per NewImage se presente
            if (ModelState.ContainsKey("NewImage"))
            {
                ModelState.Remove("NewImage");
            }

            if (!ModelState.IsValid)
            {
                // Log degli errori di ModelState per debug
                foreach (var state in ModelState)
                {
                    Console.WriteLine($"Campo: {state.Key}, Errori: {state.Value.Errors.Count}");
                    foreach (var error in state.Value.Errors)
                    {
                        Console.WriteLine($"- {error.ErrorMessage}");
                    }
                }
                return Page();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Carico il post COMPLETO dal database per assicurarmi di avere tutti i dati
            var existingPost = await _context.Posts
                .AsNoTracking()  // Importante per evitare problemi di tracking
                .FirstOrDefaultAsync(p => p.IdPost == Post.IdPost);

            if (existingPost == null)
            {
                return NotFound();
            }

            // Verifica che l'utente sia il proprietario del post o un amministratore
            if (existingPost.UserId != userId && !User.IsInRole("Admin"))
            {
                ErrorMessage = "Non hai il permesso di modificare questo post";
                return RedirectToPage(User.IsInRole("Premium") ? "/User/Premium/HomePremium" : "/User/HomeUser");
            }

            try
            {
                Console.WriteLine($"Aggiorno post ID {Post.IdPost}, ImgUrl originale: {existingPost.ImgUrl ?? "nessuna"}");

                // Aggiorna solo i campi consentiti
                var postToUpdate = new Models.Post
                {
                    IdPost = existingPost.IdPost,
                    UserId = existingPost.UserId,
                    Content = Post.Content,
                    SocialPlatform = existingPost.SocialPlatform,
                    DateCreate = existingPost.DateCreate,
                    ImgSource = existingPost.ImgSource,
                    ImgUrl = existingPost.ImgUrl  // Manteniamo l'URL originale come default
                };

                // Gestione dell'immagine
                if (NewImage != null && NewImage.Length > 0)
                {
                    Console.WriteLine("Caricamento nuova immagine");
                    string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + NewImage.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await NewImage.CopyToAsync(fileStream);
                    }

                    // Elimina vecchia immagine se esiste e non è un'immagine di default
                    if (!string.IsNullOrEmpty(existingPost.ImgUrl) && existingPost.ImgUrl.StartsWith("/uploads/"))
                    {
                        var oldImagePath = Path.Combine(_environment.WebRootPath, existingPost.ImgUrl.TrimStart('/'));
                        if (System.IO.File.Exists(oldImagePath))
                        {
                            try
                            {
                                System.IO.File.Delete(oldImagePath);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Errore eliminazione immagine: {ex.Message}");
                                // Continue execution even if we can't delete the old image
                            }
                        }
                    }

                    postToUpdate.ImgUrl = "/uploads/" + uniqueFileName;
                    Console.WriteLine($"Nuova ImgUrl: {postToUpdate.ImgUrl}");
                }
                else if (!keepCurrentImage)
                {
                    // L'utente non vuole mantenere l'immagine attuale
                    Console.WriteLine("Rimozione immagine");
                    postToUpdate.ImgUrl = null;
                }
                else
                {
                    // L'utente vuole mantenere l'immagine attuale
                    Console.WriteLine($"Mantengo immagine esistente: {postToUpdate.ImgUrl ?? "nessuna"}");
                }

                // Aggiorna il post nel database
                _context.Entry(postToUpdate).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                Console.WriteLine("Post aggiornato con successo");
                SuccessMessage = "Post modificato con successo!";

                // Redirect alla homepage appropriata
                if (User.IsInRole("Premium") || User.IsInRole("Admin"))
                {
                    return RedirectToPage("/User/Premium/HomePremium");
                }
                else
                {
                    return RedirectToPage("/User/HomeUser");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore durante modifica post: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                ErrorMessage = $"Si è verificato un errore durante la modifica del post: {ex.Message}";
                return Page();
            }
        }
    }
}