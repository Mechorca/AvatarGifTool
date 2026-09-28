using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using WzComparerR2;
using WzComparerR2.AvatarCommon;
using WzComparerR2.CharaSim;
using WzComparerR2.Encoders;
using WzComparerR2.PluginBase;
using WzComparerR2.WzLib;

namespace AvatarGifTool
{
    internal static class Program
    {
        private const int PreferredDefaultSkinId = 2000;
        private static readonly object AppearanceNodeCacheLock = new object();
        private static readonly Dictionary<string, Wz_Node> AppearanceNodeCache = new Dictionary<string, Wz_Node>(StringComparer.OrdinalIgnoreCase);
        private static readonly string[] ActionStripActions = new[]
        {
            "stand1",
            "swingO1",
            "swingO2",
            "shoot1",
            "jump",
            "walk1",
        };
        private const int DyeHueColumnCount = 3;
        private static readonly (int SaturationOffset, int BrightnessOffset, string Key)[] DyeExtremeAdjustmentVariants = new[]
        {
            (-99, -99, "sat-99_bri-99"),
            (-99, 99, "sat-99_bri+99"),
            (99, -99, "sat+99_bri-99"),
            (99, 99, "sat+99_bri+99"),
        };
        private static readonly PrismColorTypeDefinition[] PrismColorTypeDefinitions = new[]
        {
            new PrismColorTypeDefinition(0, "整体色系", "全色系", "overall", "all"),
            new PrismColorTypeDefinition(1, "红色系", "red"),
            new PrismColorTypeDefinition(2, "黄色系", "yellow"),
            new PrismColorTypeDefinition(3, "绿色系", "green"),
            new PrismColorTypeDefinition(4, "祖母绿色系", "emerald"),
            new PrismColorTypeDefinition(5, "青色系", "cyan", "blue"),
            new PrismColorTypeDefinition(6, "紫色系", "purple"),
        };

        internal static IReadOnlyList<string> SupportedExportActions => ActionStripActions;

        internal static IReadOnlyList<PrismColorTypeDefinition> SupportedPrismColorTypes => PrismColorTypeDefinitions;

        internal static IReadOnlyList<string> SupportedHairMixColors => AvatarCanvas.HairColor;

        internal static IReadOnlyList<string> SupportedFaceMixColors => AvatarCanvas.FaceColor;

        internal static int NormalizePrismType(int prismType)
        {
            return PrismColorTypeDefinitions.Any(type => type.Value == prismType)
                ? prismType
                : 0;
        }

        internal static string GetPrismTypeName(int prismType)
        {
            return PrismColorTypeDefinitions.FirstOrDefault(type => type.Value == prismType)?.Name
                ?? PrismColorTypeDefinitions[0].Name;
        }

        internal static string[] NormalizeNormalActionSelection(IEnumerable<string> selectedActions)
        {
            var selectedSet = new HashSet<string>(
                (selectedActions ?? Enumerable.Empty<string>())
                    .Where(action => !string.IsNullOrWhiteSpace(action))
                    .Select(action => action.Trim()),
                StringComparer.OrdinalIgnoreCase);

            string[] normalized = ActionStripActions
                .Where(action => selectedSet.Contains(action))
                .ToArray();

            return normalized.Length > 0
                ? normalized
                : (string[])ActionStripActions.Clone();
        }

        internal static string NormalizeDyeActionSelection(string selectedAction)
        {
            if (!string.IsNullOrWhiteSpace(selectedAction))
            {
                string matchedAction = ActionStripActions.FirstOrDefault(action =>
                    string.Equals(action, selectedAction.Trim(), StringComparison.OrdinalIgnoreCase));
                if (matchedAction != null)
                {
                    return matchedAction;
                }
            }

            return ActionStripActions[0];
        }

        internal static string NormalizePreviewAction(string selectedAction)
        {
            if (!string.IsNullOrWhiteSpace(selectedAction))
            {
                string matchedAction = ActionStripActions.FirstOrDefault(action =>
                    string.Equals(action, selectedAction.Trim(), StringComparison.OrdinalIgnoreCase));
                if (matchedAction != null)
                {
                    return matchedAction;
                }
            }

            return ActionStripActions[0];
        }

