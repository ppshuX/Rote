using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Rote.Models;
using Rote.Services;
using System;
using System.Linq;

namespace Rote;

public partial class MainWindow : Window
{
    // ── Core state ────────────────────────────────────────────────
    private AppState _state = null!;
    private bool _initialized;

    // ── Drag tracking (manual) ────────────────────────────────────
    private bool _isDragging;
    private PixelPoint _posBeforeDrag; // to detect click vs drag

    // ── Resize tracking ──────────────────────────────────────────
    private bool _isResizing;
    private Point _resizeStartPoint;
    private double _resizeStartWidth;
    private double _resizeStartHeight;

    // ── Auto-save ─────────────────────────────────────────────────
    private DispatcherTimer? _saveTimer;

    // ═══════════════════════════════════════════════════════════════
    public MainWindow()
    {
        InitializeComponent();

        _state = StateStorage.Load();

        // Restore content BEFORE sizing
        NoteEditor.Text = _state.Content;

        ApplySize();
        Topmost = _state.IsTopmost;

        // ── Subtle shadows ──

        // ── Handle: drag + click ──
        HandleBorder.PointerPressed  += OnHandlePressed;
        HandleBorder.PointerMoved    += OnHandleMoved;
        HandleBorder.PointerReleased += OnHandleReleased;

        // ── Resize handle ──
        ResizeHandle.PointerPressed  += OnResizePressed;
        ResizeHandle.PointerMoved    += OnResizeMoved;
        ResizeHandle.PointerReleased += OnResizeReleased;

        // ── Context menu ──
        // Build a SEPARATE instance for each target (review F3): sharing one
        // ContextMenu between two controls throws "already has a visual parent"
        // or causes it to not show on one of them.
        HandleBorder.ContextMenu = BuildContextMenu();
        this.ContextMenu         = BuildContextMenu();

        // ── Text change → debounced auto-save ──
        NoteEditor.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
                ScheduleAutoSave();
        };

        // ── Save on window events ──
        this.Deactivated += (_, _) => SaveNow();
        this.Closing     += OnWindowClosing;

        // ── Save position after window moves ──
        this.PositionChanged += (_, _) =>
        {
            if (_initialized)
            {
                _state.WindowX = Position.X;
                _state.WindowY = Position.Y;
                ScheduleAutoSave();
            }
        };

        // ── Restore position once screens are available ──
        this.Opened += (_, _) =>
        {
            RestoreWindowPosition();
            UpdateVisualState();
        };

        // ── Keyboard shortcut (on TextBox to avoid IME conflict) ──
        NoteEditor.KeyDown += OnKeyDown;

        _initialized = true;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Size management
    // ═══════════════════════════════════════════════════════════════

    private void ApplySize()
    {
        Width  = _state.IsExpanded ? _state.ExpandedWidth  : AppConstants.CollapsedSize;
        Height = _state.IsExpanded ? _state.ExpandedHeight : AppConstants.CollapsedSize;
        Opacity = _state.Opacity;
    }

    private void ToggleExpand()
    {
        _state.IsExpanded = !_state.IsExpanded;
        ApplySize();
        UpdateVisualState();
        SaveNow();
    }

    private void UpdateVisualState()
    {
        NoteBorder.IsVisible = _state.IsExpanded;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Drag & click (manual implementation)
    // ═══════════════════════════════════════════════════════════════

    private void OnHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        _posBeforeDrag = Position;
        _isDragging = false;
        e.Handled = true;
        e.Pointer.Capture(HandleBorder);
    }

    private void OnHandleMoved(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Captured != HandleBorder) return;

