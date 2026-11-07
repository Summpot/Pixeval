using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AnimatedControls.Avalonia;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.Utilities;
using Pixeval.Views.Entry;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SkeletonTest
{
    [ClassInitialize]
    public static void Initialize(TestContext _)
    {
        I18NManager.CandidatePaths.Add(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Pixeval")));
        I18NManager.Register(new JsonMarkdownLangPlugin(), LanguageHelper.DefaultLanguage);
    }

    [TestMethod]
    public async Task ShimmerClockStopsWhenHiddenDetachedOrRetemplated()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        _ = await session.Dispatch<bool>(async () =>
        {
            var skeleton = new Skeleton { Width = 320, Height = 180 };
            var parent = new Border { Child = skeleton };
            var window = new Window { Width = 500, Height = 400, Content = parent };
            try
            {
                window.Show();
                await RenderAsync(window);
                var shimmer = Shimmer(skeleton);
                var visual = ElementComposition.GetElementVisual(shimmer)!;
                Assert.AreEqual(1, ClockCount(visual), "Only the visible skeleton should own a composition clock.");
                Assert.IsTrue(shimmer.Width > 0 && shimmer.Height == skeleton.Bounds.Height);
                parent.IsVisible = false;
                await RenderAsync(window);
                Assert.IsFalse(skeleton.IsEffectivelyVisible);
                Assert.AreEqual(0, ClockCount(visual), "Hiding an ancestor must stop background shimmer work.");
                parent.IsVisible = true;
                await RenderAsync(window);
                Assert.AreEqual(1, ClockCount(visual), "Showing the same skeleton starts one animation.");
                skeleton.IsVisible = false;
                await RenderAsync(window);
                Assert.AreEqual(0, ClockCount(visual));
                skeleton.IsVisible = true;
                skeleton.Width = 450;
                skeleton.ShimmerPeriod = TimeSpan.FromMilliseconds(800);
                await RenderAsync(window);
                Assert.AreEqual(1, ClockCount(visual), "Resizing and changing duration replace the running animation.");
                Assert.AreEqual(450 * .75, shimmer.Width);
                skeleton.ShimmerPeriod = TimeSpan.Zero;
                Assert.IsTrue(skeleton.ShimmerPeriod > TimeSpan.Zero);
                parent.Child = null;
                await RenderAsync(window);
                Assert.AreEqual(0, ClockCount(visual), "Removing a loading control releases its animation clock.");
                parent.Child = skeleton;
                await RenderAsync(window);
                Assert.AreEqual(1, ClockCount(visual));

                skeleton.Template = new FuncControlTemplate<Skeleton>((_, scope) =>
                {
                    var replacement = new Border { Name = "PART_Shimmer", Background = Brushes.Gray };
                    scope.Register("PART_Shimmer", replacement);
                    return new Border { Child = replacement };
                });
                await RenderAsync(window);
                Assert.AreNotSame(shimmer, Shimmer(skeleton));
                Assert.AreEqual(1, ClockCount(visual), "Retemplating must not retain an old clock beside the replacement.");
                skeleton.IsVisible = false;
                await RenderAsync(window);
                Assert.AreEqual(0, ClockCount(visual));
            }
            finally
            {
                window.Content = null;
                window.Close();
            }
            return true;
        }, CancellationToken.None);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RealEntryAndAvatarKeepLoadingEmptyAndReadyStates(bool dark)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        _ = await session.Dispatch<bool>(async () =>
        {
            using var bitmap = IAnimatedBitmap.Load(AssetLoader.Open(new Uri("avares://Pixeval/Assets/Platforms/pixiv.png")), true);
            bitmap.Init();
            var avatar = new AvatarImage { Width = 96, Height = 96 };
            var entry = new EntryView { Height = 260, HasNoItem = true, IsLoadingMore = true };
            var root = new StackPanel { Margin = new Thickness(20), Spacing = 16, Children = { avatar, entry } };
            var window = new Window { Width = 500, Height = 500, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light, Content = root };
            try
            {
                window.Show();
                await RenderAsync(window);
                var avatarSkeleton = avatar.GetVisualDescendants().OfType<Skeleton>().Single();
                var entrySkeleton = entry.GetVisualDescendants().OfType<Skeleton>().Single();
                var progress = entry.GetVisualDescendants().OfType<ProgressBar>().Single();
                Assert.IsTrue(avatarSkeleton.IsEffectivelyVisible && entrySkeleton.IsEffectivelyVisible);
                Assert.IsTrue(progress.IsIndeterminate && progress.IsEffectivelyVisible);
                Capture(window, $"skeleton-loading-{dark}");
                var originalColor = Assert.IsInstanceOfType<ISolidColorBrush>(entrySkeleton.Background).Color;
                window.RequestedThemeVariant = dark ? ThemeVariant.Light : ThemeVariant.Dark;
                await RenderAsync(window);
                Assert.AreNotEqual(originalColor, Assert.IsInstanceOfType<ISolidColorBrush>(entrySkeleton.Background).Color);
                avatar.Source = bitmap;
                entry.IsLoadingMore = false;
                await RenderAsync(window);
                Assert.IsFalse(avatarSkeleton.IsEffectivelyVisible || entrySkeleton.IsEffectivelyVisible);
                Assert.IsFalse(progress.IsIndeterminate || progress.IsEffectivelyVisible);
                var empty = entry.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == I18NManager.GetResource(EntryViewResources.ResultIsEmptyTextBlock));
                Assert.IsTrue(empty.IsEffectivelyVisible);
                entry.HasNoItem = false;
                var content = new Button { Content = "Existing entry action" };
                entry.Content = content;
                entry.IsLoadingMore = true;
                await RenderAsync(window);
                Assert.IsTrue(content.IsEffectivelyVisible && content.Focus());
                Assert.IsFalse(entrySkeleton.IsEffectivelyVisible || empty.IsEffectivelyVisible);
                Assert.IsTrue(progress.IsIndeterminate && progress.IsEffectivelyVisible, "Loading another page keeps existing content visible.");
                entry.IsLoadingMore = false;
                avatar.Source = null;
                await RenderAsync(window);
                Assert.IsTrue(avatarSkeleton.IsEffectivelyVisible, "Reusing an avatar without a source restores its placeholder.");
                Capture(window, $"skeleton-reused-{dark}");
                avatar.Source = null;
            }
            finally
            {
                window.Content = null;
                window.Close();
            }
            return true;
        }, CancellationToken.None);
    }

    private static Border Shimmer(Skeleton skeleton) => skeleton.GetVisualDescendants().OfType<Border>().Single(border => border.Name is "PART_Shimmer");

    [TestMethod]
    public async Task DetachedSkeletonDoesNotRemainSubscribedToItsLiveAncestors()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        _ = await session.Dispatch<bool>(async () =>
        {
            var parent = new Border();
            var window = new Window { Width = 500, Height = 400, Content = parent };
            try
            {
                window.Show();
                var detached = MountAndDetach(window, parent);
                await RenderAsync(window);
                for (var attempt = 0; attempt < 4 && detached.IsAlive; attempt++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    await RenderAsync(window);
                }
                Assert.IsFalse(detached.IsAlive, "A live window/parent must not retain a detached skeleton through visibility handlers.");
                parent.IsVisible = false;
                parent.IsVisible = true;
                await RenderAsync(window);
            }
            finally
            {
                window.Close();
            }
            return true;
        }, CancellationToken.None);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference MountAndDetach(Window window, Border parent)
    {
        var skeleton = new Skeleton();
        parent.Child = skeleton;
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        var reference = new WeakReference(skeleton);
        parent.Child = null;
        return reference;
    }

    private static int ClockCount(CompositionVisual visual)
    {
        // Avalonia 12.1.5 has no public clock diagnostics. Read only the compositor's
        // scheduler in this UI-thread-rendered fixture; never mutate framework internals.
        var server = typeof(CompositionObject).GetProperty("Server", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(visual)!;
        var compositor = server.GetType().GetProperty("Compositor")!.GetValue(server)!;
        var animations = compositor.GetType().GetProperty("Animations")!.GetValue(compositor)!;
        var clocks = animations.GetType().GetField("_clockItems", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(animations)!;
        return (int) clocks.GetType().GetProperty("Count")!.GetValue(clocks)!;
    }

    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(80);
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }

    private static void Capture(Window window, string name)
    {
        if (Environment.GetEnvironmentVariable("PIXEVAL_SKELETON_CAPTURE") is not { Length: > 0 } directory)
            return;
        Directory.CreateDirectory(directory);
        using var frame = window.CaptureRenderedFrame();
        Assert.IsNotNull(frame);
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }
}