        internal static string[] ParseActionList(string value, string fieldName)
        {
            string[] actions = Regex.Matches(value ?? string.Empty, @"[A-Za-z0-9]+")
                .Cast<Match>()
                .Select(match => match.Value)
                .ToArray();

            if (actions.Length == 0)
            {
                throw new FormatException($"{fieldName}里没有识别到任何动作。");
            }

            return actions;
        }

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Application.ThreadException += (_, e) => ErrorLog.Write(e.Exception, "UI ThreadException");
                AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                    ErrorLog.Write(e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString()), "AppDomain UnhandledException");
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
                return 0;
            }

            try
            {
                if (HasFlag(args, "--help", "-h", "/?"))
                {
                    Console.WriteLine(GetUsage());
                    return 0;
                }

                CommandOptions options = CommandOptions.Parse(args);
                string outputPath = Execute(options);
                Console.WriteLine($"Done: {outputPath}");
                return 0;
            }
            catch (Exception ex)
            {
                ErrorLog.Write(ex, "Program.Main");
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine();
                Console.Error.WriteLine(GetUsage());
                return 1;
            }
        }

        internal static string Execute(CommandOptions options, bool manageWzContext = true)
        {
            Run(options, manageWzContext);
            return NormalizeOutputPath(options.OutputPath);
        }

        internal static Bitmap RenderPreviewStand1(
            string templateText,
            string gearText,
            RenderMode renderMode,
            int hairMixColor,
            int hairMixOpacity,
            int faceMixColor,
            int faceMixOpacity,
            int prismType,
            int hue,
            int saturationOffset,
            int brightnessOffset)
        {
            using PreviewFrameSet frameSet = RenderPreviewStand1Frames(
                templateText,
                gearText,
                "stand1",
                renderMode,
                hairMixColor,
                hairMixOpacity,
                faceMixColor,
                faceMixOpacity,
                prismType,
                hue,
                saturationOffset,
                brightnessOffset);
            if (frameSet == null || frameSet.Count == 0)
            {
                return null;
            }

            return new Bitmap(frameSet.Frames[0]);
        }

        internal static PreviewFrameSet RenderPreviewStand1Frames(
            string templateText,
            string gearText,
            string actionName,
            RenderMode renderMode,
            int hairMixColor,
            int hairMixOpacity,
            int faceMixColor,
            int faceMixOpacity,
            int prismType,
            int hue,
            int saturationOffset,
            int brightnessOffset)
        {
            AvatarTemplate template = AvatarTemplate.Parse(templateText, DetectAppearanceIdKind);
            int[] gearIds = string.IsNullOrWhiteSpace(gearText)
                ? Array.Empty<int>()
                : ParseIdList(gearText, "Gear");

            using var avatar = new AvatarBuilder();

            avatar.AddBodyFromSkin(template.Skin);
            avatar.AddHairOrFace(template.Face);
            avatar.AddHairOrFace(template.Hair);

            foreach (int presetGearId in template.PresetGearIds)
            {
                avatar.AddGear(presetGearId, required: true);
            }

            var dyeTargetParts = new List<AvatarPart>(gearIds.Length);
            foreach (int gearId in gearIds)
            {
                AvatarPart targetPart = avatar.AddGear(gearId, required: true);
                if (targetPart != null)
                {
                    dyeTargetParts.Add(targetPart);
                }
            }

            avatar.ReloadEffects();
            avatar.ApplyCosmeticMix(
                hairMixColor,
                hairMixOpacity,
                faceMixColor,
                faceMixOpacity);

            if (renderMode == RenderMode.DyeGrid)
            {
                ApplyPrism(
                    dyeTargetParts,
                    prismType,
                    0,
                    ClampPrismValue(100 + saturationOffset),
                    ClampPrismValue(100 + brightnessOffset));
            }
            else if (renderMode == RenderMode.ExactDye)
            {
                ApplyPrism(
                    dyeTargetParts,
                    prismType,
                    hue,
                    ClampPrismValue(100 + saturationOffset),
                    ClampPrismValue(100 + brightnessOffset));
            }

            avatar.ClearSkinCache();
            string selectedAction = NormalizePreviewAction(actionName);
            string emotion = avatar.GetStandardEmotion();
            using RenderTrack track = RenderSingleTrack(avatar, selectedAction, emotion, selectedAction);
            if (track == null || track.Frames.Count == 0)
            {
                return null;
            }

            var frameSet = new PreviewFrameSet();
            foreach (RenderFrame frame in track.Frames)
            {
                if (frame?.Bitmap == null)
                {
                    continue;
                }

                frameSet.Frames.Add(new Bitmap(frame.Bitmap));
                frameSet.Delays.Add(Math.Max(10, frame.Delay));
            }

            return frameSet;
        }

        private static void Run(CommandOptions options, bool manageWzContext)
        {
            if (manageWzContext)
            {
                using var wzContext = new WzSearchContext(options.BaseWzPath);
                RunWithActiveContext(options);
                return;
            }

            RunWithActiveContext(options);
        }

        private static void RunWithActiveContext(CommandOptions options)
        {
            AvatarTemplate template = AvatarTemplate.Parse(options.Template, DetectAppearanceIdKind);

            using var avatar = new AvatarBuilder();

            avatar.AddBodyFromSkin(template.Skin);
            avatar.AddHairOrFace(template.Face);
            avatar.AddHairOrFace(template.Hair);

            foreach (int presetGearId in template.PresetGearIds)
            {
                avatar.AddGear(presetGearId, required: true);
            }

            var dyeTargetParts = new List<AvatarPart>(options.GearIds.Length);
            foreach (int gearId in options.GearIds)
            {
                AvatarPart targetPart = avatar.AddGear(gearId, required: true);
                if (targetPart != null)
                {
                    dyeTargetParts.Add(targetPart);
                }
            }

            avatar.ReloadEffects();
            avatar.ApplyCosmeticMix(
                options.HairMixColor,
                options.HairMixOpacity,
                options.FaceMixColor,
                options.FaceMixOpacity);

            if (RequiresDyeTarget(options.Mode) && dyeTargetParts.Count == 0)
            {
                throw new InvalidOperationException("dye mode requires a gear ID.");
            }

            List<RenderTrack> tracks = options.Mode switch
            {
                RenderMode.DyeGrid => RenderDyeTracks(avatar, dyeTargetParts, options),
                RenderMode.ExactDye => RenderExactDyeTracks(avatar, dyeTargetParts, options),
                _ => RenderActionTracks(avatar, options.NormalActions),
            };

            try
            {
                WriteGif(tracks, options);
            }
            finally
            {
                foreach (RenderTrack track in tracks)
                {
                    track.Dispose();
                }
            }
        }

        private static List<RenderTrack> RenderActionTracks(AvatarBuilder avatar, IReadOnlyList<string> actionNames)
        {
            string emotion = avatar.GetStandardEmotion();
            string[] selectedActions = NormalizeNormalActionSelection(actionNames).ToArray();
            var tracks = new List<RenderTrack>(selectedActions.Length);

            foreach (string actionName in selectedActions)
            {
                tracks.Add(RenderSingleTrack(avatar, actionName, emotion, actionName));
            }

            return tracks;
        }

        private static List<RenderTrack> RenderDyeTracks(AvatarBuilder avatar, IReadOnlyList<AvatarPart> targetParts, CommandOptions options)
        {
            string emotion = avatar.GetStandardEmotion();
            string dyeAction = NormalizeDyeActionSelection(options.DyeAction);
            var tracks = new List<RenderTrack>();
            int hueIndex = 0;

            for (int hue = 0; hue < 360; hue += options.HueStep)
            {
                ApplyPrism(targetParts, options.PrismType, hue, ClampPrismValue(100 + options.SaturationOffset), ClampPrismValue(100 + options.BrightnessOffset));
                avatar.ClearSkinCache();
                RenderTrack track = RenderSingleTrack(avatar, dyeAction, emotion, $"h{hue:000}");
                track.GridRow = hueIndex / DyeHueColumnCount;
                track.GridColumn = hueIndex % DyeHueColumnCount;
                tracks.Add(track);
                hueIndex++;
            }

            for (int i = 0; i < DyeExtremeAdjustmentVariants.Length; i++)
            {
                var variant = DyeExtremeAdjustmentVariants[i];
                ApplyPrism(
                    targetParts,
                    options.PrismType,
                    0,
                    ClampPrismValue(100 + variant.SaturationOffset),
                    ClampPrismValue(100 + variant.BrightnessOffset));
                avatar.ClearSkinCache();

                RenderTrack track = RenderSingleTrack(avatar, dyeAction, emotion, variant.Key);
                track.GridRow = i;
                track.GridColumn = DyeHueColumnCount;
                tracks.Add(track);
            }

            ApplyPrism(targetParts, options.PrismType, 0, 100, 100);
            avatar.ClearSkinCache();
            return tracks;
        }

        private static List<RenderTrack> RenderExactDyeTracks(AvatarBuilder avatar, IReadOnlyList<AvatarPart> targetParts, CommandOptions options)
        {
            ApplyPrism(
                targetParts,
                options.PrismType,
                options.Hue,
                ClampPrismValue(100 + options.SaturationOffset),
                ClampPrismValue(100 + options.BrightnessOffset));
            avatar.ClearSkinCache();

            try
            {
                return RenderActionTracks(avatar, options.NormalActions);
            }
            finally
            {
                ApplyPrism(targetParts, options.PrismType, 0, 100, 100);
                avatar.ClearSkinCache();
            }
        }

        private static int ClampPrismValue(int value)
        {
            return Math.Max(1, Math.Min(199, value));
        }

        private static void ApplyPrism(IReadOnlyList<AvatarPart> targetParts, int prismType, int hue, int saturation, int brightness)
        {
            if (targetParts == null || targetParts.Count == 0)
            {
                return;
            }

            foreach (AvatarPart targetPart in targetParts)
            {
                ApplyPrism(targetPart, prismType, hue, saturation, brightness);
            }
        }

        private static void ApplyPrism(AvatarPart targetPart, int prismType, int hue, int saturation, int brightness)
        {
            if (targetPart == null)
            {
                return;
            }

            bool applyWeaponEffect = IsWeaponAppearance(targetPart);

            if (hue == 0 && saturation == 100 && brightness == 100)
            {
                targetPart.PrismData.Clear(PrismDataCollection.PrismDataType.Default);
                if (applyWeaponEffect)
                {
                    targetPart.PrismData.Clear(PrismDataCollection.PrismDataType.WeaponEffect);
                }
                return;
            }

            targetPart.PrismData.Set(PrismDataCollection.PrismDataType.Default, prismType, hue, saturation, brightness);
            if (applyWeaponEffect)
            {
                targetPart.PrismData.Set(PrismDataCollection.PrismDataType.WeaponEffect, prismType, hue, saturation, brightness);
            }
        }

        private static bool IsWeaponAppearance(AvatarPart targetPart)
        {
            if (targetPart?.ID == null)
            {
                return false;
            }

            GearType type = Gear.GetGearType(targetPart.ID.Value);
            return Gear.IsWeapon(type)
                || type == GearType.cashWeapon
                || type == GearType.shovel
                || type == GearType.pickaxe;
        }

        private static RenderTrack RenderSingleTrack(AvatarBuilder avatar, string actionName, string emotionName, string key)
        {
            ActionFrame[] bodyFrames = avatar.GetActionFrames(actionName);
            if (bodyFrames == null || bodyFrames.Length == 0)
            {
                throw new InvalidOperationException($"Action '{actionName}' has no frames.");
            }

            ActionFrame[] faceFrames = avatar.GetFaceFrames(emotionName);
            ActionFrame[][] effectFrames = avatar.GetEffectFrames(actionName);
            int[] bodyDelays = bodyFrames
                .Select(frame => NormalizeFrameDelay(frame?.AbsoluteDelay ?? 0))
                .ToArray();
            int totalDuration = bodyDelays.Sum();
            if (totalDuration <= 0)
            {
                throw new InvalidOperationException($"Action '{actionName}' has no valid frame delays.");
            }

            var track = new RenderTrack
            {
                Key = key,
                Frames = new List<RenderFrame>(bodyFrames.Length),
            };

            Rectangle? bounds = null;
            int bodyFrameIndex = 0;
            int bodyRemaining = bodyDelays[0];
            int[] effectFrameIndexes = CreateEffectFrameIndexArray(effectFrames);
            int[] effectRemaining = CreateEffectRemainingArray(effectFrames);
            int elapsed = 0;

            while (elapsed < totalDuration)
            {
                int frameDelay = bodyRemaining;
                for (int i = 0; i < effectRemaining.Length; i++)
                {
                    if (effectFrameIndexes[i] >= 0)
                    {
                        frameDelay = Math.Min(frameDelay, effectRemaining[i]);
                    }
                }

                frameDelay = NormalizeFrameDelay(frameDelay);
                if (elapsed + frameDelay > totalDuration)
                {
                    frameDelay = totalDuration - elapsed;
                }

                ActionFrame faceAction = faceFrames != null && faceFrames.Length > 0 ? faceFrames[0] : null;
                BitmapOrigin bitmapOrigin = avatar.GetBitmapOrigin(
                    bodyFrames[bodyFrameIndex],
                    faceAction,
                    null,
                    BuildEffectFrameSelection(effectFrames, effectFrameIndexes));
                if (bitmapOrigin.Bitmap == null)
                {
                    AdvanceCompositeFrameState(bodyDelays, effectFrames, ref bodyFrameIndex, ref bodyRemaining, effectFrameIndexes, effectRemaining, frameDelay);
                    elapsed += frameDelay;
                    continue;
                }

                track.Frames.Add(new RenderFrame
                {
                    Bitmap = bitmapOrigin.Bitmap,
                    Origin = bitmapOrigin.Origin,
                    Delay = frameDelay,
                });

                Rectangle frameBounds = new Rectangle(-bitmapOrigin.Origin.X, -bitmapOrigin.Origin.Y, bitmapOrigin.Bitmap.Width, bitmapOrigin.Bitmap.Height);
                bounds = bounds.HasValue ? Rectangle.Union(bounds.Value, frameBounds) : frameBounds;

                AdvanceCompositeFrameState(bodyDelays, effectFrames, ref bodyFrameIndex, ref bodyRemaining, effectFrameIndexes, effectRemaining, frameDelay);
                elapsed += frameDelay;
            }

            if (track.Frames.Count == 0 || !bounds.HasValue)
            {
                throw new InvalidOperationException($"Action '{actionName}' rendered no bitmap frames.");
            }

            track.Bounds = bounds.Value;
            return track;
        }

        private static ActionFrame[] BuildEffectFrameSelection(ActionFrame[][] effectFrames, int[] effectFrameIndexes)
        {
            var selectedFrames = new ActionFrame[AvatarCanvas.LayerSlotLength];
            for (int i = 0; i < selectedFrames.Length; i++)
            {
                int frameIndex = effectFrameIndexes[i];
                if (frameIndex < 0)
                {
                    continue;
                }

                ActionFrame[] frames = effectFrames[i];
                if (frames == null || frameIndex >= frames.Length)
                {
                    continue;
                }

                selectedFrames[i] = frames[frameIndex];
            }

            return selectedFrames;
        }

        private static int NormalizeFrameDelay(int delay)
        {
            return delay > 0 ? delay : 30;
        }

        private static int[] CreateEffectFrameIndexArray(ActionFrame[][] effectFrames)
        {
            var indexes = new int[AvatarCanvas.LayerSlotLength];
            for (int i = 0; i < indexes.Length; i++)
            {
                indexes[i] = effectFrames[i]?.Length > 0 ? 0 : -1;
            }

            return indexes;
        }

        private static int[] CreateEffectRemainingArray(ActionFrame[][] effectFrames)
        {
            var remaining = new int[AvatarCanvas.LayerSlotLength];
            for (int i = 0; i < remaining.Length; i++)
            {
                if (effectFrames[i]?.Length > 0)
                {
                    remaining[i] = NormalizeFrameDelay(effectFrames[i][0]?.AbsoluteDelay ?? 0);
                }
            }

            return remaining;
        }

        private static void AdvanceCompositeFrameState(
            int[] bodyDelays,
            ActionFrame[][] effectFrames,
            ref int bodyFrameIndex,
            ref int bodyRemaining,
            int[] effectFrameIndexes,
            int[] effectRemaining,
            int elapsed)
        {
            bodyRemaining -= elapsed;
            while (bodyRemaining <= 0)
            {
                bodyFrameIndex = (bodyFrameIndex + 1) % bodyDelays.Length;
                bodyRemaining += NormalizeFrameDelay(bodyDelays[bodyFrameIndex]);
            }

            for (int i = 0; i < effectFrameIndexes.Length; i++)
            {
                if (effectFrameIndexes[i] < 0 || effectFrames[i] == null || effectFrames[i].Length == 0)
                {
                    continue;
                }

                effectRemaining[i] -= elapsed;
                while (effectRemaining[i] <= 0)
                {
                    effectFrameIndexes[i] = (effectFrameIndexes[i] + 1) % effectFrames[i].Length;
                    effectRemaining[i] += NormalizeFrameDelay(effectFrames[i][effectFrameIndexes[i]]?.AbsoluteDelay ?? 0);
                }
            }
        }

        private static void WriteGif(IReadOnlyList<RenderTrack> tracks, CommandOptions options)
        {
            if (tracks.Count == 0)
            {
                throw new InvalidOperationException("No frames were produced.");
            }

            bool useExplicitGrid = tracks.Any(track => track.GridRow.HasValue || track.GridColumn.HasValue);
            int columns = useExplicitGrid
                ? tracks.Max(track => track.GridColumn.GetValueOrDefault()) + 1
                : GetGridColumnCount(tracks.Count, options.Mode);
            int rows = useExplicitGrid
                ? tracks.Max(track => track.GridRow.GetValueOrDefault()) + 1
                : (int)Math.Ceiling(tracks.Count / (double)columns);
            int cellWidth = tracks.Max(track => track.Bounds.Width);
            int cellHeight = tracks.Max(track => track.Bounds.Height);

            int width = options.CanvasPadding * 2 + columns * cellWidth + Math.Max(0, columns - 1) * options.CellGap;
            int height = options.CanvasPadding * 2 + rows * cellHeight + Math.Max(0, rows - 1) * options.CellGap;

            string outputPath = NormalizeOutputPath(options.OutputPath);
            string outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            using var encoder = new BuildInGifEncoder();
            encoder.Init(outputPath, width, height);
            using Image backgroundImage = LoadBackgroundImage(options.BackgroundImagePath);

            foreach (TimelineState state in BuildTimeline(tracks))
            {
                using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    DrawBackground(graphics, new Rectangle(0, 0, width, height), options.BackgroundColor, backgroundImage);
                    graphics.CompositingMode = CompositingMode.SourceOver;
                    graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                    graphics.PixelOffsetMode = PixelOffsetMode.Half;

                    for (int trackIndex = 0; trackIndex < tracks.Count; trackIndex++)
                    {
                        int row = useExplicitGrid
                            ? tracks[trackIndex].GridRow.GetValueOrDefault()
                            : trackIndex / columns;
                        int column = useExplicitGrid
                            ? tracks[trackIndex].GridColumn.GetValueOrDefault()
                            : trackIndex % columns;
                        DrawTrackFrame(
                            graphics,
                            tracks[trackIndex],
                            state.FrameIndexes[trackIndex],
                            cellWidth,
                            cellHeight,
                            options.CanvasPadding + column * (cellWidth + options.CellGap),
                            options.CanvasPadding + row * (cellHeight + options.CellGap));
                    }
                }

                encoder.AppendFrame(bitmap, Math.Max(10, state.Delay));
            }
        }

        private static int GetGridColumnCount(int trackCount, RenderMode mode)
        {
            if (trackCount <= 1)
            {
                return 1;
            }

            if (mode == RenderMode.DyeGrid)
            {
                return 3;
            }

            return (int)Math.Ceiling(Math.Sqrt(trackCount));
        }

        private static bool RequiresDyeTarget(RenderMode mode)
        {
            return mode == RenderMode.DyeGrid || mode == RenderMode.ExactDye;
        }

        private static Image LoadBackgroundImage(string backgroundImagePath)
        {
            if (string.IsNullOrWhiteSpace(backgroundImagePath))
            {
                return null;
            }

            using Image sourceImage = Image.FromFile(backgroundImagePath);
            return new Bitmap(sourceImage);
        }

        private static void DrawBackground(Graphics graphics, Rectangle canvasBounds, Color backgroundColor, Image backgroundImage)
        {
            graphics.Clear(backgroundColor);

            if (backgroundImage == null)
            {
                return;
            }

            Rectangle destination = GetCoverRectangle(canvasBounds.Size, backgroundImage.Size);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(backgroundImage, destination);
        }

        private static Rectangle GetCoverRectangle(Size canvasSize, Size imageSize)
        {
            if (canvasSize.Width <= 0 || canvasSize.Height <= 0 || imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return new Rectangle(Point.Empty, canvasSize);
            }

            double scale = Math.Max(
                canvasSize.Width / (double)imageSize.Width,
                canvasSize.Height / (double)imageSize.Height);

            int drawWidth = Math.Max(canvasSize.Width, (int)Math.Ceiling(imageSize.Width * scale));
            int drawHeight = Math.Max(canvasSize.Height, (int)Math.Ceiling(imageSize.Height * scale));
            int drawX = (canvasSize.Width - drawWidth) / 2;
            int drawY = (canvasSize.Height - drawHeight) / 2;
            return new Rectangle(drawX, drawY, drawWidth, drawHeight);
        }

        private static IEnumerable<TimelineState> BuildTimeline(IReadOnlyList<RenderTrack> tracks)
        {
            int totalDuration = tracks.Max(track => track.TotalDuration);
            if (totalDuration <= 0)
            {
                yield break;
            }

            int[] frameIndexes = new int[tracks.Count];
            int[] remaining = tracks.Select(track => track.Frames[0].Delay).ToArray();
            int elapsed = 0;
            TimelineState previous = null;

            while (elapsed < totalDuration)
            {
                int delay = remaining.Min();
                if (delay <= 0)
                {
                    delay = 10;
                }

                if (elapsed + delay > totalDuration)
                {
                    delay = totalDuration - elapsed;
                }

                var current = new TimelineState
                {
                    FrameIndexes = (int[])frameIndexes.Clone(),
                    Delay = delay,
                };

                if (previous != null && previous.FrameIndexes.SequenceEqual(current.FrameIndexes))
                {
                    previous.Delay += current.Delay;
                }
                else
                {
                    if (previous != null)
                    {
                        yield return previous;
                    }

                    previous = current;
                }

                elapsed += delay;

                for (int trackIndex = 0; trackIndex < tracks.Count; trackIndex++)
                {
                    remaining[trackIndex] -= delay;
                    while (remaining[trackIndex] <= 0)
                    {
                        frameIndexes[trackIndex] = (frameIndexes[trackIndex] + 1) % tracks[trackIndex].Frames.Count;
                        remaining[trackIndex] += tracks[trackIndex].Frames[frameIndexes[trackIndex]].Delay;
                    }
                }
            }

            if (previous != null)
            {
                yield return previous;
            }
        }

        private static void DrawTrackFrame(Graphics graphics, RenderTrack track, int frameIndex, int cellWidth, int cellHeight, int cellX, int cellY)
        {
            RenderFrame frame = track.Frames[frameIndex];
            int leftPadding = (cellWidth - track.Bounds.Width) / 2;
            int topPadding = (cellHeight - track.Bounds.Height) / 2;
            int drawX = cellX + leftPadding - track.Bounds.X - frame.Origin.X;
            int drawY = cellY + topPadding - track.Bounds.Y - frame.Origin.Y;

            graphics.DrawImage(frame.Bitmap, drawX, drawY, frame.Bitmap.Width, frame.Bitmap.Height);
        }

        private static string NormalizeOutputPath(string outputPath)
        {
            return string.IsNullOrWhiteSpace(Path.GetExtension(outputPath))
                ? outputPath + ".gif"
                : outputPath;
        }

        private static bool HasFlag(string[] args, params string[] flags)
        {
            return args.Any(arg => flags.Any(flag => string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase)));
        }

        private static string GetUsage()
        {
            return string.Join(Environment.NewLine, new[]
            {
                "AvatarGifTool",
                "",
                "Build (Win10/Win11 x64):",
                "  dotnet build AvatarGifTool\\AvatarGifTool.csproj -c Release -p:Platform=x64",
                "",
                "Usage:",
                "  AvatarGifTool.exe --base-wz <Base.wz> --template <appearanceIds...>",
                "                    --output <file.gif> [--mode normal|dye|exact] [--gear <gearIds...>]",
                "",
                "Options:",
                "  --base-wz        Base.wz full path.",
                "  --template       Comma-separated appearance IDs. Skin, face and hair are auto-detected.",
                "  --gear           Optional extra appearance IDs, comma-separated. In dye mode it is required and all IDs will be dyed together.",
                "  --output         Output GIF path.",
                "  --mode           normal, dye or exact. Default: normal.",
                "  --dye            Same as --mode dye.",
                "  --exact-dye      Same as --mode exact.",
                "  --hue-step       Default 30.",
                "  --prism-type     Color type, default 0 / 整体色系. Supports 0..6 or Chinese names.",
                "  --hue            Exact dye hue, default 0, range 0..359.",
                "  --saturation     Saturation offset, default 0, range -99..99.",
                "  --brightness     Brightness offset, default 0, range -99..99.",
                "  --hair-mix-color Hair mix color, default 0. Supports 0..7 or color names: 黑, 红, 橙, 黄, 绿, 青, 紫, 褐.",
                "  --hair-mix-opacity Hair mix opacity, default 0, range 0..100.",
                "  --eye-mix-color  Eye/face mix color, default 0. Supports 0..7 or color names: 黑, 青, 红, 绿, 褐, 祖母绿, 紫, 紫水晶.",
                "  --eye-mix-opacity Eye/face mix opacity, default 0, range 0..100.",
                "  --bg-color       Background color, default #FFFFFF. Supports transparent / #RRGGBB / #AARRGGBB.",
                "  --bg-image       Optional background image path. Supports png/jpg and uses cover scaling with crop.",
                "  --actions        Optional normal-mode action list. Example: stand1,swingO1,jump",
                "  --dye-action     Optional single action for dye mode. Default: stand1.",
                "  --cell-gap       Default 8.",
                "  --canvas-padding Default 8.",
                "",
                "Examples:",
                "  AvatarGifTool.exe --base-wz D:\\Maple\\Base.wz --template 1051001,30000,2000,1072153,20000 --output D:\\out\\avatar.gif",
                "  AvatarGifTool.exe --base-wz D:\\Maple\\Base.wz --template 1051001,30000,2000,1072153,20000 --gear 1702000,1082102 --output D:\\out\\weapon.gif",
                "  AvatarGifTool.exe --base-wz D:\\Maple\\Base.wz --template 1051001,30000,2000,20000 --gear 1053345,1072153 --output D:\\out\\dye.gif --dye --saturation 12 --brightness -6 --bg-color transparent",
                "  AvatarGifTool.exe --base-wz D:\\Maple\\Base.wz --template 1051001,30000,2000,20000 --gear 1053345 --output D:\\out\\exact.gif --mode exact --prism-type 红色系 --hue 30 --saturation 20 --brightness -10",
                "",
                "Notes:",
                "  normal mode defaults to stand1, swingO1, swingO2, shoot1, jump, walk1 and auto-arranges near square.",
                "  dye mode defaults to stand1, gear is required, hue 0..330 with step 30 by default, arranged as 3 hue columns plus one saturation/brightness extreme column.",
                "  exact dye mode uses the normal-mode action list and applies one manually specified dye setting.",
            });
        }

        internal sealed class PrismColorTypeDefinition
        {
            private readonly string[] aliases;

            public PrismColorTypeDefinition(int value, string name, params string[] aliases)
            {
                this.Value = value;
                this.Name = name;
                this.aliases = aliases ?? Array.Empty<string>();
            }

            public int Value { get; }

            public string Name { get; }

            public bool Matches(string text)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    return false;
                }

                string trimmed = text.Trim();
                return string.Equals(this.Value.ToString(), trimmed, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(this.Name, trimmed, StringComparison.OrdinalIgnoreCase)
                    || this.aliases.Any(alias => string.Equals(alias, trimmed, StringComparison.OrdinalIgnoreCase));
            }

            public override string ToString()
            {
                return this.Name;
            }
        }

        internal sealed class CommandOptions
        {
            public string BaseWzPath { get; private set; }
            public string Template { get; private set; }
            public int[] GearIds { get; private set; }
            public string OutputPath { get; private set; }
            public RenderMode Mode { get; private set; }
            public int HueStep { get; private set; }
            public int PrismType { get; private set; }
            public int Hue { get; private set; }
            public int SaturationOffset { get; private set; }
            public int BrightnessOffset { get; private set; }
            public int HairMixColor { get; private set; }
            public int HairMixOpacity { get; private set; }
            public int FaceMixColor { get; private set; }
            public int FaceMixOpacity { get; private set; }
            public Color BackgroundColor { get; private set; }
            public string BackgroundImagePath { get; private set; }
            public string[] NormalActions { get; private set; }
            public string DyeAction { get; private set; }
            public int CellGap { get; private set; }
            public int CanvasPadding { get; private set; }

            public static CommandOptions Create(
                string baseWzPath,
                string template,
                string outputPath,
                RenderMode mode,
                string gearText = null,
                int hueStep = 30,
                int prismType = 0,
                int hue = 0,
                int saturationOffset = 0,
                int brightnessOffset = 0,
                int hairMixColor = 0,
                int hairMixOpacity = 0,
                int faceMixColor = 0,
                int faceMixOpacity = 0,
                Color? backgroundColor = null,
                string backgroundImagePath = null,
                IEnumerable<string> normalActions = null,
                string dyeAction = null,
                int cellGap = 8,
                int canvasPadding = 8)
            {
                var options = new CommandOptions
                {
                    BaseWzPath = baseWzPath?.Trim(),
                    Template = template?.Trim(),
                    GearIds = ParseOptionalIntList(gearText, "gear"),
                    OutputPath = outputPath?.Trim(),
                    Mode = mode,
                    HueStep = hueStep,
                    PrismType = NormalizePrismType(prismType),
                    Hue = hue,
                    SaturationOffset = saturationOffset,
                    BrightnessOffset = brightnessOffset,
                    HairMixColor = hairMixColor,
                    HairMixOpacity = hairMixOpacity,
                    FaceMixColor = faceMixColor,
                    FaceMixOpacity = faceMixOpacity,
                    BackgroundColor = backgroundColor ?? Color.White,
                    BackgroundImagePath = string.IsNullOrWhiteSpace(backgroundImagePath) ? null : backgroundImagePath.Trim(),
                    NormalActions = NormalizeNormalActionSelection(normalActions),
                    DyeAction = NormalizeDyeActionSelection(dyeAction),
                    CellGap = cellGap,
                    CanvasPadding = canvasPadding,
                };

                options.Validate();
                return options;
            }

            public static CommandOptions Parse(string[] args)
            {
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                bool dyeFlag = false;
                bool exactDyeFlag = false;

                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i];
                    if (string.Equals(arg, "--dye", StringComparison.OrdinalIgnoreCase))
                    {
                        dyeFlag = true;
                        continue;
                    }
                    if (string.Equals(arg, "--exact-dye", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(arg, "--precise-dye", StringComparison.OrdinalIgnoreCase))
                    {
                        exactDyeFlag = true;
                        continue;
                    }

                    if (!arg.StartsWith("-") && !arg.StartsWith("/"))
                    {
                        throw new ArgumentException($"Unknown argument: {arg}");
                    }

                    string key = arg.TrimStart('-', '/');
                    var valueParts = new List<string>();

                    while (i + 1 < args.Length)
                    {
                        string nextArg = args[i + 1];
                        if (IsOptionToken(nextArg))
                        {
                            break;
                        }

                        valueParts.Add(nextArg);
                        i++;
                    }

                    if (valueParts.Count == 0)
                    {
                        throw new ArgumentException($"Missing value for argument: {arg}");
                    }

                    values[key] = string.Join(" ", valueParts);
                }

                var options = new CommandOptions
                {
                    BaseWzPath = GetRequired(values, "base-wz"),
                    Template = GetRequired(values, "template"),
                    GearIds = ParseOptionalIntList(GetOptional(values, "gear", null), "gear"),
                    OutputPath = GetRequired(values, "output"),
                    Mode = exactDyeFlag ? RenderMode.ExactDye : dyeFlag ? RenderMode.DyeGrid : ParseMode(GetOptional(values, "mode", "normal")),
                    HueStep = ParseInt(GetOptional(values, "hue-step", "30"), "hue-step"),
                    PrismType = ParsePrismType(GetOptional(values, "prism-type", "0"), "prism-type"),
                    Hue = ParseInt(GetOptional(values, "hue", "0"), "hue"),
                    SaturationOffset = ParseInt(GetOptional(values, "saturation", "0"), "saturation"),
                    BrightnessOffset = ParseInt(GetOptional(values, "brightness", "0"), "brightness"),
                    HairMixColor = ParseMixColor(GetOptional(values, "hair-mix-color", "0"), "hair-mix-color", SupportedHairMixColors),
                    HairMixOpacity = ParseInt(GetOptional(values, "hair-mix-opacity", "0"), "hair-mix-opacity"),
                    FaceMixColor = ParseMixColor(GetOptional(values, "face-mix-color", GetOptional(values, "eye-mix-color", "0")), "eye-mix-color", SupportedFaceMixColors),
                    FaceMixOpacity = ParseInt(GetOptional(values, "face-mix-opacity", GetOptional(values, "eye-mix-opacity", "0")), "eye-mix-opacity"),
                    BackgroundColor = ParseColor(GetOptional(values, "bg-color", "#FFFFFF"), "bg-color"),
                    BackgroundImagePath = NormalizeOptionalPath(GetOptional(values, "bg-image", null)),
                    NormalActions = NormalizeNormalActionSelection(ParseOptionalActionList(GetOptional(values, "actions", null), "actions")),
                    DyeAction = NormalizeDyeActionSelection(ParseOptionalAction(GetOptional(values, "dye-action", null), "dye-action")),
                    CellGap = ParseInt(GetOptional(values, "cell-gap", "8"), "cell-gap"),
                    CanvasPadding = ParseInt(GetOptional(values, "canvas-padding", "8"), "canvas-padding"),
                };

                options.Validate();
                return options;
            }

            private void Validate()
            {
                if (!File.Exists(this.BaseWzPath))
                {
                    throw new FileNotFoundException("Base.wz was not found.", this.BaseWzPath);
                }

                if (string.IsNullOrWhiteSpace(this.Template))
                {
                    throw new ArgumentException("template is required.");
                }

                if (string.IsNullOrWhiteSpace(this.OutputPath))
                {
                    throw new ArgumentException("output is required.");
                }

                this.GearIds ??= Array.Empty<int>();
                this.NormalActions = NormalizeNormalActionSelection(this.NormalActions);
                this.DyeAction = NormalizeDyeActionSelection(this.DyeAction);

                if (this.GearIds.Any(id => id <= 0))
                {
                    throw new ArgumentOutOfRangeException(nameof(this.GearIds), "gear must contain positive integers.");
                }

                if (RequiresDyeTarget(this.Mode) && this.GearIds.Length == 0)
                {
                    throw new ArgumentException("gear is required in dye mode.");
                }

                if (this.HueStep <= 0 || this.HueStep > 360)
                {
                    throw new ArgumentOutOfRangeException(nameof(this.HueStep), "hue-step must be between 1 and 360.");
                }

                if (!PrismColorTypeDefinitions.Any(type => type.Value == this.PrismType))
                {
                    throw new ArgumentOutOfRangeException(nameof(this.PrismType), "prism-type must be between 0 and 6.");
                }

                if (this.Hue < 0 || this.Hue > 359)
                {
                    throw new ArgumentOutOfRangeException(nameof(this.Hue), "hue must be between 0 and 359.");
                }

                if (this.SaturationOffset < -99 || this.SaturationOffset > 99)
                {
                    throw new ArgumentOutOfRangeException(nameof(this.SaturationOffset), "saturation must be between -99 and 99.");
                }

                if (this.BrightnessOffset < -99 || this.BrightnessOffset > 99)
                {
                    throw new ArgumentOutOfRangeException(nameof(this.BrightnessOffset), "brightness must be between -99 and 99.");
                }

                ValidateMixColor(this.HairMixColor, SupportedHairMixColors, nameof(this.HairMixColor), "hair-mix-color");
                ValidateMixColor(this.FaceMixColor, SupportedFaceMixColors, nameof(this.FaceMixColor), "eye-mix-color");

                if (this.HairMixOpacity < 0 || this.HairMixOpacity > 100)
                {
                    throw new ArgumentOutOfRangeException(nameof(this.HairMixOpacity), "hair-mix-opacity must be between 0 and 100.");
                }

                if (this.FaceMixOpacity < 0 || this.FaceMixOpacity > 100)
                {
                    throw new ArgumentOutOfRangeException(nameof(this.FaceMixOpacity), "eye-mix-opacity must be between 0 and 100.");
                }

                if (this.CellGap < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(this.CellGap), "cell-gap cannot be negative.");
                }

                if (!string.IsNullOrWhiteSpace(this.BackgroundImagePath) && !File.Exists(this.BackgroundImagePath))
                {
                    throw new FileNotFoundException("Background image was not found.", this.BackgroundImagePath);
                }

                if (this.CanvasPadding < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(this.CanvasPadding), "canvas-padding cannot be negative.");
                }
            }

            private static string GetRequired(Dictionary<string, string> values, string key)
            {
                if (!values.TryGetValue(key, out string value) || string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException($"Missing required argument: --{key}");
                }

                return value;
            }

            private static string GetOptional(Dictionary<string, string> values, string key, string defaultValue)
            {
                return values.TryGetValue(key, out string value) && !string.IsNullOrWhiteSpace(value)
                    ? value
                    : defaultValue;
            }

            private static string NormalizeOptionalPath(string value)
            {
                return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            }

            private static int[] ParseOptionalIntList(string value, string key)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return Array.Empty<int>();
                }

                return ParseIdList(value, key);
            }

            private static string[] ParseOptionalActionList(string value, string key)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return null;
                }

                string[] actions = ParseActionList(value, key);
                string[] invalidActions = actions
                    .Where(action => !ActionStripActions.Any(supported =>
                        string.Equals(supported, action, StringComparison.OrdinalIgnoreCase)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (invalidActions.Length > 0)
                {
                    throw new ArgumentException($"Argument '{key}' contains unsupported actions: {string.Join(", ", invalidActions)}.");
                }

                return actions;
            }

            private static string ParseOptionalAction(string value, string key)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return null;
                }

                string action = value.Trim();
                if (!ActionStripActions.Any(supported => string.Equals(supported, action, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new ArgumentException($"Argument '{key}' contains unsupported action: {action}.");
                }

                return action;
            }

            private static int ParseInt(string value, string key)
            {
                if (!int.TryParse(value, out int result))
                {
                    throw new ArgumentException($"Argument '{key}' must be an integer.");
                }

                return result;
            }

            private static int ParsePrismType(string value, string key)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException($"Argument '{key}' must not be empty.");
                }

                PrismColorTypeDefinition colorType = PrismColorTypeDefinitions.FirstOrDefault(type => type.Matches(value));
                if (colorType == null)
                {
                    throw new ArgumentException($"Argument '{key}' must be 0..6 or one of: {string.Join(", ", PrismColorTypeDefinitions.Select(type => type.Name))}.");
                }

                return colorType.Value;
            }

            private static int ParseMixColor(string value, string key, IReadOnlyList<string> colorNames)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException($"Argument '{key}' must not be empty.");
                }

                string text = value.Trim();
                if (int.TryParse(text, out int index))
                {
                    ValidateMixColor(index, colorNames, key, key);
                    return index;
                }

                for (int i = 0; i < colorNames.Count; i++)
                {
                    if (string.Equals(colorNames[i], text, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }

                throw new ArgumentException($"Argument '{key}' must be 0..{colorNames.Count - 1} or one of: {string.Join(", ", colorNames)}.");
            }

            private static void ValidateMixColor(int color, IReadOnlyList<string> colorNames, string paramName, string displayName)
            {
                if (color < 0 || color >= colorNames.Count)
                {
                    throw new ArgumentOutOfRangeException(paramName, $"{displayName} must be between 0 and {colorNames.Count - 1}.");
                }
            }

            private static Color ParseColor(string value, string key)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException($"Argument '{key}' must not be empty.");
                }

                string text = value.Trim();
                if (string.Equals(text, "transparent", StringComparison.OrdinalIgnoreCase))
                {
                    return Color.Transparent;
                }

                if (text.StartsWith("#", StringComparison.Ordinal))
                {
                    text = text[1..];
                }

                if (text.Length == 6 && int.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out int rgb))
                {
                    return Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
                }

                if (text.Length == 8 && int.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out int argb))
                {
                    return Color.FromArgb(
                        (argb >> 24) & 0xFF,
                        (argb >> 16) & 0xFF,
                        (argb >> 8) & 0xFF,
                        argb & 0xFF);
                }

                throw new ArgumentException($"Argument '{key}' must be transparent, #RRGGBB or #AARRGGBB.");
            }

            private static RenderMode ParseMode(string mode)
            {
                switch (mode.Trim().ToLowerInvariant())
                {
                    case "normal":
                    case "strip":
                    case "action":
                        return RenderMode.ActionStrip;

                    case "dye":
                    case "grid":
                        return RenderMode.DyeGrid;

                    case "exact":
                    case "precise":
                    case "exact-dye":
                    case "precise-dye":
                        return RenderMode.ExactDye;

                    default:
                        throw new ArgumentException($"Unknown mode: {mode}");
                }
            }

            private static bool IsOptionToken(string arg)
            {
                return arg.StartsWith("--")
                    || (arg.StartsWith("/") && !arg.Contains("\\"))
                    || (arg.StartsWith("-") && arg.Length > 1 && !char.IsDigit(arg[1]));
            }
        }

        internal static TemplateAnalysis AnalyzeTemplate(string template, Func<int, AppearanceIdKind> idKindResolver)
        {
            int[] values = ParseIdList(template, "模板");

            if (idKindResolver == null)
            {
                throw new ArgumentNullException(nameof(idKindResolver));
            }

            int? skin = null;
            int? face = null;
            int? hair = null;
            var presetGearIds = new List<int>();
            var items = new List<TemplateItemInfo>(values.Length);

            foreach (int id in values)
            {
                AppearanceIdKind kind = idKindResolver(id);
                items.Add(new TemplateItemInfo
                {
                    Id = id,
                    Kind = kind,
                });

                switch (kind)
                {
                    case AppearanceIdKind.Skin:
                        skin = id;
                        break;

                    case AppearanceIdKind.Face:
                        face = id;
                        break;

                    case AppearanceIdKind.Hair:
                        hair = id;
                        break;

                    case AppearanceIdKind.Gear:
                        presetGearIds.Add(id);
                        break;
                }
            }

            return new TemplateAnalysis
            {
                Skin = skin,
                Face = face,
                Hair = hair,
                PresetGearIds = presetGearIds.ToArray(),
                Items = items,
            };
        }

        internal sealed class AvatarTemplate
        {
            public int Skin { get; private set; }
            public int Face { get; private set; }
            public int Hair { get; private set; }
            public int[] PresetGearIds { get; private set; }

            public static AvatarTemplate Parse(string template, Func<int, AppearanceIdKind> idKindResolver)
            {
                TemplateAnalysis analysis = AnalyzeTemplate(template, idKindResolver);
                int[] unknownIds = analysis.Items
                    .Where(item => item.Kind == AppearanceIdKind.Unknown)
                    .Select(item => item.Id)
                    .Distinct()
                    .ToArray();

                if (unknownIds.Length > 0)
                {
                    throw new FormatException($"模板中包含无法识别的 ID：{string.Join(", ", unknownIds)}。");
                }

                int resolvedSkin;
                if (analysis.Skin.HasValue)
                {
                    resolvedSkin = analysis.Skin.Value;
                }
                else if (!TryResolveDefaultSkinId(out resolvedSkin))
                {
                    throw new FormatException("模板里缺少肤色 ID，且未找到可用默认肤色资源。");
                }

                if (!analysis.Face.HasValue || !analysis.Hair.HasValue)
                {
                    throw new FormatException("模板里必须包含脸型、发型 ID。");
                }

                return new AvatarTemplate
                {
                    Skin = resolvedSkin,
                    Face = analysis.Face.Value,
                    Hair = analysis.Hair.Value,
                    PresetGearIds = analysis.PresetGearIds,
                };
            }
        }

        internal sealed class TemplateAnalysis
        {
            public int? Skin { get; set; }
            public int? Face { get; set; }
            public int? Hair { get; set; }
            public int[] PresetGearIds { get; set; }
            public List<TemplateItemInfo> Items { get; set; }

            public bool HasUnknownIds => this.Items?.Any(item => item.Kind == AppearanceIdKind.Unknown) == true;

            public bool HasCoreAvatarIds => (this.Skin.HasValue || TryResolveDefaultSkinId(out _)) && this.Face.HasValue && this.Hair.HasValue;

            public bool CanRenderAvatar => this.HasCoreAvatarIds && !this.HasUnknownIds;
        }

        internal sealed class TemplateItemInfo
        {
            public int Id { get; set; }
            public AppearanceIdKind Kind { get; set; }
        }

        internal enum AppearanceIdKind
        {
            Unknown = 0,
            Skin,
            Face,
            Hair,
            Gear,
            Item,
        }

        internal static int[] ParseIdList(string text, string fieldName)
        {
            MatchCollection matches = Regex.Matches(text ?? string.Empty, @"\d+");
            int[] values = matches.Cast<Match>().Select(m => int.Parse(m.Value)).ToArray();
            if (values.Length == 0)
            {
                throw new FormatException($"{fieldName}里没有识别到任何 ID。");
            }

            return values;
        }

        internal static EffectiveAppearance BuildEffectiveAppearance(AvatarTemplate template, IEnumerable<int> extraGearIds)
        {
            var gears = new List<EffectiveGear>();

            foreach (int presetGearId in template.PresetGearIds)
            {
                ApplyGearOverride(gears, presetGearId);
            }

            foreach (int extraGearId in extraGearIds ?? Enumerable.Empty<int>())
            {
                ApplyGearOverride(gears, extraGearId);
            }

            return new EffectiveAppearance
            {
                Skin = template.Skin,
                Face = template.Face,
                Hair = template.Hair,
                Gears = gears,
            };
        }

        internal static string GetAppearanceSlotLabel(int id)
        {
            return GetAppearanceSlotLabel(GetAppearanceSlotKey(id));
        }

        private static void ApplyGearOverride(List<EffectiveGear> gears, int id)
        {
            AppearanceSlotKey slotKey = GetAppearanceSlotKey(id);
            if (slotKey != AppearanceSlotKey.Ring)
            {
                gears.RemoveAll(gear => SlotsConflict(slotKey, gear.SlotKey));
            }

            gears.Add(new EffectiveGear
            {
                Id = id,
                SlotKey = slotKey,
                SlotLabel = GetAppearanceSlotLabel(slotKey),
            });
        }

        private static bool SlotsConflict(AppearanceSlotKey newSlot, AppearanceSlotKey existingSlot)
        {
            switch (newSlot)
            {
                case AppearanceSlotKey.Coat:
                    return existingSlot == AppearanceSlotKey.Coat || existingSlot == AppearanceSlotKey.Longcoat;

                case AppearanceSlotKey.Longcoat:
                    return existingSlot == AppearanceSlotKey.Coat
                        || existingSlot == AppearanceSlotKey.Longcoat
                        || existingSlot == AppearanceSlotKey.Pants;

                case AppearanceSlotKey.Pants:
                    return existingSlot == AppearanceSlotKey.Pants || existingSlot == AppearanceSlotKey.Longcoat;

                case AppearanceSlotKey.Ring:
                    return false;

                default:
                    return newSlot == existingSlot;
            }
        }

        private static AppearanceSlotKey GetAppearanceSlotKey(int id)
        {
            GearType type = Gear.GetGearType(id);

            if (Gear.IsWeapon(type) || type == GearType.cashWeapon || type == GearType.shovel || type == GearType.pickaxe)
            {
                return AppearanceSlotKey.Weapon;
            }

            if (type == GearType.shield || Gear.IsSubWeapon(type))
            {
                return AppearanceSlotKey.SubWeapon;
            }

            switch (type)
            {
                case GearType.cap:
                    return AppearanceSlotKey.Cap;

                case GearType.coat:
                    return AppearanceSlotKey.Coat;

                case GearType.longcoat:
                    return AppearanceSlotKey.Longcoat;

                case GearType.pants:
                    return AppearanceSlotKey.Pants;

                case GearType.shoes:
                    return AppearanceSlotKey.Shoes;

                case GearType.glove:
                    return AppearanceSlotKey.Glove;

                case GearType.cape:
                    return AppearanceSlotKey.Cape;

                case GearType.earrings:
                    return AppearanceSlotKey.Earrings;

                case GearType.faceAccessory:
                    return AppearanceSlotKey.FaceAccessory;

                case GearType.eyeAccessory:
                    return AppearanceSlotKey.EyeAccessory;

                case GearType.taming:
                case GearType.taming2:
                case GearType.taming3:
                case GearType.tamingChair:
                    return AppearanceSlotKey.Taming;

                case GearType.saddle:
                    return AppearanceSlotKey.Saddle;

                case GearType.pendant:
                    return AppearanceSlotKey.Pendant;

                case GearType.belt:
                    return AppearanceSlotKey.Belt;

                case GearType.shoulderPad:
                    return AppearanceSlotKey.ShoulderPad;

                case GearType.pocket:
                    return AppearanceSlotKey.Pocket;

                case GearType.emblem:
                case GearType.powerSource:
                    return AppearanceSlotKey.Emblem;

                case GearType.ring:
                    return AppearanceSlotKey.Ring;

                default:
                    return AppearanceSlotKey.Unknown;
            }
        }

        private static string GetAppearanceSlotLabel(AppearanceSlotKey slotKey)
        {
            switch (slotKey)
            {
                case AppearanceSlotKey.Cap:
                    return "帽子";
                case AppearanceSlotKey.Coat:
                    return "上衣";
                case AppearanceSlotKey.Longcoat:
                    return "套服";
                case AppearanceSlotKey.Pants:
                    return "裤子";
                case AppearanceSlotKey.Shoes:
                    return "鞋子";
                case AppearanceSlotKey.Glove:
                    return "手套";
                case AppearanceSlotKey.SubWeapon:
                    return "副手";
                case AppearanceSlotKey.Cape:
                    return "披风";
                case AppearanceSlotKey.Weapon:
                    return "武器";
                case AppearanceSlotKey.Earrings:
                    return "耳环";
                case AppearanceSlotKey.FaceAccessory:
                    return "脸饰";
                case AppearanceSlotKey.EyeAccessory:
                    return "眼饰";
                case AppearanceSlotKey.Taming:
                    return "骑宠";
                case AppearanceSlotKey.Saddle:
                    return "鞍子";
                case AppearanceSlotKey.Pendant:
                    return "吊坠";
                case AppearanceSlotKey.Belt:
                    return "腰带";
                case AppearanceSlotKey.ShoulderPad:
                    return "肩饰";
                case AppearanceSlotKey.Pocket:
                    return "口袋";
                case AppearanceSlotKey.Emblem:
                    return "纹章";
                case AppearanceSlotKey.Ring:
                    return "戒指";
                default:
                    return "外观";
            }
        }

        internal static AppearanceIdKind DetectAppearanceIdKind(int id)
        {
            GearType type = Gear.GetGearType(id);

            if (Gear.IsFace(type) && FindFaceNode(id) != null)
            {
                return AppearanceIdKind.Face;
            }

            if (Gear.IsHair(type) && FindHairNode(id) != null)
            {
                return AppearanceIdKind.Hair;
            }

            if (IsSkinIdCandidate(id) && TryGetSkinNodes(id, out _, out _))
            {
                return AppearanceIdKind.Skin;
            }

            if (FindGearNode(id) != null)
            {
                return AppearanceIdKind.Gear;
            }

            return AppearanceIdKind.Unknown;
        }

        internal static bool TryGetSkinNodes(int skin, out Wz_Node bodyNode, out Wz_Node headNode)
        {
            if (!IsSkinIdCandidate(skin))
            {
                bodyNode = null;
                headNode = null;
                return false;
            }

            bodyNode = BuildSkinBodyCandidates(skin)
                .Select(FindExtractedNode)
                .FirstOrDefault(node => node != null);

            headNode = BuildSkinHeadCandidates(skin)
                .Select(FindExtractedNode)
                .FirstOrDefault(node => node != null);

            return bodyNode != null && headNode != null;
        }

        private static bool IsSkinIdCandidate(int id)
        {
            if (id <= 0)
            {
                return false;
            }

            GearType type = Gear.GetGearType(id);
            return type == GearType.body
                || type == GearType.head
                || type == GearType.head_n;
        }

        internal static bool TryResolveDefaultSkinId(out int skin)
        {
            if (TryGetSkinNodes(PreferredDefaultSkinId, out _, out _))
            {
                skin = PreferredDefaultSkinId;
                return true;
            }

            if (TryGetSkinNodes(2015, out _, out _))
            {
                skin = 2015;
                return true;
            }

            Wz_Node charaWz = PluginManager.FindWz(Wz_Type.Character);
            if (charaWz != null)
            {
                foreach (Wz_Node node in charaWz.Nodes)
                {
                    Match match = Regex.Match(node.Text, @"^0000(\d{4})\.img$");
                    if (!match.Success)
                    {
                        continue;
                    }

                    if (int.TryParse(match.Groups[1].Value, out int candidateSkin)
                        && TryGetSkinNodes(candidateSkin, out _, out _))
                    {
                        skin = candidateSkin;
                        return true;
                    }
                }
            }

            skin = 0;
            return false;
        }

        internal static IEnumerable<int> EnumerateAvailableSkinIds()
        {
            Wz_Node charaWz = PluginManager.FindWz(Wz_Type.Character);
            if (charaWz == null)
            {
                yield break;
            }

            var bodyIds = new HashSet<int>();
            var headIds = new HashSet<int>();

            foreach (Wz_Node node in charaWz.Nodes)
            {
                if (node?.Text == null)
                {
                    continue;
                }

                if (TryParseSkinNodeId(node.Text, "0000", out int bodyId))
                {
                    bodyIds.Add(bodyId);
                }

                if (TryParseSkinNodeId(node.Text, "0001", out int headId))
                {
                    headIds.Add(headId);
                }
            }

            foreach (int id in bodyIds.Intersect(headIds).OrderBy(id => id))
            {
                yield return id;
            }
        }

        internal static Wz_Node FindHairNode(int id)
        {
            return FindCharacterAppearanceNode("Hair", id);
        }

        internal static Wz_Node FindFaceNode(int id)
        {
            return FindCharacterAppearanceNode("Face", id);
        }

        internal static Wz_Node FindGearNode(int id)
        {
            Wz_Node charaWz = PluginManager.FindWz(Wz_Type.Character);
            if (charaWz == null)
            {
                return null;
            }

            return FindCharacterNodeByImageName(charaWz, id.ToString("D8") + ".img");
        }

        internal static Wz_Node FindAppearanceIconNode(int id)
        {
            AppearanceIdKind kind = DetectAppearanceIdKind(id);
            switch (kind)
            {
                case AppearanceIdKind.Skin:
                    if (TryGetSkinNodes(id, out Wz_Node bodyNode, out Wz_Node headNode))
                    {
                        return FindIconNode(bodyNode) ?? FindIconNode(headNode);
                    }

                    return null;

                case AppearanceIdKind.Face:
                    return FindIconNode(FindFaceNode(id));

                case AppearanceIdKind.Hair:
                    return FindIconNode(FindHairNode(id));

                case AppearanceIdKind.Gear:
                    return FindIconNode(FindGearNode(id));

                default:
                    return null;
            }
        }

        private static IEnumerable<string> BuildSkinBodyCandidates(int skin)
        {
            int skinId = (skin % 2000) + 2000;
            return new[]
            {
                $@"Character\0000{skinId:D4}.img",
                $@"Character\00002{skin:D3}.img",
                $@"Character\0000{skin:D4}.img",
            };
        }

        private static IEnumerable<string> BuildSkinHeadCandidates(int skin)
        {
            int skinId = (skin % 2000) + 2000;
            return new[]
            {
                $@"Character\0001{skinId:D4}.img",
                $@"Character\00012{skin:D3}.img",
                $@"Character\0001{skin:D4}.img",
            };
        }

        private static bool TryParseSkinNodeId(string text, string prefix, out int skinId)
        {
            skinId = 0;
            Match match = Regex.Match(text ?? string.Empty, $@"^{Regex.Escape(prefix)}(\d{{4}})\.img$");
            return match.Success && int.TryParse(match.Groups[1].Value, out skinId);
        }

        private static Wz_Node FindExtractedNode(string path)
        {
            return EnsureExtractedNode(PluginManager.FindWz(path));
        }

        internal static void ResetAppearanceNodeCache()
        {
            lock (AppearanceNodeCacheLock)
            {
                AppearanceNodeCache.Clear();
            }
        }

        private static Wz_Node EnsureExtractedNode(Wz_Node node)
        {
            if (node == null)
            {
                return null;
            }

            Wz_Image image = node.GetValueEx<Wz_Image>(null);
            if (image != null)
            {
                return image.TryExtract() ? image.Node : null;
            }

            return node;
        }

        private static Wz_Node FindIconNode(Wz_Node imageNode)
        {
            return imageNode?.FindNodeByPath("info/icon") ?? imageNode?.FindNodeByPath("info/iconRaw");
        }

        private static Wz_Node FindCharacterAppearanceNode(string category, int id)
        {
            string cacheKey = $"{category}:{id:D8}";
            lock (AppearanceNodeCacheLock)
            {
                if (AppearanceNodeCache.TryGetValue(cacheKey, out Wz_Node cachedNode))
                {
                    return cachedNode;
                }
            }

            Wz_Node imageNode = FindExtractedNode($@"Character\{category}\{id:D8}.img");
            if (imageNode == null)
            {
                Wz_Node categoryNode = FindExtractedNode($@"Character\{category}");
                if (categoryNode != null)
                {
                    imageNode = FindCharacterNodeByImageName(categoryNode, id.ToString("D8") + ".img");
                }
            }

            lock (AppearanceNodeCacheLock)
            {
                AppearanceNodeCache[cacheKey] = imageNode;
            }

            return imageNode;
        }

        private static Wz_Node FindCharacterNodeByImageName(Wz_Node rootNode, string imageName)
        {
            if (rootNode == null || string.IsNullOrWhiteSpace(imageName))
            {
                return null;
            }

            var stack = new Stack<Wz_Node>();
            stack.Push(rootNode);

            while (stack.Count > 0)
            {
                Wz_Node node = stack.Pop();
                if (node?.Text != null
                    && string.Equals(node.Text, imageName, StringComparison.OrdinalIgnoreCase))
                {
                    return EnsureExtractedNode(node);
                }

                for (int i = node?.Nodes.Count - 1 ?? -1; i >= 0; i--)
                {
                    Wz_Node child = node.Nodes[i];
                    if (child?.Text != null
                        && child.Text.Contains("_Canvas", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    stack.Push(child);
                }
            }

            return null;
        }

        internal sealed class EffectiveAppearance
        {
            public int Skin { get; set; }
            public int Face { get; set; }
            public int Hair { get; set; }
            public List<EffectiveGear> Gears { get; set; }
        }

        internal sealed class EffectiveGear
        {
            public int Id { get; set; }
            public AppearanceSlotKey SlotKey { get; set; }
            public string SlotLabel { get; set; }
        }

        internal enum AppearanceSlotKey
        {
            Unknown = 0,
            Cap,
            Coat,
            Longcoat,
            Pants,
            Shoes,
            Glove,
            SubWeapon,
            Cape,
            Weapon,
            Earrings,
            FaceAccessory,
            EyeAccessory,
            Taming,
            Saddle,
            Pendant,
            Belt,
            ShoulderPad,
            Pocket,
            Emblem,
            Ring,
        }

        private sealed class AvatarBuilder : IDisposable
        {
            private readonly AvatarCanvas canvas;
            private int weaponType;

            public AvatarBuilder()
            {
                this.canvas = new AvatarCanvas();
                this.canvas.LoadZ();
                this.canvas.LoadActions();
                this.canvas.LoadEmotions();
                this.weaponType = 0;
            }

            public void AddBodyFromSkin(int skin)
            {
                if (!TryGetSkinNodes(skin, out Wz_Node bodyNode, out Wz_Node headNode))
                {
                    throw new InvalidOperationException(
                        $"Unable to load body/head for skin {skin}. Tried body: {string.Join(", ", BuildSkinBodyCandidates(skin))} ; head: {string.Join(", ", BuildSkinHeadCandidates(skin))}");
                }

                this.canvas.AddPart(bodyNode);
                this.canvas.AddPart(headNode);
                this.canvas.LoadAllEffects();
            }

            public void AddHairOrFace(int id)
            {
                Wz_Node node = FindHairNode(id) ?? FindFaceNode(id);

                if (node == null)
                {
                    throw new InvalidOperationException($"Unable to load hair/face ID {id:D8}.");
                }

                this.canvas.AddPart(node);
                GearType type = Gear.GetGearType(id);
                if (Gear.IsFace(type))
                {
                    this.canvas.LoadEmotions();
                }
                this.canvas.LoadAllEffects();
            }

            public AvatarPart AddGear(int id, bool required)
            {
                Wz_Node gearNode = FindGearNode(id);
                if (gearNode == null)
                {
                    if (required)
                    {
                        throw new InvalidOperationException($"Unable to load appearance ID {id:D8}.");
                    }

                    return null;
                }

                this.PrepareSlotForGear(id);
                AvatarPart part = this.canvas.AddPart(gearNode);
                this.UpdateWeaponState(id, gearNode);
                this.canvas.LoadAllEffects();
                return part;
            }

            public BitmapOrigin GetBitmapOrigin(ActionFrame bodyAction, ActionFrame faceAction, ActionFrame tamingAction, ActionFrame[] effectActions = null)
            {
                Bone bone = this.canvas.CreateFrame(bodyAction, faceAction, tamingAction, effectActions);
                return this.canvas.DrawFrame(bone);
            }

            public ActionFrame[] GetActionFrames(string actionName)
            {
                return this.canvas.GetActionFrames(actionName);
            }

            public ActionFrame[] GetFaceFrames(string emotionName)
            {
                return this.canvas.GetFaceFrames(emotionName);
            }

            public ActionFrame[][] GetEffectFrames(string actionName)
            {
                var effectFrames = new ActionFrame[AvatarCanvas.LayerSlotLength][];

                for (int layerIndex = 0; layerIndex < effectFrames.Length; layerIndex++)
                {
                    if (!this.canvas.IsPartEffectVisible(layerIndex))
                    {
                        continue;
                    }

                    string selectedAction = this.SelectEffectActionName(layerIndex, actionName);
                    if (string.IsNullOrEmpty(selectedAction))
                    {
                        continue;
                    }

                    ActionFrame[] frames = this.canvas.GetEffectFrames(selectedAction, layerIndex);
                    if (frames == null || frames.Length == 0)
                    {
                        continue;
                    }

                    effectFrames[layerIndex] = frames;
                }

                return effectFrames;
            }

            public string GetStandardEmotion()
            {
                IEnumerable<string> emotions = this.canvas.Face?.Node?.Nodes
                    ?.Select(node => node?.Text)
                    ?.Where(text => !string.IsNullOrWhiteSpace(text) && !string.Equals(text, "info", StringComparison.OrdinalIgnoreCase))
                    ?.ToArray();

                if (emotions == null || !emotions.Any())
                {
                    emotions = this.canvas.Emotions;
                }

                if (emotions.Contains("default", StringComparer.OrdinalIgnoreCase))
                {
                    return "default";
                }

                if (emotions.Contains("blink", StringComparer.OrdinalIgnoreCase))
                {
                    return "blink";
                }

                return emotions.FirstOrDefault();
            }

            public void Dispose()
            {
                this.canvas.ClearSkinCache();
                Array.Clear(this.canvas.Parts, 0, this.canvas.Parts.Length);
            }

            public void ClearSkinCache()
            {
                this.canvas.ClearSkinCache();
            }

            public void ApplyCosmeticMix(int hairMixColor, int hairMixOpacity, int faceMixColor, int faceMixOpacity)
            {
                bool changed = ApplyPartMix(this.canvas.Hair, hairMixColor, hairMixOpacity)
                    | ApplyPartMix(this.canvas.Face, faceMixColor, faceMixOpacity);

                if (changed)
                {
                    this.canvas.ClearSkinCache();
                }
            }

            public void ReloadEffects()
            {
                this.canvas.LoadAllEffects();
            }

            private static bool ApplyPartMix(AvatarPart part, int mixColor, int mixOpacity)
            {
                if (part == null)
                {
                    return false;
                }

                int targetColor = part.BaseColor;
                int targetOpacity = 0;

                if (mixOpacity > 0
                    && part.MixNodes != null
                    && mixColor >= 0
                    && mixColor < part.MixNodes.Length
                    && part.MixNodes[mixColor] != null
                    && part.BaseColor != mixColor)
                {
                    targetColor = mixColor;
                    targetOpacity = Math.Max(0, Math.Min(100, mixOpacity));
                }

                if (part.MixColor == targetColor && part.MixOpacity == targetOpacity)
                {
                    return false;
                }

                part.MixColor = targetColor;
                part.MixOpacity = targetOpacity;
                return true;
            }

            private string SelectEffectActionName(int layerIndex, string actionName)
            {
                List<string> availableActions = this.canvas.EffectActions[layerIndex];
                if (availableActions == null || availableActions.Count == 0)
                {
                    return null;
                }

                foreach (string candidate in new[] { actionName, "default", "effect", "effect2", "stand1" })
                {
                    if (!string.IsNullOrWhiteSpace(candidate)
                        && availableActions.Any(value => string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase)))
                    {
                        return candidate;
                    }
                }

                return availableActions[0];
            }

            private void PrepareSlotForGear(int id)
            {
                switch (GetAppearanceSlotKey(id))
                {
                    case AppearanceSlotKey.Coat:
                        this.canvas.Longcoat = null;
                        break;

                    case AppearanceSlotKey.Longcoat:
                        this.canvas.Coat = null;
                        this.canvas.Pants = null;
                        break;

                    case AppearanceSlotKey.Pants:
                        this.canvas.Longcoat = null;
                        break;
                }
            }

            private void UpdateWeaponState(int id, Wz_Node gearNode)
            {
                GearType type = Gear.GetGearType(id);
                if (Gear.IsWeapon(type) || type == GearType.shovel || type == GearType.pickaxe)
                {
                    this.weaponType = (int)type;
                    this.canvas.WeaponType = this.weaponType;
                    return;
                }

                if (type != GearType.cashWeapon)
                {
                    return;
                }

                List<int> cashWeaponTypes = GetCashWeaponTypes(gearNode);
                if (cashWeaponTypes.Count == 0)
                {
                    return;
                }

                if (this.weaponType != 0 && cashWeaponTypes.Contains(this.weaponType))
                {
                    this.canvas.WeaponType = this.weaponType;
                    return;
                }

                this.weaponType = cashWeaponTypes[0];
                this.canvas.WeaponType = this.weaponType;
            }

            private static List<int> GetCashWeaponTypes(Wz_Node gearNode)
            {
                return gearNode?.Nodes
                    .Select(node => int.TryParse(node.Text, out int typeValue) ? (int?)typeValue : null)
                    .Where(typeValue => typeValue.HasValue)
                    .Select(typeValue => typeValue.Value)
                    .OrderBy(typeValue => typeValue)
                    .ToList()
                    ?? new List<int>();
            }
        }

        internal sealed class WzSearchContext : IDisposable
        {
            private readonly Wz_Structure wzStructure;
            private readonly EventInfo wzFileFindingEvent;
            private readonly FindWzEventHandler handler;

            public WzSearchContext(string baseWzPath)
            {
                ResetAppearanceNodeCache();
                this.wzStructure = new Wz_Structure();
                OpenWzLikeMainForm(this.wzStructure, baseWzPath);

                this.handler = this.OnWzFileFinding;
                this.wzFileFindingEvent = typeof(PluginManager).GetEvent("WzFileFinding", BindingFlags.Static | BindingFlags.NonPublic);
                if (this.wzFileFindingEvent == null)
                {
                    throw new MissingMemberException("PluginManager.WzFileFinding was not found.");
                }

                MethodInfo addMethod = this.wzFileFindingEvent.GetAddMethod(true);
                addMethod.Invoke(null, new object[] { this.handler });
            }

            public void Dispose()
            {
                MethodInfo removeMethod = this.wzFileFindingEvent?.GetRemoveMethod(true);
                removeMethod?.Invoke(null, new object[] { this.handler });
                this.wzStructure.Clear();
                ResetAppearanceNodeCache();
            }

            private static void OpenWzLikeMainForm(Wz_Structure wz, string wzFilePath)
            {
                string[] msFileExtensions = { ".ms", ".mn" };
                string extension = Path.GetExtension(wzFilePath);

                if (msFileExtensions.Any(ext => string.Equals(extension, ext, StringComparison.OrdinalIgnoreCase)))
                {
                    wz.LoadMsFile(wzFilePath);
                    return;
                }

                if (wz.IsKMST1125WzFormat(wzFilePath))
                {
                    wz.LoadKMST1125DataWz(wzFilePath);

                    if (string.Equals(Path.GetFileName(wzFilePath), "Base.wz", StringComparison.OrdinalIgnoreCase))
                    {
                        string packsDir = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(wzFilePath)), "Packs");
                        if (Directory.Exists(packsDir))
                        {
                            foreach (string ext in msFileExtensions)
                            {
                                foreach (string msFile in Directory.GetFiles(packsDir, $"*{ext}"))
                                {
                                    wz.LoadMsFile(msFile);
                                }
                            }
                        }
                    }

                    return;
                }

                wz.Load(wzFilePath, true);
            }

            private void OnWzFileFinding(object sender, FindWzEventArgs e)
            {
                string[] fullPath = null;
                if (!string.IsNullOrEmpty(e.FullPath))
                {
                    fullPath = e.FullPath.Split('/', '\\');
                    e.WzType = Enum.TryParse(fullPath[0], true, out Wz_Type wzType) ? wzType : Wz_Type.Unknown;
                }

                var preSearch = new List<Wz_Node>();
                if (e.WzType != Wz_Type.Unknown)
                {
                    Wz_Structure targetStructure = e.WzFile?.WzStructure ?? this.wzStructure;
                    Wz_File baseWz = null;
                    bool foundTypedWz = false;

                    foreach (Wz_File wzFile in targetStructure.wz_files)
                    {
                        if (wzFile.Type == e.WzType && wzFile.Node?.Nodes.Count > 0)
                        {
                            preSearch.Add(wzFile.Node);
                            foundTypedWz = true;
                        }

                        if (wzFile.Type == Wz_Type.Base)
                        {
                            baseWz = wzFile;
                        }
                    }

                    if (baseWz != null && !foundTypedWz)
                    {
                        string key = e.WzType.ToString();
                        foreach (Wz_Node node in baseWz.Node.Nodes)
                        {
                            if (node.Text == key && node.Nodes.Count > 0)
                            {
                                preSearch.Add(node);
                            }
                        }
                    }
                }

                if (fullPath == null || fullPath.Length <= 1)
                {
                    if (e.WzType != Wz_Type.Unknown && preSearch.Count > 0)
                    {
                        e.WzNode = preSearch[0];
                        e.WzFile = preSearch[0].Value as Wz_File;
                    }
                    return;
                }

                foreach (Wz_Node wzFileNode in preSearch)
                {
                    Wz_Node searchNode = wzFileNode;
                    for (int i = 1; i < fullPath.Length && searchNode != null; i++)
                    {
                        searchNode = searchNode.Nodes[fullPath[i]];
                        Wz_Image image = searchNode.GetValueEx<Wz_Image>(null);
                        if (image != null)
                        {
                            searchNode = image.TryExtract() ? image.Node : null;
                        }
                    }

                    if (searchNode != null)
                    {
                        e.WzNode = searchNode;
                        e.WzFile = wzFileNode.Value as Wz_File;
                        return;
                    }
                }

                e.WzNode = null;
                e.WzFile = null;
            }
        }

        internal sealed class PreviewFrameSet : IDisposable
        {
            public List<Bitmap> Frames { get; } = new List<Bitmap>();

            public List<int> Delays { get; } = new List<int>();

            public int Count => this.Frames.Count;

            public void Dispose()
            {
                foreach (Bitmap bitmap in this.Frames)
                {
                    bitmap?.Dispose();
                }

                this.Frames.Clear();
                this.Delays.Clear();
            }
        }

        private sealed class RenderTrack : IDisposable
        {
            public string Key { get; set; }
            public Rectangle Bounds { get; set; }
            public List<RenderFrame> Frames { get; set; }
            public int? GridRow { get; set; }
            public int? GridColumn { get; set; }

            public int TotalDuration => this.Frames.Sum(frame => frame.Delay);

            public void Dispose()
            {
                foreach (RenderFrame frame in this.Frames)
                {
                    frame.Bitmap?.Dispose();
                }

                this.Frames.Clear();
            }
        }

        private sealed class RenderFrame
        {
            public Bitmap Bitmap { get; set; }
            public Point Origin { get; set; }
            public int Delay { get; set; }
        }

        private sealed class TimelineState
        {
            public int[] FrameIndexes { get; set; }
            public int Delay { get; set; }
        }

        internal enum RenderMode
        {
            ActionStrip = 0,
            DyeGrid = 1,
            ExactDye = 2,
        }
    }
}
