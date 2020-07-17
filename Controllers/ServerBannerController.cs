using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using ImageProcessor;
using ImageProcessor.Imaging;
using Newtonsoft.Json;
using TCAdmin.SDK.Objects;
using TCAdmin.SDK.Web.MVC.Controllers;
using TCAdminServerBanner.Extensions;
using TCAdminServerBanner.HttpResponses;
using TCAdminServerBanner.Models;
using Service = TCAdmin.GameHosting.SDK.Objects.Service;

namespace TCAdminServerBanner.Controllers
{
    public class ServerBannerController : BaseController
    {
        public ServerBannerController()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }

        [HttpGet]
        [ParentAction("Service", "Home")]
        // [AllowAnonymous]
        public ActionResult Index(int id)
        {
            try
            {
                ObjectBase.GlobalSkipSecurityCheck = true;
                var skipCache = bool.Parse(Request.QueryString["SkipCache"]);
                var service = new Service(id);
                var game = new TCAdmin.GameHosting.SDK.Objects.Game(service.GameId);
                var settings = GetSettingsForGame(game);
                var cachedFile = new FileInfo(Path.Combine(TCAdmin.SDK.Utility.GetTempPath(),
                    $"__BannerModule.Service.{service.ServiceId}.jpg"));
                var cachedTime = game.QueryMonitoring.QueryInterval;
                if (skipCache && cachedFile.Exists)
                {
                    cachedFile.Delete();
                }
                else
                {
                    if (cachedFile.Exists)
                    {
                        if (DateTime.Now - cachedFile.CreationTime < TimeSpan.FromSeconds(cachedTime))
                        {
                            return File(cachedFile.FullName, "image/jpeg");
                        }
                        else
                        {
                            cachedFile.Delete();
                        }
                    }
                }

                using (var imageFactory = new ImageFactory(true))
                {
                    var image = GenerateImageSize(settings.GeneralSettingsModel.SizeX,
                        settings.GeneralSettingsModel.SizeY);

                    imageFactory.Load(image);
                    var dimensions = imageFactory.Image.PhysicalDimension;
                    foreach (var overlay in settings.Overlays.OrderBy(x => x.ViewOrder))
                    {
                        var overlayImage = ImageFromUrl(overlay.Url);
                        overlayImage = ResizeImage(overlayImage, (int) dimensions.Width, (int) dimensions.Height,
                            ResizeMode.Stretch);
                        overlay.PositionX = 0;
                        overlay.PositionY = 0;
                        imageFactory.Overlay(new ImageLayer
                        {
                            Image = overlayImage,
                            Opacity = 100,
                            Position = new Point(overlay.PositionX, overlay.PositionY),
                        });
                    }

                    foreach (var parsedWatermark in settings.Watermarks.OrderBy(x => x.ViewOrder).Select(watermark =>
                        watermark.ReplaceWithContext(service)))
                    {
                        imageFactory.Watermark(new TextLayer
                        {
                            Position = new Point(parsedWatermark.PositionX, parsedWatermark.PositionY),
                            Text = parsedWatermark.Text,
                            FontSize = parsedWatermark.FontSize,
                            Style = FontStyle.Bold,
                            FontColor = ColorTranslator.FromHtml(parsedWatermark.Color),
                        });
                    }

                    var tempFileName = Path.GetTempFileName();
                    imageFactory.Save(tempFileName);
                    System.IO.File.Copy(tempFileName, cachedFile.FullName);
                    return File(tempFileName, "image/jpeg");
                }
            }
            finally
            {
                ObjectBase.GlobalSkipSecurityCheck = false;
            }
        }

        [ParentAction("Game", "Edit")]
        public ActionResult Settings(int id)
        {
            var game = new TCAdmin.GameHosting.SDK.Objects.Game(id);
            ViewData.Add("gameId", id);

            var model = new SettingsModel
            {
                GameId = game.GameId,
                Watermarks = GetSettingsForGame(game).Watermarks
            };
            return View(model);
        }

