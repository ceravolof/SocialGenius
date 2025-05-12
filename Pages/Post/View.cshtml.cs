using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SocialGenius.Data;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace SocialGenius.Pages.Post
{
    [Authorize]
    public class ViewModel : PageModel
    {
        private readonly AppDbContext _context;

        public Models.Post Post { get; set; }
        public string UserName { get; set; }
        public List<string> Hashtags { get; set; }
        public bool UserCanEdit { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        [TempData]
        public string SuccessMessage { get; set; }

        public ViewModel(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var post = await _context.Posts
                .Where(p => p.IdPost == id)
                .FirstOrDefaultAsync();

            if (post == null)
            {
                return NotFound();
            }

            // Carica i dati dell'utente che ha creato il post
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == post.UserId);
            UserName = user?.UserName ?? "Utente sconosciuto";

            // Estrai gli hashtag dal contenuto
            Hashtags = ExtractHashtags(post.Content);

            // Imposta i dati
            Post = post;

            // Controlla se l'utente corrente può modificare il post
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            UserCanEdit = post.UserId == currentUserId || User.IsInRole("Admin");

            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var post = await _context.Posts.FindAsync(id);

            if (post == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (post.UserId != userId && !User.IsInRole("Admin"))
            {
                ErrorMessage = "Non hai il permesso di eliminare questo post";
                return RedirectToPage(User.IsInRole("Premium") ? "/User/Premium/HomePremium" : "/User/HomeUser");
            }

            try
            {
                // Elimina l'immagine se esiste e non è un'immagine di default
                if (!string.IsNullOrEmpty(post.ImgUrl) && post.ImgUrl.StartsWith("/uploads/"))
                {
                    var imagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", post.ImgUrl.TrimStart('/'));
                    if (System.IO.File.Exists(imagePath))
                    {
                        try
                        {
                            System.IO.File.Delete(imagePath);
                        }
                        catch
                        {
                            // Ignora errori durante l'eliminazione dell'immagine
                        }
                    }
                }

                _context.Posts.Remove(post);
                await _context.SaveChangesAsync();

                SuccessMessage = "Post eliminato con successo!";
                return RedirectToPage(User.IsInRole("Premium") ? "/User/Premium/HomePremium" : "/User/HomeUser");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Si è verificato un errore durante l'eliminazione: {ex.Message}";
                return RedirectToPage("/Post/View", new { id });
            }
        }

        private List<string> ExtractHashtags(string content)
        {
            if (string.IsNullOrEmpty(content))
                return new List<string>();

            var hashtags = new List<string>();
            var regex = new Regex(@"#(\w+)");
            var matches = regex.Matches(content);

            foreach (Match match in matches)
            {
                hashtags.Add(match.Value);
            }

            return hashtags;
        }
    }
}