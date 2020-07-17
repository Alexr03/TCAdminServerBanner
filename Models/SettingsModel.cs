using System.Collections.Generic;

namespace TCAdminServerBanner.Models
{
    public class SettingsModel
    {
        public int GameId { get; set; }
        public GeneralSettingsModel GeneralSettingsModel { get; set; }
        public List<WatermarkModel> Watermarks { get; set; }
        public List<OverlayModel> Overlays { get; set; }
    }
}