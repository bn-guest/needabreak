﻿/*
 NEED A BREAK is an application intended to help you take care of your health while you work on a computer. 
 It will encourage you to regularly have a break in order to rest your back and your eyes.
    Copyright (C) 2020  Benoit Rocco

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
using MahApps.Metro.Controls;
using NeedABreak.Extensions;
using NeedABreak.Utils;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Windows.ApplicationModel;

namespace NeedABreak
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public MainWindow()
        {
            App.Logger.Debug("MainWindow ctor start");
            InitializeComponent();
            LaunchOnStartupMenuItem.Checked += LaunchOnStartupMenuItem_Checked;
            LaunchOnStartupMenuItem.Unchecked += LaunchOnStartupMenuItem_Unchecked;
            LoadUserSettings();
            App.Logger.Debug("MainWindow ctor end");
        }

        private void LoadUserSettings()
        {
            AutomaticSuspensionMenuItem.IsChecked = Properties.Settings.Default.AutomaticSuspension;
        }

        private void CheckStartupState(StartupTaskState startupTaskState)
        {
            switch (startupTaskState)
            {
                case StartupTaskState.Disabled:
                    LaunchOnStartupMenuItem.IsChecked = false;
                    break;
                case StartupTaskState.DisabledByUser:
                    // Task is disabled and user must enable it manually.
                    LaunchOnStartupMenuItem.IsChecked = false;
                    LaunchOnStartupMenuItem.IsEnabled = false;
                    LaunchOnStartupMenuItem.Header = Properties.Resources.executer_au_demarrage_user_disabled;
                    App.Logger.Error("Startup task is disabled and user must enable it manually.");
                    break;
                case StartupTaskState.Enabled:
                    LaunchOnStartupMenuItem.IsChecked = true;
                    break;
                case StartupTaskState.DisabledByPolicy:
                    LaunchOnStartupMenuItem.IsChecked = false;
                    LaunchOnStartupMenuItem.IsEnabled = false;
                    LaunchOnStartupMenuItem.Header = Properties.Resources.executer_au_demarrage_admin_disabled;
                    App.Logger.Error("Startup task is disabled by group policy.");
                    break;
                case StartupTaskState.EnabledByPolicy:
                    LaunchOnStartupMenuItem.IsChecked = true;
                    LaunchOnStartupMenuItem.IsEnabled = false;
                    LaunchOnStartupMenuItem.Header = Properties.Resources.executer_au_demarrage_admin_disabled;
                    App.Logger.Error("Startup task is enabled by group policy.");
                    break;
                default:
                    App.Logger.Error($"Unknown state {startupTaskState}");
                    break;
            }
        }

        private async void LaunchOnStartupMenuItem_Unchecked(object sender, RoutedEventArgs e)
        {
            StartupTask startupTask = await StartupTask.GetAsync("NeedABreak.StartupTask");

            switch (startupTask.State)
            {
                case StartupTaskState.Disabled:
                    App.Logger.Debug("Startup Task is already disabled.");
                    break;
                case StartupTaskState.DisabledByUser:
                case StartupTaskState.DisabledByPolicy:
                case StartupTaskState.EnabledByPolicy:
                    CheckStartupState(startupTask.State);
                    break;
                case StartupTaskState.Enabled:
                    startupTask.Disable();
                    App.Logger.Debug("Startup Task disabled.");
                    break;
                default:
                    App.Logger.Error($"Unknown state {startupTask.State}");
                    break;
            }
        }

        private async void LaunchOnStartupMenuItem_Checked(object sender, RoutedEventArgs e)
        {
            StartupTask startupTask = await StartupTask.GetAsync("NeedABreak.StartupTask");

            switch (startupTask.State)
            {
                case StartupTaskState.Disabled:
                    // Task is disabled but can be enabled.
                    StartupTaskState newState = await startupTask.RequestEnableAsync();
                    CheckStartupState(newState);
                    App.Logger.Debug($"startupTask.RequestEnableAsync State = {newState}");
                    break;
                case StartupTaskState.DisabledByUser:
                case StartupTaskState.DisabledByPolicy:
                case StartupTaskState.EnabledByPolicy:
                    CheckStartupState(startupTask.State);
                    break;
                case StartupTaskState.Enabled:
                    App.Logger.Debug("Startup Task is already enabled.");
                    break;
                default:
                    App.Logger.Error($"Unknown state {startupTask.State}");
                    break;
            }
        }

        private const string CoffeeSuspendedUri = "pack://application:,,,/NeedABreak;component/coffee_suspended.ico";
        private const string CoffeeCupUri = "pack://application:,,,/NeedABreak;component/coffee cup.ico";
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private bool _imminentLocking = false;

        public async Task StartLockWorkStationAsync()
        {
            _imminentLocking = true;
            var viewModel = GetViewModel();
            Show();
            var token = _cancellationTokenSource.Token;

            for (int i = 1000; i >= 0; i--)
            {
                viewModel.CentiSeconds = i;
                viewModel.Seconds = i / 100;

                try
                {
                    await Task.Delay(10, token);
                }
                catch (TaskCanceledException)
                {
                    Hide();
                    _imminentLocking = false;
                    return;
                }

                if (token.IsCancellationRequested)
                {
                    Hide();
                    _imminentLocking = false;
                    return;
                }
            }

            Hide();
            SessionLock.LockSession();
            _imminentLocking = false;
        }

        public void ShowCustomBalloon()
        {
            var template = (DataTemplate)FindResource("BalloonTipTemplate");
            var balloon = (FrameworkElement)template.LoadContent();
            balloon.Name = "uxBalloonTip";
            uxTaskbarIcon.ShowCustomBalloon(balloon, System.Windows.Controls.Primitives.PopupAnimation.Scroll, 10000);
        }

        public MainWindowViewModel GetViewModel()
        {
            return DataContext as MainWindowViewModel;
        }

        private void SetToolTipText(string text)
        {
            var viewModel = GetViewModel();
            viewModel.TrayToolTipText = text;
        }

        private static void ShowSettingsWindow()
        {
            Window settingsWindow = null;

            foreach (Window window in App.Current.Windows)
            {
                if (window is SettingsWindow)
                {
                    settingsWindow = window;
                    break;
                }
            }

            if (settingsWindow == null)
            {
                settingsWindow = new SettingsWindow();
                settingsWindow.DataContext = new SettingsWindowViewModel();
            }

            settingsWindow.Show();
            settingsWindow.Activate();
        }

        private void TaskbarIcon_PreviewTrayToolTipOpen(object sender, RoutedEventArgs e)
        {
            UpdateToolTip();
        }

        public void UpdateToolTip()
        {
            string tooltipTitle;

            if (App.IsSuspended)
            {
                tooltipTitle = Properties.Resources.suspended_title;
            }
            else if (_imminentLocking)
            {
                tooltipTitle = Properties.Resources.Imminent_locking;
            }
            else
            {
                var minutesLeft = App.GetMinutesLeft();

                if (minutesLeft <= 1)
                {
                    tooltipTitle = Properties.Resources.Less_than_a_minute_before_locking;
                }
                else
                {
                    minutesLeft = Math.Round(minutesLeft);

                    if (minutesLeft == 1)
                    {
                        tooltipTitle = Properties.Resources.one_minute_before_locking;
                    }
                    else
                    {
                        tooltipTitle = string.Format(Properties.Resources.minutes_before_locking, minutesLeft);
                    }
                }
            }

            TimeSpan todayScreenTime = App.GetTodayScreenTime();
            string humanFriendlyTime = todayScreenTime.ToHumanFriendlyString();
            string todayScreenTimeText = string.Format(Properties.Resources.today_screen_time, humanFriendlyTime);
            SetToolTipText($"{tooltipTitle}\r\n-\r\n{todayScreenTimeText}");
        }

        private void CloseMenuItem_Click(object sender, RoutedEventArgs e)
        {
            App.Current.Shutdown();
        }

        private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ShowSettingsWindow();
        }

        private Lazy<AboutBoxWindow> aboutBoxFactory =
            new Lazy<AboutBoxWindow>(() => new AboutBoxWindow());

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            AboutBoxWindow aboutBox = aboutBoxFactory.Value;
            aboutBox.Show();
            aboutBox.Activate();
        }

        private void CloseBalloon_Click(object sender, RoutedEventArgs e)
        {
            uxTaskbarIcon.CloseBalloon();
        }

        private void ReporterBalloon_Click(object sender, RoutedEventArgs e)
        {
            uxTaskbarIcon.CloseBalloon();
            App.ShiftCountdown();
        }

        private void AnnulerBalloon_Click(object sender, RoutedEventArgs e)
        {
            uxTaskbarIcon.CloseBalloon();
            App.ResetCountdown();    // annulation, le compte à rebours repart de zéro
        }

        private void ReporterButton_Click(object sender, RoutedEventArgs e)
        {
            Interlocked.Exchange(ref _cancellationTokenSource, new CancellationTokenSource()).Cancel();
            App.ShiftCountdown();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Interlocked.Exchange(ref _cancellationTokenSource, new CancellationTokenSource()).Cancel();
            App.ResetCountdown();
        }

        private void LockButton_Click(object sender, RoutedEventArgs e)
        {
            Interlocked.Exchange(ref _cancellationTokenSource, new CancellationTokenSource()).Cancel();
            SessionLock.LockSession();
        }

        private void SuspendResumeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (App.IsSuspended)
            {
                App.Resume();
            }
            else
            {
                App.Suspend();
            }
        }

        public void OnSessionUnlock()
        {
            if (App.IsSuspended && App.SuspensionCause == SuspensionCause.Manual)
            {
                ShowSuspendBalloonTip();
            }
        }

        private void ShowResumeBalloonTip()
        {
            uxTaskbarIcon.ShowBalloonTip(
                Properties.Resources.resumed_title,
                Properties.Resources.resumed_message,
                Hardcodet.Wpf.TaskbarNotification.BalloonIcon.None);
        }

        private void ShowSuspendBalloonTip()
        {
            uxTaskbarIcon.ShowBalloonTip(
                Properties.Resources.suspend,
                Properties.Resources.suspended_message,
                Hardcodet.Wpf.TaskbarNotification.BalloonIcon.None);
        }

        public void NotifySuspensionStateChanged()
        {
            var viewModel = GetViewModel();

            if (App.IsSuspended)
            {
                viewModel.UpdateSuspendResumeMenuItemToResume();
                ShowSuspendBalloonTip();
                uxTaskbarIcon.IconSource = new BitmapImage(new Uri(CoffeeSuspendedUri, UriKind.Absolute));
            }
            else
            {
                viewModel.UpdateSuspendResumeMenuItemToSuspend();
                ShowResumeBalloonTip();
                uxTaskbarIcon.IconSource = new BitmapImage(new Uri(CoffeeCupUri, UriKind.Absolute));
            }

            viewModel.NotifyIsSuspendedChanged();
        }

        private void AutomaticSuspensionMenuItem_Checked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.AutomaticSuspension = true;
            Properties.Settings.Default.Save();
        }

        private void AutomaticSuspensionMenuItem_Unchecked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.AutomaticSuspension = false;
            Properties.Settings.Default.Save();

            if (App.IsSuspended && App.SuspensionCause == SuspensionCause.Automatic)
            {
                App.Resume();
            }
        }

        private async void StartLockWorkStation_Click(object sender, RoutedEventArgs e)
        {
            await StartLockWorkStationAsync();
        }

        private async void ContextMenu_Initialized(object sender, EventArgs e)
        {
            StartupTask startupTask = await StartupTask.GetAsync("NeedABreak.StartupTask");
            CheckStartupState(startupTask.State);
        }
    }
}
