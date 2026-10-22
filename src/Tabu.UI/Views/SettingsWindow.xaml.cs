using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Tabu.UI.Services;
using Tabu.UI.ViewModels;

namespace Tabu.UI.Views;

public partial class SettingsWindow : Wpf.Ui.Controls.FluentWindow
{
    // Combo item order in SettingsWindow.xaml. The enum values are pixel
    // sizes, so they cannot double as combo indices.
    private static readonly Tabu.Domain.Entities.BarSize[] BarSizeOptions =
    {
        Tabu.Domain.Entities.BarSize.Small,
        Tabu.Domain.Entities.BarSize.Medium,
        Tabu.Domain.Entities.BarSize.Large,
    };

    private static readonly Tabu.Domain.Entities.ClockSize[] ClockSizeOptions =
    {
        Tabu.Domain.Entities.ClockSize.Small,
        Tabu.Domain.Entities.ClockSize.Medium,
        Tabu.Domain.Entities.ClockSize.Large,
    };

    private readonly MainViewModel _viewModel;
    private bool _initialized;

    public SettingsWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        Loaded += OnLoaded;

        // Fit small or high-scale displays: WorkArea already excludes the
        // space reserved by the bar itself.
        var workArea = SystemParameters.WorkArea;
        Width = Math.Min(Width, workArea.Width - 48);
        Height = Math.Min(Height, workArea.Height - 48);

        // Sync controls with current state. Combo indices follow the item
        // order declared in SettingsWindow.xaml.
        BarPlacementCombo.SelectedIndex = _viewModel.IsBarOnAllMonitors ? 1 : 0;
        DetectionModeCombo.SelectedIndex = _viewModel.IsDetectSameScreenOnly ? 1 : 0;

        ThemeCombo.SelectedIndex = _viewModel.AppTheme switch
        {
            AppTheme.Dark => 1,
            AppTheme.Light => 2,
            _ => 0,
        };

        OpacitySlider.Value = _viewModel.BarOpacity * 100;
        OpacityValueText.Text = $"{(int)(_viewModel.BarOpacity * 100)}%";

        TabWidthCombo.SelectedIndex = _viewModel.UseFixedTabWidth ? 1 : 0;
        BrandingToggle.IsChecked = _viewModel.ShowBranding;

        // Language combo
        LanguageCombo.ItemsSource = LocalizationManager.AvailableLanguages;
        var currentLang = LocalizationManager.AvailableLanguages
            .FirstOrDefault(l => l.Code == _viewModel.Language)
            ?? LocalizationManager.AvailableLanguages[0];
        LanguageCombo.SelectedItem = currentLang;

        // Accent color: prime the textbox with the canonical hex form so
        // legacy preset codes ("blue", "red", …) saved before v1.8.0 are
        // surfaced to the user as proper #RRGGBB strings.
        var canonicalAccent = AccentColorManager.ResolveCanonicalHex(_viewModel.AccentColor);
        AccentColorBox.Text = canonicalAccent;
        SyncAccentWheelFromHex(canonicalAccent);

        BarVisibilityCombo.SelectedIndex = _viewModel.AutoHideBar ? 1 : 0;
        StartupToggle.IsChecked = _viewModel.LaunchAtStartup;
        ClockToggle.IsChecked = _viewModel.ShowClock;
        ClockSizeCombo.SelectedIndex = Array.IndexOf(ClockSizeOptions, _viewModel.ClockSize);
        UpdateClockSizeAvailability();

        NotificationsToggle.IsChecked = _viewModel.ShowNotificationBadges;

        NotificationDotSizeSlider.Value = _viewModel.NotificationDotSize;
        NotificationDotSizeValueText.Text = $"{(int)_viewModel.NotificationDotSize} px";
        NotificationDotColorBox.Text = _viewModel.NotificationDotColor;
        SyncWheelFromHex(_viewModel.NotificationDotColor);
        UpdateNotificationDotPanelAvailability();

        // Active tab tint controls — load from VM. Slider/text changes
        // round-trip through the same _Changed handlers used at runtime.
        ActiveTabColorBox.Text = _viewModel.ActiveTabColor;
        ActiveTabOpacitySlider.Value = _viewModel.ActiveTabOpacity;
        ActiveTabOpacityValueText.Text = $"{(int)_viewModel.ActiveTabOpacity}%";
        SyncActiveTabWheelFromHex(_viewModel.ActiveTabColor);

