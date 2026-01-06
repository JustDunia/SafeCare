using System.ComponentModel.DataAnnotations;

namespace SafeCare.Enums
{
    public enum IncidentCategory
    {
        [Display(Name = "Działalność kliniczna")]
        Clinical,

        [Display(Name = "Farmakoterapia")]
        Pharmacotherapy,

        [Display(Name = "Przetaczanie krwi i jej składników")]
        Transfusion,

        [Display(Name = "Zdarzenia dotyczące sprzętu medycznego, wyposażenia, organizacji pracy")]
        Operational,

        [Display(Name = "Inne")]
        Other
    }
}
