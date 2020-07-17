using System.ComponentModel.DataAnnotations;

namespace TCAdminServerBanner.Models
{
    public class WatermarkModel : BannerObjectModel
    {
        [Required] public string Text { get; set; } = "Watermark";

        [Required] public int PositionX { get; set; } = 5;

        [Required] public int PositionY { get; set; } = 5;

        [Required] public int FontSize { get; set; } = 12;

        [Required] public string Color { get; set; } = "#000";
    }
}