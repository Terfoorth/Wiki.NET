using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using Wiki_Blaze.Data.Entities;

namespace Wiki_Blaze.Data
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        public const string DefaultThemePreference = "system";
        public const string DefaultDensityPreference = "comfortable";
        public const string DefaultStartPage = "/";
        public const string DefaultPreferredLanguage = "en";

        [PersonalData]
        [MaxLength(100)]
        public string? FirstName { get; set; }

        [PersonalData]
        [MaxLength(100)]
        public string? LastName { get; set; }

        [PersonalData]
        [MaxLength(150)]
        public string? DisplayName { get; set; }

        [PersonalData]
        [MaxLength(150)]
        public string? JobTitle { get; set; }

        [PersonalData]
        [MaxLength(150)]
        public string? Department { get; set; }

        [PersonalData]
        [MaxLength(150)]
        public string? Location { get; set; }

        [PersonalData]
        [MaxLength(100)]
        public string? Country { get; set; }

        [PersonalData]
        [MaxLength(500)]
        public string? Bio { get; set; }

        [PersonalData]
        [MaxLength(80)]
        public string? PreferredLanguage { get; set; }

        [PersonalData]
        [MaxLength(120)]
        public string? TimeZone { get; set; }

        [MaxLength(20)]
        public string? ThemePreference { get; set; }

        [MaxLength(20)]
        public string? DensityPreference { get; set; }

        [MaxLength(80)]
        public string? StartPage { get; set; }

        public bool ReceiveProductUpdates { get; set; } = true;

        public bool ReceiveWeeklyDigest { get; set; }

        public ICollection<WikiPage> OwnedEntries { get; set; } = new List<WikiPage>();

    }

}
