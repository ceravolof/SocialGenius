using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using SocialGenius.Data;
using SocialGenius.Models;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;


namespace SocialGenius.Pages.User
{
    [Authorize(Roles = "Premium,Admin")]
    public class HomePremiumModel : PageModel
    {
        private readonly AppDbContext _context;
        private const int PageSize = 9;

        public HomePremiumModel(AppDbContext context)
        {
            _context = context;
        }

        public List<SocialGenius.Models.Post> Posts { get; set; } = new List<SocialGenius.Models.Post>();
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public string CurrentSearchTerm { get; set; } = string.Empty;
        public string CurrentPlatform { get; set; } = string.Empty;
        public int TotalPosts { get; set; }
        public int PostsCreatedToday { get; set; }
        public int InstagramPosts { get; set; }
        public int FacebookPosts { get; set; }

        public async Task<IActionResult> OnGetAsync(int page = 1, string searchTerm = "", string platform = "")
        {
            CurrentPage = page < 1 ? 1 : page;
            CurrentSearchTerm = searchTerm ?? string.Empty;
            CurrentPlatform = platform ?? string.Empty;

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Carica statistiche
            await LoadStatisticsAsync(userId);

            var query = _context.Posts.AsQueryable()
                .Where(p => p.UserId == userId);

            // Applica filtri
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(p => p.Content.Contains(searchTerm));
            }

            if (!string.IsNullOrWhiteSpace(platform))
            {
                query = query.Where(p => p.SocialPlatform == platform);
            }

            // Calcola il totale e imposta la paginazione
            var totalPosts = await query.CountAsync();
            TotalPages = (int)Math.Ceiling(totalPosts / (double)PageSize);

            Posts = await query
                .OrderByDescending(p => p.DateCreate)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int postId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var post = await _context.Posts
                .FirstOrDefaultAsync(p => p.IdPost == postId && p.UserId == userId);

            if (post == null)
            {
                return NotFound();
            }

            _context.Posts.Remove(post);
            await _context.SaveChangesAsync();

            return RedirectToPage();
        }

        private async Task LoadStatisticsAsync(string userId)
        {
            // Ottieni statistiche totali
            TotalPosts = await _context.Posts
                .CountAsync(p => p.UserId == userId);

            // Post creati oggi
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            PostsCreatedToday = await _context.Posts
                .CountAsync(p => p.UserId == userId &&
                           p.DateCreate >= today &&
                           p.DateCreate < tomorrow);

            // Statistiche per piattaforma
            InstagramPosts = await _context.Posts
                .CountAsync(p => p.UserId == userId &&
                           p.SocialPlatform == "Instagram");

            FacebookPosts = await _context.Posts
                .CountAsync(p => p.UserId == userId &&
                           p.SocialPlatform == "Facebook");
        }
    }
}