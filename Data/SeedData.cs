using System.Diagnostics;
using SocialGenius.Models;

namespace SocialGenius.Data
{
    public static class SeedData
    {
        public static void Initialize(AppDbContext context)
        {
            //if (context.eleAttivita.Any()) return;

            context.EleUser.AddRange(
                new User
                {
                    IdPost = 1,
                    email = "test@example.com",
                    username = "user1",
                    password = "password1"
                },
                 new User
                 {
                     IdPost = 2,
                     email = "admin@example.com",
                     username = "admin",
                     password = "password2"
                 },
                new User
                {
                    IdPost = 3,
                    email = "premium@example.com",
                    username = "premiumUser",
                    password = "password3"
                });
        }
    }
}