        BarSizeCombo.SelectedIndex = Array.IndexOf(BarSizeOptions, _viewModel.BarSize);
        BlurToggle.IsChecked = _viewModel.UseBlurEffect;
        UpdateThemeLockState();
        AutoUpdatesToggle.IsChecked = _viewModel.AutoCheckUpdates;

        ApplyFluentTheme();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closed += (_, _) => _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _initialized = true;
    }

    private void BarPlacement_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        bool allMonitors = BarPlacementCombo.SelectedIndex == 1;

        if (allMonitors != _viewModel.IsBarOnAllMonitors)
        {
            _viewModel.IsBarOnAllMonitors = allMonitors;
        }
    }

    private void DetectionMode_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        bool sameScreen = DetectionModeCombo.SelectedIndex == 1;

        if (sameScreen != _viewModel.IsDetectSameScreenOnly)
        {
            _viewModel.IsDetectSameScreenOnly = sameScreen;
        }
    }

    private void Theme_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        AppTheme theme = ThemeCombo.SelectedIndex switch
        {
            1 => AppTheme.Dark,
            2 => AppTheme.Light,
            _ => AppTheme.System,
        };

        if (theme != _viewModel.AppTheme)
        {
            _viewModel.AppTheme = theme;
        }
    }

    private void Opacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_initialized) return;

        double opacity = e.NewValue / 100.0;
        _viewModel.BarOpacity = opacity;
        OpacityValueText.Text = $"{(int)e.NewValue}%";
    }

    private void TabWidth_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        bool fixedWidth = TabWidthCombo.SelectedIndex == 1;

        if (fixedWidth != _viewModel.UseFixedTabWidth)
        {
            _viewModel.UseFixedTabWidth = fixedWidth;
        }
    }

    private void Branding_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        bool show = BrandingToggle.IsChecked == true;

        if (show != _viewModel.ShowBranding)
        {
            _viewModel.ShowBranding = show;
        }
    }

    private void Language_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_initialized) return;

        if (LanguageCombo.SelectedItem is LanguageOption selected && selected.Code != _viewModel.Language)
        {
            _viewModel.Language = selected.Code;
        }
    }

    private void AccentColor_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_initialized) return;

        var raw = AccentColorBox.Text?.Trim() ?? string.Empty;
        // Empty input snaps back to the default accent. Otherwise only push
        // a valid #RRGGBB so half-typed values don’t flicker the bar.
        if (raw.Length == 0)
        {
            if (!string.Equals(_viewModel.AccentColor, AccentColorManager.DefaultAccentCode, StringComparison.Ordinal))
            {
                _viewModel.AccentColor = AccentColorManager.DefaultAccentCode;
            }
            return;
        }

        if (TryNormaliseHex(raw, out var canonical) && !string.Equals(canonical, _viewModel.AccentColor, StringComparison.Ordinal))
        {
            _viewModel.AccentColor = canonical;
            SyncAccentWheelFromHex(canonical);
        }
    }

    private void AccentColorReset_Click(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        AccentColorBox.Text = AccentColorManager.DefaultAccentCode;
        if (!string.Equals(_viewModel.AccentColor, AccentColorManager.DefaultAccentCode, StringComparison.Ordinal))
        {
            _viewModel.AccentColor = AccentColorManager.DefaultAccentCode;
        }
    }

    private void AccentColorSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (AccentColorPopup is null) return;
        SyncAccentWheelFromHex(_viewModel.AccentColor);
        AccentColorPopup.IsOpen = !AccentColorPopup.IsOpen;
    }

    private void AccentColorWheel_SelectedColorChanged(object? sender, System.Windows.Media.Color color)
    {
        if (!_initialized) return;
        var hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        if (!string.Equals(AccentColorBox.Text, hex, StringComparison.OrdinalIgnoreCase))
        {
            AccentColorBox.Text = hex;
        }
    }

    private void SyncAccentWheelFromHex(string hex)
    {
        if (AccentColorWheel is null) return;
        var canonical = AccentColorManager.ResolveCanonicalHex(hex);
        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(canonical);
            AccentColorWheel.SelectedColor = color;
        }
        catch (FormatException)
        {
            // ResolveCanonicalHex always emits a valid form, so this is
            // defensive only: ignore and leave the wheel where it is.
        }
    }

    private void BarVisibility_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        bool autoHide = BarVisibilityCombo.SelectedIndex == 1;

        if (autoHide != _viewModel.AutoHideBar)
        {
            _viewModel.AutoHideBar = autoHide;
        }
    }

    private void Startup_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        bool enabled = StartupToggle.IsChecked == true;

        if (enabled != _viewModel.LaunchAtStartup)
        {
            _viewModel.LaunchAtStartup = enabled;
        }
    }

    private void Clock_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        bool show = ClockToggle.IsChecked == true;

        if (show != _viewModel.ShowClock)
        {
            _viewModel.ShowClock = show;
        }
        UpdateClockSizeAvailability();
    }

    private void ClockSize_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        if (ClockSizeCombo.SelectedIndex < 0) return;
        var size = ClockSizeOptions[ClockSizeCombo.SelectedIndex];

        if (size != _viewModel.ClockSize)
        {
            _viewModel.ClockSize = size;
        }
    }

    /// <summary>
    /// Collapses the clock-size selector when the clock itself is hidden,
    /// keeping the settings UI free of dead controls.
    /// </summary>
    private void UpdateClockSizeAvailability()
    {
        if (ClockSizePanel is null) return;
        ClockSizePanel.Visibility = _viewModel.ShowClock ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Notifications_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        bool show = NotificationsToggle.IsChecked == true;
        if (show != _viewModel.ShowNotificationBadges)
        {
            _viewModel.ShowNotificationBadges = show;
        }
        UpdateNotificationDotPanelAvailability();
    }

    private void NotificationDotSize_Changed(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_initialized) return;

        var size = (int)Math.Round(e.NewValue);
        if (NotificationDotSizeValueText is not null)
        {
            NotificationDotSizeValueText.Text = $"{size} px";
        }
        if (Math.Abs(_viewModel.NotificationDotSize - size) > 0.0001)
        {
            _viewModel.NotificationDotSize = size;
        }
    }

    private void NotificationDotColor_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_initialized) return;

        var raw = NotificationDotColorBox.Text?.Trim() ?? string.Empty;
        // Empty input → follow accent. Otherwise only push valid #RRGGBB so
        // half-typed values don’t flicker the bar mid-edit.
        if (raw.Length == 0)
        {
            if (_viewModel.NotificationDotColor.Length != 0)
            {
                _viewModel.NotificationDotColor = string.Empty;
            }
            return;
        }

        if (TryNormaliseHex(raw, out var canonical) && !string.Equals(canonical, _viewModel.NotificationDotColor, StringComparison.Ordinal))
        {
            _viewModel.NotificationDotColor = canonical;
            SyncWheelFromHex(canonical);
        }
    }

    private void NotificationDotColorReset_Click(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        NotificationDotColorBox.Text = string.Empty;
        if (_viewModel.NotificationDotColor.Length != 0)
        {
            _viewModel.NotificationDotColor = string.Empty;
        }
    }

    private void DotColorWheel_SelectedColorChanged(object? sender, System.Windows.Media.Color color)
    {
        if (!_initialized) return;
        var hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        if (!string.Equals(NotificationDotColorBox.Text, hex, StringComparison.OrdinalIgnoreCase))
        {
            // Setting the textbox triggers TextChanged, which pushes the
            // value to the VM and avoids duplicating the persistence path.
            NotificationDotColorBox.Text = hex;
        }
    }

    private void DotColorSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (DotColorPopup is null) return;
        // Re-sync the wheel to the current value just before showing it
        // so the selector lands on the right hue/saturation/brightness.
        SyncWheelFromHex(_viewModel.NotificationDotColor);
        DotColorPopup.IsOpen = !DotColorPopup.IsOpen;
    }

    /// <summary>
    /// Pushes a hex string into the wheel without retriggering its event
    /// loop. Falls back to the current accent color when <paramref name="hex"/>
    /// is empty so the wheel always shows a meaningful selector position.
    /// </summary>
    private void SyncWheelFromHex(string hex)
    {
        if (DotColorWheel is null) return;
        System.Windows.Media.Color color;
        if (!string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            }
            catch
            {
                return;
            }
        }
        else if (TryFindResource("AccentColor") is System.Windows.Media.Color accent)
        {
            color = accent;
        }
        else
        {
            color = System.Windows.Media.Colors.DodgerBlue;
        }
        DotColorWheel.SelectedColor = color;
    }

    /// <summary>
    /// Hides the dot size/color editor when notifications are disabled, so
    /// the user can’t tweak invisible UI.
    /// </summary>
    private void UpdateNotificationDotPanelAvailability()
    {
        if (NotificationDotPanel is null) return;
        NotificationDotPanel.Visibility = _viewModel.ShowNotificationBadges ? Visibility.Visible : Visibility.Collapsed;
    }

    // --- Active tab tint -----------------------------------------------

    private void ActiveTabOpacity_Changed(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_initialized) return;
        var pct = Math.Round(e.NewValue);
        ActiveTabOpacityValueText.Text = $"{(int)pct}%";
        if (Math.Abs(_viewModel.ActiveTabOpacity - pct) > 0.0001)
        {
            _viewModel.ActiveTabOpacity = pct;
        }
    }

    private void ActiveTabColor_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_initialized) return;
        var raw = ActiveTabColorBox.Text?.Trim() ?? string.Empty;
        if (raw.Length == 0)
        {
            if (_viewModel.ActiveTabColor.Length != 0)
            {
                _viewModel.ActiveTabColor = string.Empty;
            }
            return;
        }
        if (TryNormaliseHex(raw, out var canonical) && !string.Equals(canonical, _viewModel.ActiveTabColor, StringComparison.Ordinal))
        {
            _viewModel.ActiveTabColor = canonical;
            SyncActiveTabWheelFromHex(canonical);
        }
    }

    private void ActiveTabColorReset_Click(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        ActiveTabColorBox.Text = string.Empty;
        if (_viewModel.ActiveTabColor.Length != 0)
        {
            _viewModel.ActiveTabColor = string.Empty;
        }
    }

    private void ActiveTabColorSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveTabColorPopup is null) return;
        SyncActiveTabWheelFromHex(_viewModel.ActiveTabColor);
        ActiveTabColorPopup.IsOpen = !ActiveTabColorPopup.IsOpen;
    }

    private void ActiveTabColorWheel_SelectedColorChanged(object? sender, System.Windows.Media.Color color)
    {
        if (!_initialized) return;
        var hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        if (!string.Equals(ActiveTabColorBox.Text, hex, StringComparison.OrdinalIgnoreCase))
        {
            ActiveTabColorBox.Text = hex;
        }
    }

    private void SyncActiveTabWheelFromHex(string hex)
    {
        if (ActiveTabColorWheel is null) return;
        System.Windows.Media.Color color;
        if (!string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            }
            catch
            {
                return;
            }
        }
        else if (TryFindResource("AccentColor") is System.Windows.Media.Color accent)
        {
            color = accent;
        }
        else
        {
            color = System.Windows.Media.Colors.DodgerBlue;
        }
        ActiveTabColorWheel.SelectedColor = color;
    }

    /// <summary>
    /// Accepts <c>#RRGGBB</c>, <c>RRGGBB</c>, <c>#RGB</c> or <c>RGB</c> and
    /// returns the canonical upper-case <c>#RRGGBB</c> form. Returns false
    /// for any malformed input so the caller can ignore in-flight typing.
    /// </summary>
    private static bool TryNormaliseHex(string input, out string canonical)
    {
        canonical = string.Empty;
        var v = input.StartsWith('#') ? input[1..] : input;
        if (v.Length == 3)
        {
            v = $"{v[0]}{v[0]}{v[1]}{v[1]}{v[2]}{v[2]}";
        }
        if (v.Length != 6) return false;
        for (int i = 0; i < 6; i++)
        {
            if (!Uri.IsHexDigit(v[i])) return false;
        }
        canonical = "#" + v.ToUpperInvariant();
        return true;
    }

    private void BarSize_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        if (BarSizeCombo.SelectedIndex < 0) return;
        var size = BarSizeOptions[BarSizeCombo.SelectedIndex];

        if (size != _viewModel.BarSize)
        {
            _viewModel.BarSize = size;
        }
    }

    private void Blur_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        bool enabled = BlurToggle.IsChecked == true;
        if (enabled != _viewModel.UseBlurEffect)
        {
            _viewModel.UseBlurEffect = enabled;
        }

        // App.xaml.cs forces the theme to Dark when blur turns on; mirror
        // that change in the settings UI and grey out the theme combo so
        // the user cannot pick an incompatible combination.
        if (enabled)
        {
            ThemeCombo.SelectedIndex = 1;
        }
        UpdateThemeLockState();
    }

    /// <summary>
    /// Soft-locks the theme & opacity controls when blur is on. The
    /// underlying inputs stay enabled (so the bound values still display
    /// correctly), but a transparent overlay intercepts clicks and the
    /// section is faded to communicate the disabled state. Attempting
    /// to interact triggers a toast explaining why.
    /// </summary>
    private void UpdateThemeLockState()
    {
        bool locked = _viewModel.UseBlurEffect;
        double targetOpacity = locked ? 0.4 : 1.0;
        Visibility overlayVisibility = locked ? Visibility.Visible : Visibility.Collapsed;

        ThemeContent.Opacity = targetOpacity;
        ThemeLockOverlay.Visibility = overlayVisibility;

        OpacityContent.Opacity = targetOpacity;
        OpacityLockOverlay.Visibility = overlayVisibility;
    }

    private void LockedSection_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // The overlay only exists while blur is active, so we can show
        // the toast unconditionally. Mark handled to prevent the click
        // from reaching the underlying control.
        e.Handled = true;
        ShowToast(TryFindResource("Settings_BlurLockedHint") as string
            ?? "This option is disabled while background blur is active.");
    }

    private System.Windows.Threading.DispatcherTimer? _toastDismissTimer;

    /// <summary>
    /// Slides a small banner up from the bottom of the settings window
    /// and fades it out after a short delay. Reusing the same banner
    /// keeps the UI snappy if the user repeatedly clicks a locked area.
    /// </summary>
    private void ShowToast(string message)
    {
        ToastText.Text = message;

        // Reset any in-flight animation/timer so consecutive clicks restart
        // the toast cleanly rather than fighting an ongoing fade-out.
        ToastBanner.BeginAnimation(OpacityProperty, null);
        if (ToastBanner.RenderTransform is TranslateTransform tt)
        {
            tt.BeginAnimation(TranslateTransform.YProperty, null);
            tt.Y = 20;
        }
        _toastDismissTimer?.Stop();

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = System.TimeSpan.FromMilliseconds(200);

        ToastBanner.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, duration) { EasingFunction = ease });

        if (ToastBanner.RenderTransform is TranslateTransform translate)
        {
            translate.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(20, 0, duration) { EasingFunction = ease });
        }

        _toastDismissTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = System.TimeSpan.FromMilliseconds(2200)
        };
        _toastDismissTimer.Tick += (_, _) =>
        {
            _toastDismissTimer?.Stop();
            var fadeOut = new DoubleAnimation(1, 0, System.TimeSpan.FromMilliseconds(260))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            ToastBanner.BeginAnimation(OpacityProperty, fadeOut);
        };
        _toastDismissTimer.Start();
    }

    private void AutoUpdates_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        bool enabled = AutoUpdatesToggle.IsChecked == true;
        if (enabled != _viewModel.AutoCheckUpdates)
        {
            _viewModel.AutoCheckUpdates = enabled;
        }
    }

    private void CheckUpdatesNow_Click(object sender, RoutedEventArgs e)
    {
        // Forwards to the VM command which raises ManualUpdateCheckRequested
        // — App.xaml.cs handles the actual orchestration so this view
        // remains free of update domain knowledge.
        if (_viewModel.CheckForUpdatesCommand.CanExecute(null))
        {
            _viewModel.CheckForUpdatesCommand.Execute(null);
        }
    }

    /// <summary>
    /// Restores every Tabu setting to its factory default. Mirrors
    /// the values declared in <see cref="Tabu.Domain.Entities.UserSettings"/>.
    /// Each toggle/slider/combo assignment fires its existing
    /// <c>*_Changed</c> handler, so the MainViewModel is updated and
    /// persisted exactly as if the user had toggled each control by hand.
    /// </summary>
    private void ResetSettings_Click(object sender, RoutedEventArgs e)
    {
        var title = TryFindResource("Settings_ResetConfirmTitle") as string ?? "Reset settings";
        var body = TryFindResource("Settings_ResetConfirmBody") as string
                   ?? "All Tabu settings will be restored to their default values. Continue?";
        var yesText = TryFindResource("Dialog_Yes") as string ?? "Yes";
        var noText = TryFindResource("Dialog_No") as string ?? "Cancel";

        var result = TabuDialog.Show(
            this,
            body,
            title,
            TabuDialogVariant.Warning,
            primaryText: yesText,
            secondaryText: noText,
            isPrimaryDestructive: true);

        if (result != TabuDialogResult.Yes)
        {
            return;
        }

        // Bar layout
        BarPlacementCombo.SelectedIndex = 1;
        DetectionModeCombo.SelectedIndex = 1;
        BarSizeCombo.SelectedIndex = 0;
        BarVisibilityCombo.SelectedIndex = 0;

        // Appearance
        BlurToggle.IsChecked = false;
        ThemeCombo.SelectedIndex = 0;
        OpacitySlider.Value = 100;
        AccentColorBox.Text = AccentColorManager.DefaultAccentCode;
        SyncAccentWheelFromHex(AccentColorManager.DefaultAccentCode);
        BrandingToggle.IsChecked = true;

        // Tabs
        TabWidthCombo.SelectedIndex = 1;
        ClockToggle.IsChecked = true;
        ClockSizeCombo.SelectedIndex = 0;
        UpdateClockSizeAvailability();
        NotificationsToggle.IsChecked = true;
        NotificationDotSizeSlider.Value = 7;
        NotificationDotColorBox.Text = string.Empty;
        SyncWheelFromHex(string.Empty);
        UpdateNotificationDotPanelAvailability();

        ActiveTabColorBox.Text = string.Empty;
        ActiveTabOpacitySlider.Value = 100;
        SyncActiveTabWheelFromHex(string.Empty);

        // System
        var defaultLanguage = LocalizationManager.AvailableLanguages
            .FirstOrDefault(l => l.Code == "en")
            ?? LocalizationManager.AvailableLanguages[0];
        LanguageCombo.SelectedItem = defaultLanguage;
        StartupToggle.IsChecked = false;
        AutoUpdatesToggle.IsChecked = true;

        ShowToast(TryFindResource("Settings_ResetSuccess") as string ?? "Settings restored to defaults.");
    }

    // --- Fluent theme (WPF UI, scoped to this window) -------------------

    private Wpf.Ui.Appearance.ApplicationTheme _fluentTheme = Wpf.Ui.Appearance.ApplicationTheme.Dark;

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.AppTheme)
            or nameof(MainViewModel.UseBlurEffect)
            or nameof(MainViewModel.AccentColor))
        {
            ApplyFluentTheme();
        }
    }

    /// <summary>
    /// Mirrors Tabu's effective theme and accent onto this window's WPF UI
    /// resources. Blur forces dark, matching App.xaml.cs.
    /// </summary>
    private void ApplyFluentTheme()
    {
        bool dark = _viewModel.UseBlurEffect || _viewModel.AppTheme switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => ThemeManager.IsSystemDarkMode(),
        };
        var theme = dark ? Wpf.Ui.Appearance.ApplicationTheme.Dark : Wpf.Ui.Appearance.ApplicationTheme.Light;

        if (theme != _fluentTheme)
        {
            _fluentTheme = theme;
            Resources.MergedDictionaries[0] = new Wpf.Ui.Markup.ThemesDictionary { Theme = theme };
            ApplyWindowDarkMode();
        }

        ApplyFluentAccent(dark);
    }

    /// <summary>
    /// Overrides WPF UI's accent keys with Tabu's accent color. Local keys
    /// win over the merged dictionaries, and only this window sees them.
    /// Variants follow WPF UI's convention: lighter shades on dark, darker
    /// shades on light.
    /// </summary>
    private void ApplyFluentAccent(bool dark)
    {
        var hex = AccentColorManager.ResolveCanonicalHex(_viewModel.AccentColor);
        if (ColorConverter.ConvertFromString(hex) is not Color accent)
        {
            return;
        }

        Color target = dark ? Colors.White : Colors.Black;
        Color primary = Blend(accent, target, dark ? 0.25 : 0.10);
        Color secondary = Blend(accent, target, dark ? 0.40 : 0.25);
        Color tertiary = Blend(accent, target, dark ? 0.55 : 0.40);
        Color fill = dark ? primary : secondary;

        Resources["SystemAccentColor"] = accent;
        Resources["SystemAccentColorPrimary"] = primary;
        Resources["SystemAccentColorSecondary"] = secondary;
        Resources["SystemAccentColorTertiary"] = tertiary;
        Resources["AccentFillColorDefault"] = fill;
        Resources["AccentFillColorSecondary"] = Color.FromArgb(0xE6, fill.R, fill.G, fill.B);
        Resources["AccentFillColorTertiary"] = Color.FromArgb(0xCC, fill.R, fill.G, fill.B);

        Resources["SystemAccentColorBrush"] = new SolidColorBrush(accent);
        Resources["SystemAccentColorPrimaryBrush"] = new SolidColorBrush(primary);
        Resources["SystemAccentColorSecondaryBrush"] = new SolidColorBrush(secondary);
        Resources["SystemAccentColorTertiaryBrush"] = new SolidColorBrush(tertiary);
        Resources["AccentFillColorDefaultBrush"] = new SolidColorBrush(fill);
        Resources["AccentFillColorSecondaryBrush"] = new SolidColorBrush(Color.FromArgb(0xE6, fill.R, fill.G, fill.B));
        Resources["AccentFillColorTertiaryBrush"] = new SolidColorBrush(Color.FromArgb(0xCC, fill.R, fill.G, fill.B));
        Resources["AccentTextFillColorPrimaryBrush"] = new SolidColorBrush(dark ? tertiary : secondary);
        Resources["AccentFillColorSelectedTextBackgroundBrush"] = new SolidColorBrush(accent);

        // Knob / text on the accent fill: dark on light accents, white otherwise.
        double luminance = (0.299 * fill.R + 0.587 * fill.G + 0.114 * fill.B) / 255;
        var onAccent = new SolidColorBrush(luminance > 0.6 ? Colors.Black : Colors.White);
        Resources["TextOnAccentFillColorPrimaryBrush"] = onAccent;
        Resources["ToggleSwitchKnobFillOn"] = onAccent;
        Resources["ToggleSwitchKnobFillOnPointerOver"] = onAccent;
        Resources["ToggleSwitchKnobFillOnPressed"] = onAccent;
    }

    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmsbtMainWindow = 2; // Mica

    [System.Runtime.InteropServices.LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>
    /// Tints the Mica backdrop and caption for this window only. WPF UI's
    /// own helpers read a process-wide theme that is only set when its
    /// dictionaries live in App.xaml, which Tabu deliberately avoids.
    /// </summary>
    private void ApplyWindowDarkMode()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        int dark = _fluentTheme == Wpf.Ui.Appearance.ApplicationTheme.Dark ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));

        // DWM only re-tints Mica when the backdrop type is (re)assigned.
        int backdrop = DwmsbtMainWindow;
        _ = DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref backdrop, sizeof(int));
    }

    private static Color Blend(Color from, Color to, double amount) => Color.FromRgb(
        (byte)(from.R + (to.R - from.R) * amount),
        (byte)(from.G + (to.G - from.G) * amount),
        (byte)(from.B + (to.B - from.B) * amount));

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        // Fires during InitializeComponent for the default item, before
        // the pages further down the XAML have been created.
        if (sender is not RadioButton { Tag: string pageName } item || PageScroller is null)
        {
            return;
        }

        foreach (var page in new FrameworkElement[] { BarLayoutPage, AppearancePage, TabsPage, SystemPage })
        {
            page.Visibility = page.Name == pageName ? Visibility.Visible : Visibility.Collapsed;
        }

        // Bind the heading to the nav item's label so it follows language changes.
        if (item.Content is Panel { Children.Count: > 1 } content && content.Children[1] is TextBlock label)
        {
            PageTitle.SetBinding(TextBlock.TextProperty,
                new System.Windows.Data.Binding(nameof(TextBlock.Text)) { Source = label });
        }

        PageScroller.ScrollToTop();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // The Mica tint needs the HWND and must land after WPF UI applies
        // its backdrop during load, so defer it past the Loaded handlers.
        Dispatcher.BeginInvoke(ApplyWindowDarkMode, System.Windows.Threading.DispatcherPriority.Loaded);

        const int durationMs = 220;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = System.TimeSpan.FromMilliseconds(durationMs);

        var fade = new DoubleAnimation(0, 1, duration) { EasingFunction = ease };
        var grow = new DoubleAnimation(0.97, 1, duration) { EasingFunction = ease };

        SettingsRoot.BeginAnimation(OpacityProperty, fade);

        if (SettingsRoot.RenderTransform is ScaleTransform scale)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        }
    }
}