        [ParentAction("Game", "Edit")]
        public ActionResult GeneralSettings(int id)
        {
            var game = new TCAdmin.GameHosting.SDK.Objects.Game(id);
            ViewData.Add("gameId", id);

            var settings = GetSettingsForGame(game);
            return PartialView("_GeneralSettings", settings.GeneralSettingsModel);
        }

        [ParentAction("Game", "Edit")]
        [HttpPost]
        public ActionResult GeneralSettings(int id, GeneralSettingsModel model)
        {
            var game = new TCAdmin.GameHosting.SDK.Objects.Game(id);

            var settings = GetSettingsForGame(game);
            settings.GeneralSettingsModel = model;
            SaveBannerSettingsToGame(game, settings);

            return new JsonHttpStatusResult(new
            {
                Message = "Saved Successfully"
            }, HttpStatusCode.OK);
        }

        [ParentAction("Game", "Edit")]
        public ActionResult WatermarkSettings(int id, int watermarkId)
        {
            var game = new TCAdmin.GameHosting.SDK.Objects.Game(id);
            ViewData.Add("gameId", id);

            var settings = GetSettingsForGame(game);
            var watermark = settings.Watermarks.FirstOrDefault(x => x.BannerObjectId == watermarkId);
            return PartialView("_UpdateWatermark", watermark);
        }

        [HttpPost]
        public ActionResult WatermarkSettings(int id, WatermarkModel model)
        {
            var game = new TCAdmin.GameHosting.SDK.Objects.Game(id);

            if (!ModelState.IsValid)
            {
                return new JsonHttpStatusResult(new
                {
                    Message = "Cannot save - Model state invalid."
                }, HttpStatusCode.BadRequest);
            }

            var settings = GetSettingsForGame(game);
            settings.Watermarks.Add(model);
            SaveBannerSettingsToGame(game, settings);

            return new JsonHttpStatusResult(new
            {
                Message = "Saved Successfully"
            }, HttpStatusCode.OK);
        }

        [ParentAction("Game", "Edit")]
        public ActionResult OverlaySettings(int id, int overlayId)
        {
            var game = new TCAdmin.GameHosting.SDK.Objects.Game(id);
            ViewData.Add("gameId", id);

            var settings = GetSettingsForGame(game);
            if (settings.Overlays == null || settings.Overlays.Count <= 0)
            {
                return PartialView("_UpdateOverlay", new OverlayModel()
                {
                    BannerObjectId = 1,
                    Url = "",
                    PositionX = 5,
                    PositionY = 5,
                });
            }

            var overlay = settings.Overlays.FirstOrDefault(x => x.BannerObjectId == overlayId);
            return PartialView("_UpdateOverlay", overlay);
        }

        [HttpPost]
        public ActionResult OverlaySettings(int id, OverlayModel model)
        {
            var game = new TCAdmin.GameHosting.SDK.Objects.Game(id);

            if (!ModelState.IsValid)
            {
                return new JsonHttpStatusResult(new
                {
                    Message = "Cannot save - Model state invalid."
                }, HttpStatusCode.BadRequest);
            }

            var settings = GetSettingsForGame(game);
            settings.Overlays.Add(model);
            SaveBannerSettingsToGame(game, settings);

            return new JsonHttpStatusResult(new
            {
                Message = "Saved Successfully"
            }, HttpStatusCode.OK);
        }

        public ActionResult MiscellaneousSettings(int id)
        {
            ViewData.Add("gameId", id);

            return PartialView("_Miscellaneous");
        }

        [HttpPost]
        public ActionResult AddWatermark(int id, string text)
        {
            var game = new TCAdmin.GameHosting.SDK.Objects.Game(id);
            var settings = GetSettingsForGame(game);
            settings.Watermarks.Add(new WatermarkModel
            {
                BannerObjectId =
                    settings.Watermarks.OrderByDescending(x => x.BannerObjectId).ToList()[0].BannerObjectId + 1,
                Text = text
            });
            SaveBannerSettingsToGame(game, settings);
            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }

        [HttpPost]
        public ActionResult DeleteWatermark(int id, int bannerObjectId)
        {
            var game = new TCAdmin.GameHosting.SDK.Objects.Game(id);
            var settings = GetSettingsForGame(game);
            settings.Watermarks.RemoveAll(x => x.BannerObjectId == bannerObjectId);
            SaveBannerSettingsToGame(game, settings);
            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }

