using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AutoSettingsPage.Models;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using FluentIcons.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.AppManagement;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.Models.Navigation;
using Pixeval.Services;
using Pixeval.Utilities;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Settings;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ViewerSubPageCompositionTest
{
    [TestMethod]
    public async Task CreatePageAndPushAsync_CreateSettingsPages()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ImageViewerRenderingTest.ViewerTestApplication));
        await session.Dispatch(() =>
        {
            I18NManager.Register(new JsonMarkdownLangPlugin(), LanguageHelper.DefaultLanguage);
            var appProperty = typeof(App).GetProperty(nameof(App.AppViewModel), BindingFlags.Public | BindingFlags.Static)!;
            var previous = appProperty.GetValue(null);
            var appModel = (AppViewModel) RuntimeHelpers.GetUninitializedObject(typeof(AppViewModel));
            typeof(AppViewModel).GetProperty(nameof(AppViewModel.NavigationMenuYamlText))!
                .SetValue(appModel, NavigationMenuYaml.DefaultYaml);
            appProperty.SetValue(null, appModel);
            try
            {
                var service = new NavigationService();
                var group = new EmptySettingsGroup();

                Assert.IsInstanceOfType<AboutPage>(service.CreatePage(typeof(AboutPage)));
                Assert.IsInstanceOfType<HelpPage>(service.CreatePage(typeof(HelpPage)));
                Assert.IsInstanceOfType<NavigationSettingsPage>(service.CreatePage(typeof(NavigationSettingsPage)));
                var settingsPage = Assert.IsInstanceOfType<SettingsSubView>(service.CreatePage(typeof(SettingsSubView), group));
                Assert.AreEqual(group.Header, settingsPage.Header);

                AssertPushed<AboutPage>(service);
                AssertPushed<HelpPage>(service);
                AssertPushed<NavigationSettingsPage>(service);
                AssertPushed<SettingsSubView>(service, group);
                Dispatcher.UIThread.RunJobs();
            }
            finally
            {
                appProperty.SetValue(null, previous);
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public async Task AutoPlayTick_MovesNextWithoutSavingSettings()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ImageViewerRenderingTest.ViewerTestApplication));
        await session.Dispatch(() =>
        {
            Assert.IsFalse(typeof(IllustrationViewerPageViewModel)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(field => field.FieldType == typeof(DispatcherTimer)));

            var target = new RecordingAutoPlayTarget { IsAutoPlaying = true, AutoPlayInterval = 5 };
            var control = new Border { DataContext = target };
            IllustrationAutoPlayBehavior.SetIsEnabled(control, true);

            Assert.IsTrue(IllustrationAutoPlayBehavior.IsTimerRunning(control));
            var interval = target.AutoPlayInterval;
            IllustrationAutoPlayBehavior.Tick(control.DataContext);
            Assert.AreEqual(1, target.MoveCount);
            Assert.AreEqual(0, target.SaveCount);
            Assert.AreEqual(interval, target.AutoPlayInterval);

            target.IsAutoPlaying = false;
            Assert.IsFalse(IllustrationAutoPlayBehavior.IsTimerRunning(control));
            IllustrationAutoPlayBehavior.Tick(control.DataContext);
            Assert.AreEqual(1, target.MoveCount);
            Assert.AreEqual(0, target.SaveCount);
        }, CancellationToken.None);
    }

    private static void AssertPushed<TPage>(NavigationService service, object? parameter = null)
        where TPage : Page
    {
        var navigation = new NavigationPage();
        navigation.Content = new ContentPage();
        PumpUntil(() => navigation is { IsNavigating: false, CurrentPage: not null });
        var host = navigation.CurrentPage;
        Assert.IsNotNull(host);

        var push = service.PushAsync<TPage>(host, parameter);
        PumpUntil(() => push.IsCompleted);
        Assert.IsTrue(push.IsCompletedSuccessfully);
        Assert.IsInstanceOfType<TPage>(navigation.CurrentPage);
    }

    private static void PumpUntil(Func<bool> done)
    {
        for (var attempt = 0; attempt < 40 && !done(); attempt++)
            Dispatcher.UIThread.RunJobs();
    }

    private sealed class RecordingAutoPlayTarget : IIllustrationAutoPlayTarget, INotifyPropertyChanged
    {
        private bool _isAutoPlaying;

        public int MoveCount { get; private set; }

        public int SaveCount { get; private set; }

        public int AutoPlayInterval { get; set; }

        public bool IsAutoPlaying
        {
            get => _isAutoPlaying;
            set
            {
                if (_isAutoPlaying == value)
                    return;

                _isAutoPlaying = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAutoPlaying)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void MoveAutoPlayNext() => MoveCount++;

        public void SaveAutoPlaySettings() => SaveCount++;
    }

    private sealed class EmptySettingsGroup : ISettingsGroup
    {
        public string Token => "group";

        public string Header => "Group";

        public string Description => "";

        public Symbol Icon => Symbol.Settings;

        public Uri? DescriptionUri => null;

        public int Count => 0;

        public ISettingsEntry this[int index] => throw new ArgumentOutOfRangeException(nameof(index));

        public IEnumerator<ISettingsEntry> GetEnumerator()
        {
            yield break;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
