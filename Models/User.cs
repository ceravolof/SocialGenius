using System.ComponentModel.DataAnnotations;

namespace SocialGenius.Models
{
    public class User
    {
        [Key]
        public int IdPost { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string email { get; set; }

        [Required]
        [Display(Name = "Nome utente")]
        public string username { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string password { get; set; }

        [Required]
        public string ruolo { get; set; }

    }
}
