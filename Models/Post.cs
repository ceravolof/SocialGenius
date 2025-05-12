using System.ComponentModel.DataAnnotations;

namespace SocialGenius.Models
{
    public class Post
    {
        [Key]
        public int IdPost { get; set; }

        [Required]
        public string Content { get; set; }

        // URL dell'immagine (caricata o scelta)
        public string? ImgUrl { get; set; }
        public string? ImgSource { get; set; }
        public string? SocialPlatform{get; set; }

        public DateTime DateCreate { get; set; } = DateTime.UtcNow;

        // ID dell'utente che ha creato il post
        [Required]
        public string UserId { get; set; }
    }
}