        _isDragging = true;
        var point = e.GetPosition(this);
        var screenPoint = this.PointToScreen(point);
        Position = new PixelPoint(screenPoint.X - (int)(AppConstants.CollapsedSize / 2),
                                  screenPoint.Y - (int)(AppConstants.CollapsedSize / 2));
    }

    private void OnHandleReleased(object? sender, PointerReleasedEventArgs e)
    {
        e.Pointer.Capture(null);

        if (!_isDragging)
        {
            ToggleExpand();
        }
        _isDragging = false;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Keyboard shortcuts
    // ═══════════════════════════════════════════════════════════════

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && e.KeyModifiers == KeyModifiers.Control)
        {
            ToggleExpand();
            e.Handled = true;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Resize (bottom-right handle)
    // ═══════════════════════════════════════════════════════════════

    private void OnResizePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_state.IsExpanded) return;
        _isResizing = true;
        _resizeStartPoint = e.GetPosition(this);
        _resizeStartWidth = Width;
        _resizeStartHeight = Height;
        e.Pointer.Capture(ResizeHandle);
        e.Handled = true;
    }

    private void OnResizeMoved(object? sender, PointerEventArgs e)
    {
        if (!_isResizing) return;
        var current = e.GetPosition(this);
        var dx = current.X - _resizeStartPoint.X;
        var dy = current.Y - _resizeStartPoint.Y;
        Width = Math.Max(AppConstants.MinExpandedWidth, _resizeStartWidth + dx);
        Height = Math.Max(AppConstants.MinExpandedHeight, _resizeStartHeight + dy);
    }

    private void OnResizeReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isResizing) return;
        _isResizing = false;
        e.Pointer.Capture(null);
        _state.ExpandedWidth = Width;
        _state.ExpandedHeight = Height;
        SaveNow();
    }

    // ═══════════════════════════════════════════════════════════════
    //  Auto-save (debounced)
    // ═══════════════════════════════════════════════════════════════

    private void ScheduleAutoSave()
    {
        if (_saveTimer == null)
        {
            _saveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(AppConstants.AutoSaveDelayMs),
            };
            _saveTimer.Tick += (_, _) =>
            {
                _saveTimer.Stop();
                DoSave();
            };
        }

        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveNow()
    {
        _saveTimer?.Stop();
        DoSave();
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        SaveNow();
        _saveTimer?.Stop();
        _saveTimer = null;
    }

    private void DoSave()
    {
        _state.Content = NoteEditor.Text ?? string.Empty;
        StateStorage.Save(_state);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Window position
    // ═══════════════════════════════════════════════════════════════

    private void RestoreWindowPosition()
    {
        if (IsPositionValid(_state.WindowX, _state.WindowY))
        {
            // Round rather than truncate so sub-pixel positions don't drift (review F15).
            Position = new PixelPoint((int)Math.Round(_state.WindowX), (int)Math.Round(_state.WindowY));
        }
        else
        {
            Position       = GetDefaultPosition();
            _state.WindowX = Position.X;
            _state.WindowY = Position.Y;
        }
    }

    private void ResetWindowPosition()
    {
        Position       = GetDefaultPosition();
        _state.WindowX = Position.X;
        _state.WindowY = Position.Y;
        SaveNow();
    }

    private PixelPoint GetDefaultPosition()
    {
        try
        {
            var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
            if (screen != null)
            {
                var area = screen.WorkingArea;
                int x = area.X + area.Width  - (int)AppConstants.ExpandedWidth  - AppConstants.PositionMargin;
                int y = area.Y + area.Height - (int)AppConstants.ExpandedHeight - AppConstants.PositionMargin;
                return new PixelPoint(x, y);
            }
        }
        catch (Exception ex)
        {
            RoteLogger.Log($"Failed to get screen bounds: {ex.Message}");
        }

        return new PixelPoint(100, 100);
    }

    private bool IsPositionValid(double x, double y)
    {
        if (x < 0 || y < 0) return false;

        try
        {
            foreach (var screen in Screens.All)
            {
                var wa = screen.WorkingArea;
                if (x >= wa.X && y >= wa.Y
                    && x < wa.X + wa.Width  - AppConstants.PositionMargin
                    && y < wa.Y + wa.Height - AppConstants.PositionMargin)
                {
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            RoteLogger.Log($"Failed to validate position: {ex.Message}");
        }

        return false;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Context menu
    // ═══════════════════════════════════════════════════════════════

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();

        // ── Toggle topmost ──
        var topmostItem = new MenuItem();
        UpdateTopmostLabel(topmostItem);
        topmostItem.Click += (_, _) =>
        {
            _state.IsTopmost = !_state.IsTopmost;
            Topmost = _state.IsTopmost;
            UpdateTopmostLabel(topmostItem);
            SaveNow();
        };
        menu.Items.Add(topmostItem);

        // ── Opacity submenu ──
        var opacityMenu = new MenuItem { Header = "透明度" };
        var opacityLevels = new[] { 1.0, 0.9, 0.8, 0.7, 0.6, 0.5 };
        foreach (var level in opacityLevels)
        {
            var item = new MenuItem { Header = $"{level * 100}%", Tag = level };
            item.Click += (_, _) =>
            {
                _state.Opacity = level;
                Opacity = level;
                SaveNow();
            };
            opacityMenu.Items.Add(item);
        }
        menu.Items.Add(opacityMenu);
        menu.Items.Add(new Separator());

        // ── Reset position ──
        var resetPos = new MenuItem { Header = "重置窗口位置" };
        resetPos.Click += (_, _) => ResetWindowPosition();
        menu.Items.Add(resetPos);

        // ── Reset size ──
        var resetSize = new MenuItem { Header = "重置窗口大小" };
        resetSize.Click += (_, _) =>
        {
            _state.ExpandedWidth = 320;
            _state.ExpandedHeight = 360;
            ApplySize();
            SaveNow();
        };
        menu.Items.Add(resetSize);

        // ── Clear content ──
        var clear = new MenuItem { Header = "清空内容" };
        clear.Click += async (_, _) =>
        {
            var dialog = new Window
            {
                Title                 = "Rote",
                Width                 = 280,
                Height                = 140,
                SystemDecorations     = SystemDecorations.BorderOnly,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar         = false,
                CanResize             = false,
                Topmost               = true,
            };

            var panel = new StackPanel { Margin = new Thickness(20, 16) };
            panel.Children.Add(new TextBlock
            {
                Text     = "确定要清空所有内容吗？",
                FontSize = 14,
                Margin   = new Thickness(0, 0, 0, 16),
            });

            var buttons = new StackPanel
            {
                Orientation         = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Spacing             = 8,
            };

            var cancel = new Button { Content = "取消" };
            cancel.Click += (_, _) => dialog.Close();

            var confirm = new Button { Content = "清空" };
            confirm.Click += (_, _) =>
            {
                NoteEditor.Text = string.Empty;
                _state.Content  = string.Empty;
                SaveNow();
                dialog.Close();
            };

            buttons.Children.Add(cancel);
            buttons.Children.Add(confirm);
            panel.Children.Add(buttons);
            dialog.Content = panel;

            await dialog.ShowDialog(this);
        };
        menu.Items.Add(clear);
        menu.Items.Add(new Separator());

        // ── Exit ──
        var exit = new MenuItem { Header = "退出" };
        exit.Click += (_, _) => { SaveNow(); Close(); };
        menu.Items.Add(exit);

        return menu;
    }

    private void UpdateTopmostLabel(MenuItem item)
    {
        // Express the checked state via IsChecked instead of faking it with a
        // checkbox glyph and whitespace padding (review F16).
        item.IsChecked = _state.IsTopmost;
        item.Header    = "始终置顶";
    }
}