        [HttpPost]
        public ActionResult AddOverlay(int id, string url)
        {
            var game = new TCAdmin.GameHosting.SDK.Objects.Game(id);
            var settings = GetSettingsForGame(game);
            settings.Overlays.Add(new OverlayModel
            {
                BannerObjectId = settings.Overlays.OrderByDescending(x => x.BannerObjectId).ToList()[0].BannerObjectId +
                                 1,
                Url = url
            });
            SaveBannerSettingsToGame(game, settings);
            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }

        [HttpPost]
        public ActionResult DeleteOverlay(int id, int bannerObjectId)
        {
            var game = new TCAdmin.GameHosting.SDK.Objects.Game(id);
            var settings = GetSettingsForGame(game);
            settings.Overlays.RemoveAll(x => x.BannerObjectId == bannerObjectId);
            SaveBannerSettingsToGame(game, settings);
            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }

        [HttpPost]
        public ActionResult CopyConfig(int id, int fromGameId = 0)
        {
            try
            {
                var game = new TCAdmin.GameHosting.SDK.Objects.Game(id);
                var fromGame = new TCAdmin.GameHosting.SDK.Objects.Game(fromGameId);

                if (fromGame.CustomFields["BannerModule:Settings"] != null)
                {
                    var fromGameSettings =
                        JsonConvert.DeserializeObject<SettingsModel>(fromGame.CustomFields["BannerModule:Settings"]
                            .ToString());

                    SaveBannerSettingsToGame(game, fromGameSettings);

                    return JavaScript("window.location.reload(false);");
                }

                return Json(new
                {
                    Message = "Could not parse config from " + fromGame.Name
                });
            }
            catch (Exception e)
            {
                return Json(new
                {
                    Message = "Could not copy configuration - " + e.Message
                });
            }
        }

        [HttpPost]
        public ActionResult DeleteConfig(int id)
        {
            var game = new TCAdmin.GameHosting.SDK.Objects.Game(id);
            game.CustomFields["BannerModule:Settings"] = null;
            game.Save();

            return JavaScript("window.location.reload(false);");
        }

        [HttpPost]
        public ActionResult ClearCache()
        {
            var tempDirectory = new DirectoryInfo(TCAdmin.SDK.Utility.GetTempPath());
            var bannerFiles = tempDirectory.GetFiles("__BannerModule*.jpg");
            foreach (var fileInfo in bannerFiles)
            {
                fileInfo.Delete();
            }

            return Json(new
            {
                Message = $"Successfully cleared {bannerFiles.Length} cached files."
            });
        }

        public static List<WatermarkModel> GetWatermarksForGame(int id)
        {
            var settings = GetSettingsForGame(new TCAdmin.GameHosting.SDK.Objects.Game(id));
            return settings.Watermarks;
        }

        public static List<OverlayModel> GetOverlaysForGame(int id)
        {
            var settings = GetSettingsForGame(new TCAdmin.GameHosting.SDK.Objects.Game(id));
            return settings.Overlays;
        }

        public static List<TCAdmin.GameHosting.SDK.Objects.Game> GetGamesWithConfig()
        {
            var games = TCAdmin.GameHosting.SDK.Objects.Game.GetGames()
                .Cast<TCAdmin.GameHosting.SDK.Objects.Game>().ToList()
                .Where(x => x.CustomFields["BannerModule:Settings"] != null).ToList();

            return games;
        }

