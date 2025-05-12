using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using SocialGenius.Data;
using SocialGenius.Models;
using SocialGenius.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace SocialGenius.Pages.Post
{
    [Authorize]
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly GeminiService _geminiService;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<CreateModel> _logger;

        [BindProperty]
        public PostInputModel PostInput { get; set; } = new PostInputModel();

        [TempData]
        public string ErrorMessage { get; set; }

        [TempData]
        public string SuccessMessage { get; set; }

        public CreateModel(AppDbContext context, GeminiService geminiService, IWebHostEnvironment environment, ILogger<CreateModel> logger)
        {
            _context = context;
            _geminiService = geminiService;
            _environment = environment;
            _logger = logger;
        }

        public IActionResult OnGet()
        {
            // Verifica limiti per utenti non premium
            if (!User.IsInRole("Premium") && !User.IsInRole("Admin"))
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var today = DateTime.UtcNow.Date;
                var tomorrow = today.AddDays(1);

                var postsToday = _context.Posts
                    .Count(p => p.UserId == userId && p.DateCreate >= today && p.DateCreate < tomorrow);

                if (postsToday >= 3)
                {
                    TempData["ErrorMessage"] = "Hai raggiunto il limite giornaliero di 3 post. Passa a Premium per creare post illimitati.";
                    return RedirectToPage("/User/HomeUser");
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            ModelState.Remove("PostInput.ImageFile");
            try
            {
                _logger.LogInformation("OnPostAsync chiamato - Inizio elaborazione");

                // Rimuovi la validazione ModelState per ImageFile
                ModelState.Remove("PostInput.ImageFile");

                // Verifica se una piattaforma è stata selezionata
                if (string.IsNullOrEmpty(PostInput.SocialPlatform))
                {
                    ModelState.AddModelError("PostInput.SocialPlatform", "Seleziona una piattaforma");
                    return Page();
                }

                // Verifica se c'è un contenuto
                if (string.IsNullOrEmpty(PostInput.Content))
                {
                    ModelState.AddModelError("PostInput.Content", "Il contenuto è obbligatorio");
                    return Page();
                }

                if (!ModelState.IsValid)
                {
                    foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                    {
                        _logger.LogWarning("Errore di validazione: {ErrorMessage}", error.ErrorMessage);
                    }
                    return Page();
                }

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogError("UserId non trovato");
                    ModelState.AddModelError("", "Impossibile identificare l'utente corrente");
                    return Page();
                }

                // Verifica limiti per utenti non premium
                if (!User.IsInRole("Premium") && !User.IsInRole("Admin"))
                {
                    var today = DateTime.UtcNow.Date;
                    var tomorrow = today.AddDays(1);
                    var postsToday = _context.Posts
                        .Count(p => p.UserId == userId && p.DateCreate >= today && p.DateCreate < tomorrow);

                    if (postsToday >= 3)
                    {
                        TempData["ErrorMessage"] = "Hai raggiunto il limite giornaliero di 3 post. Passa a Premium per creare post illimitati.";
                        return RedirectToPage("/User/HomeUser");
                    }
                }

                // Gestione dell'immagine
                string imgUrl = null;

                if (PostInput.ImageFile != null && PostInput.ImageFile.Length > 0)
                {
                    _logger.LogInformation("Elaborazione file immagine: {FileName}, {FileSize} bytes",
                        PostInput.ImageFile.FileName, PostInput.ImageFile.Length);

                    string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" +
                        Path.GetFileName(PostInput.ImageFile.FileName);
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await PostInput.ImageFile.CopyToAsync(fileStream);
                    }

                    imgUrl = "/uploads/" + uniqueFileName;
                }
                else if (!string.IsNullOrEmpty(PostInput.ImgUrl) && PostInput.ImgUrl.StartsWith("data:image"))
                {
                    _logger.LogInformation("Elaborazione immagine base64");

                    // Estrai i dati base64
                    var base64Data = PostInput.ImgUrl.Split(',')[1];
                    var imageBytes = Convert.FromBase64String(base64Data);

                    string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    string uniqueFileName = Guid.NewGuid().ToString() + ".png";
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);

                    imgUrl = "/uploads/" + uniqueFileName;
                }


                // Creazione del nuovo post
                var newPost = new Models.Post
                {
                    Content = PostInput.Content,
                    ImgUrl = imgUrl,
                    ImgSource = "upload", // Sempre "upload" poiché è l'unica opzione rimasta
                    SocialPlatform = PostInput.SocialPlatform,
                    DateCreate = DateTime.UtcNow,
                    UserId = userId
                };

                _logger.LogInformation("Aggiunta del post al database");
                _context.Posts.Add(newPost);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Post salvato con successo!";

                // Redirect alla home page appropriata in base al ruolo
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
                // Log dell'errore e visualizzazione messaggio utente
                Console.WriteLine($"Errore durante il salvataggio del post: {ex.Message}");
                _logger.LogError(ex, "Errore durante il salvataggio del post");
                ModelState.AddModelError("", "Si è verificato un errore durante il salvataggio del post. Riprova più tardi.");
                return Page();
            }
        }

        public async Task<IActionResult> OnPostGenerateContentAsync(string platform, List<string> interests, string imgDescription)
        {
            if (interests == null || !interests.Any())
            {
                return new JsonResult("Seleziona almeno un tema di interesse") { StatusCode = 400 };
            }

            var generatedContent = await _geminiService.GenerateContentAsync(
                platform,
                interests,
                null); // Rimosso il riferimento alla descrizione dell'immagine

            return new JsonResult(generatedContent);
        }
    }

    public class PostInputModel
    {
        [Required(ErrorMessage = "Il contenuto è obbligatorio")]
        public string Content { get; set; }

        [BindNever]
        public IFormFile ImageFile { get; set; }
        public string ImgUrl { get; set; }
        public string ImgSource { get; set; } = "upload";

        [Required(ErrorMessage = "Seleziona una piattaforma")]
        public string SocialPlatform { get; set; } = "";
    }
}