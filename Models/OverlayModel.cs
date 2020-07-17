using System.ComponentModel.DataAnnotations;

namespace TCAdminServerBanner.Models
{
    public class OverlayModel : BannerObjectModel
    {
        [Required] public string Url { get; set; } = "Watermark";

        [Required] public int PositionX { get; set; } = 5;

        [Required] public int PositionY { get; set; } = 5;
    }
}