        public static SettingsModel GetSettingsForGame(TCAdmin.GameHosting.SDK.Objects.Game game)
        {
            var bannerSettings = game.CustomFields["BannerModule:Settings"];
            if (bannerSettings == null)
            {
                return new SettingsModel
                {
                    GeneralSettingsModel = new GeneralSettingsModel(),
                    Watermarks = GenerateDefaultWatermarks(),
                    Overlays = new List<OverlayModel>(),
                    GameId = game.GameId
                };
            }

            return JsonConvert.DeserializeObject<SettingsModel>(bannerSettings.ToString(), new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Populate,
            });
        }

        private static void SaveBannerSettingsToGame(TCAdmin.GameHosting.SDK.Objects.Game game,
            SettingsModel settingsModel)
        {
            var bannerSettings = game.CustomFields["BannerModule:Settings"];
            if (bannerSettings == null)
            {
                game.CustomFields["BannerModule:Settings"] = JsonConvert.SerializeObject(settingsModel);
                game.Save();
            }

            bannerSettings = game.CustomFields["BannerModule:Settings"];
            var settings = JsonConvert.DeserializeObject<SettingsModel>(bannerSettings.ToString(),
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Include,
                });

            if (settings == null)
            {
                throw new NullReferenceException("Cannot parse Settings variable for Game.");
            }

            if (settingsModel.GeneralSettingsModel != null)
            {
                settings.GeneralSettingsModel = settingsModel.GeneralSettingsModel;
            }

            if (settingsModel.Watermarks != null)
            {
                foreach (var settingsWatermark in settingsModel.Watermarks)
                {
                    if (settings.Watermarks.Any(x => x.BannerObjectId == settingsWatermark.BannerObjectId))
                    {
                        settings.Watermarks[
                            settings.Watermarks.FindIndex(ind =>
                                ind.BannerObjectId.Equals(settingsWatermark.BannerObjectId))] = settingsWatermark;
                    }
                    else
                    {
                        settings.Watermarks.Add(settingsWatermark);
                    }
                }

                var watermarksToDelete = settings.Watermarks.Except(settingsModel.Watermarks).ToList();
                foreach (var watermarkModel in watermarksToDelete)
                {
                    settings.Watermarks.Remove(watermarkModel);
                }
            }

            if (settingsModel.Overlays != null)
            {
                foreach (var settingsOverlay in settingsModel.Overlays)
                {
                    if (settings.Overlays.Any(x => x.BannerObjectId == settingsOverlay.BannerObjectId))
                    {
                        settings.Overlays[
                            settings.Overlays.FindIndex(ind =>
                                ind.BannerObjectId.Equals(settingsOverlay.BannerObjectId))] = settingsOverlay;
                    }
                    else
                    {
                        settings.Overlays.Add(settingsOverlay);
                    }
                }

                var overlaysToDelete = settings.Overlays.Except(settingsModel.Overlays).ToList();
                foreach (var watermarkModel in overlaysToDelete)
                {
                    settings.Overlays.Remove(watermarkModel);
                }
            }

            game.CustomFields["BannerModule:Settings"] = JsonConvert.SerializeObject(settings);
            game.Save();
        }

        private static List<WatermarkModel> GenerateDefaultWatermarks()
        {
            var watermarks = new List<WatermarkModel>
            {
                new WatermarkModel()
                {
                    BannerObjectId = 1,
                    PositionX = 5,
                    PositionY = 5,
                    Text = $"$[Service.NameNoHtml]",
                },
                new WatermarkModel()
                {
                    BannerObjectId = 2,
                    PositionX = 10,
                    PositionY = 35,
                    Text = $"Players: $[Service.CurrentPlayers]/$[Service.CurrentMaxPlayers]",
                }
            };

            return watermarks;
        }

        public static Image GenerateImageSize(int x, int y, string text = "Module by Alexr03")
        {
            return ImageFromUrl($"https://via.placeholder.com/{x}x{y}?text={text.Replace(" ", "%20")}");
        }

        private static Image ImageFromUrl(string url)
        {
            using (var wc = new WebClient())
            {
                var bytes = wc.DownloadData(url);
                using (var stream = new MemoryStream(bytes))
                {
                    return Image.FromStream(stream);
                }
            }
        }

        private static Image ResizeImage(Image image, int x, int y, ResizeMode resizeMode)
        {
            try
            {
                using (var imageFactory = new ImageFactory(true))
                {
                    using (var stream = new MemoryStream())
                    {
                        imageFactory.Load(image);
                        imageFactory.Resize(new ResizeLayer(new Size(x, y), resizeMode));
                        imageFactory.Save(stream);
                        return Image.FromStream(stream);
                    }
                }
            }
            catch
            {
                return image;
            }
        }
    }
}