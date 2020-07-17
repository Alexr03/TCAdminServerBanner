using TCAdmin.GameHosting.SDK.Objects;
using TCAdminServerBanner.Models;

namespace TCAdminServerBanner.Extensions
{
    public static class Extensions
    {
        public static WatermarkModel ReplaceWithContext(this WatermarkModel textLayer, Service service)
        {
            var input = new TCAdmin.SDK.Scripting.InputParser(textLayer.Text);
            service?.ReplacePropertyValues(input);
            textLayer.Text = input.GetOutput();

            return textLayer;
        }
    }
}