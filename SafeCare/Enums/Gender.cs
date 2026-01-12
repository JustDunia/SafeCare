using System.ComponentModel.DataAnnotations;

namespace SafeCare.Enums
{
    public enum Gender
    {
        [Display(Name = "Mężczyzna")]
        Male,

        [Display(Name = "Kobieta")]
        Female,

        [Display(Name = "Nie chcę podawać")]
        NotProvided,
    }
}
