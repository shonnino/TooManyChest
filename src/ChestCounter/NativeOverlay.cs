using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ChestCounter;

/// <summary>
/// Small always-on-top counter window drawn with plain Win32/GDI (no WinForms needed).
/// Left-drag to move it, right-click to hide it (Ctrl+Shift+O or the counter's menu shows it again).
/// Visible over Fortnite when the game runs in "windowed fullscreen" (not exclusive fullscreen).
/// </summary>
/// <summary>What the overlay shows. <see cref="Current"/> null = no match in progress.</summary>
/// <param name="Detail">Optional secondary text drawn small and grey after the value.</param>
public sealed record OverlayRow(string Label, string Value, bool Treasure = false, string? Detail = null);

/// <param name="Current">Right counter (match in progress, or last match); null = "—".</param>
/// <param name="CurrentLive">Right counter is the match in progress (green).</param>
/// <param name="Badge">Header badge ("● EN DIRECT" / "● LIVE", "● PARTIE EN COURS" / "● MATCH IN PROGRESS") or null.</param>
public sealed record OverlayView(string Total, string? Current, string CurrentCaption, bool CurrentLive, string? Badge, IReadOnlyList<OverlayRow> Rows, string Treasures = "0", string Session = "0", string? Last = null, string? Squad = null, string TokenChance = "0%")
{
    internal bool SameContent(OverlayView other) => Equals(other with { Rows = Rows }) && Rows.SequenceEqual(other.Rows);
}

[SupportedOSPlatform("windows")]
public static class NativeOverlay
{
    private static Thread? _thread;
    private static IntPtr _hwnd;
    private static volatile OverlayView _view = new("0", null, OverlayModel.LastMatchCaption, false, null, Array.Empty<OverlayRow>());

    // Layout (computed by Relayout, used by Paint; both on the overlay thread).
    private static int _headerH, _bigH, _captionH, _rowH, _col1W, _col2W, _labelW;
    private static IntPtr _fontHeader, _fontCaption, _fontLabel;
    private static IntPtr _brushBorder, _brushLine;
    private const string Header = Brand.Name;
    private const string CaptionTotal = "total";
    private static Action<int, int>? _onMoved;
    private static Action<bool>? _onLockChanged;
    private static volatile bool _locked;
    private static bool _offscreen;
    private static int _stableW;
    private static RECT _lockRect; // lock button, client coordinates
    private static IntPtr _fontIcon;
    private static WndProcDelegate? _wndProc; // keep the delegate alive
    private static IntPtr _fontBig, _fontSmall, _brushBg;
    private static float _scale = 1f;    // display scaling × size chosen by the player
    private static float _dpiScale = 1f; // display scaling only
    private static volatile int _sizePercent = 100;
    private static volatile int _opacityPercent = 100;

    /// <summary>"hud" (figures only, under the minimap, like the game's HUD) or "classic" (dark card).</summary>
    private static volatile string _style = "classic";
    private static IntPtr _fontHudBig, _fontHudSmall;
    private static readonly Dictionary<IntPtr, IntPtr> NumericFonts = new();
    private static int _hudBigH, _hudSmallH;
    private static Bitmap? _hudChest;
    private static bool _perPixelLayer;

    public static string[] Styles => new[] { "hud", "classic" };

    /// <summary>Changes the look of the overlay at once.</summary>
    public static void SetStyle(string style)
    {
        _style = Array.IndexOf(Styles, style) >= 0 ? style : "hud";
        if (_hwnd != IntPtr.Zero)
        {
            PostMessageW(_hwnd, WM_APP_APPEARANCE, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private static bool IsHud => _style == "hud";

    private const int MinWidth = 240, Pad = 16, ColGap = 36;

    /// <summary>Size and opacity of the overlay (percent). Applied at once when the overlay is shown.</summary>
    public static void SetAppearance(int sizePercent, int opacityPercent)
    {
        _sizePercent = OverlayAppearance.ClampSize(sizePercent);
        _opacityPercent = OverlayAppearance.ClampOpacity(opacityPercent);
        if (_hwnd != IntPtr.Zero)
        {
            PostMessageW(_hwnd, WM_APP_APPEARANCE, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private static byte Alpha => (byte)Math.Round(_opacityPercent * 255 / 100.0);

    public static bool IsRunning => _hwnd != IntPtr.Zero;

    /// <summary>True when the overlay window is shown (not hidden with the shortcut or the menu).</summary>
    public static bool IsVisible => _hwnd != IntPtr.Zero && IsWindowVisible(_hwnd);

    /// <summary>Closes the overlay window and its thread (overlay turned off in the settings). Start() can open it again.</summary>
    public static void Stop()
    {
        var thread = _thread;
        if (thread == null)
        {
            return;
        }
        if (_hwnd != IntPtr.Zero)
        {
            PostMessageW(_hwnd, WM_APP_STOP, IntPtr.Zero, IntPtr.Zero);
        }
        thread.Join(TimeSpan.FromSeconds(3));
    }

    /// <summary>True when the global Ctrl+Shift+O shortcut could be registered.</summary>
    public static bool HotkeyRegistered { get; private set; }

    /// <summary>True when the global Ctrl+Shift+L shortcut (lock/unlock) could be registered.</summary>
    public static bool LockHotkeyRegistered { get; private set; }

    /// <summary>Locked = click-through: the mouse goes through the overlay and it cannot be moved.</summary>
    public static bool IsLocked => _locked;

    public static void Start(int? x, int? y, bool locked, Action<int, int> onMoved, Action<bool> onLockChanged)
    {
        if (_thread != null)
        {
            return;
        }

        _onMoved = onMoved;
        _offscreen = x <= -10000; // --overlay-preview: never brought back on screen
        _onLockChanged = onLockChanged;
        _locked = locked;
        var ready = new ManualResetEventSlim();
        _thread = new Thread(() =>
        {
            try
            {
                Run(x, y, ready);
            }
            catch (Exception ex)
            {
                Console.WriteLine(L.T($"Overlay indisponible : {ex.Message}", $"Overlay unavailable: {ex.Message}", $"Overlay no disponible: {ex.Message}"));
                ready.Set();
            }
            finally
            {
                _hwnd = IntPtr.Zero;
                HotkeyRegistered = LockHotkeyRegistered = false;
                _thread = null;
            }
        })
        {
            IsBackground = true,
            Name = "Overlay",
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        ready.Wait(TimeSpan.FromSeconds(5));
    }

    /// <summary>Replaces what is shown. The window resizes itself to fit.</summary>
    public static void Update(OverlayView view)
    {
        if (_view.SameContent(view)) return;
        _view = view;
        if (_hwnd != IntPtr.Zero)
        {
            PostMessageW(_hwnd, WM_APP_RELAYOUT, IntPtr.Zero, IntPtr.Zero);
        }
    }

    /// <summary>Saves the overlay as it is drawn to a PNG file (works off-screen). Used by --overlay-preview.</summary>
    public static bool Snapshot(string path)
    {
        if (_hwnd == IntPtr.Zero || !GetWindowRect(_hwnd, out var r))
        {
            return false;
        }
        int w = r.Right - r.Left, h = r.Bottom - r.Top;
        var screen = GetDC(IntPtr.Zero);
        var mem = CreateCompatibleDC(screen);
        var bmp = CreateCompatibleBitmap(screen, w, h);
        var old = SelectObject(mem, bmp);
        // Per-pixel layered windows do not supply a client bitmap to PrintWindow.
        var ok = IsHud
            ? SendMessageW(_hwnd, WM_PRINTCLIENT, mem, IntPtr.Zero) != IntPtr.Zero
            : PrintWindow(_hwnd, mem, 1 /* PW_CLIENTONLY: WM_PRINT -> WM_PRINTCLIENT */);
        SelectObject(mem, old);
        var bi = new BITMAPINFOHEADER { biSize = 40, biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32 };
        var bgra = new byte[w * h * 4];
        GetDIBits(mem, bmp, 0, (uint)h, bgra, ref bi, 0);
        DeleteObject(bmp);
        DeleteDC(mem);
        ReleaseDC(IntPtr.Zero, screen);
        Png.Write(path, w, h, bgra);
        return ok;
    }

    public static void ToggleLock()
    {
        if (_hwnd != IntPtr.Zero)
        {
            PostMessageW(_hwnd, WM_APP_LOCK, IntPtr.Zero, IntPtr.Zero);
        }
    }

    public static void Toggle()
    {
        if (_hwnd != IntPtr.Zero)
        {
            PostMessageW(_hwnd, WM_APP_TOGGLE, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private static void Run(int? x, int? y, ManualResetEventSlim ready)
    {
        try { SetProcessDPIAware(); } catch { /* old Windows */ }

        var screenDc = GetDC(IntPtr.Zero);
        _dpiScale = GetDeviceCaps(screenDc, LOGPIXELSY) / 96f;
        ReleaseDC(IntPtr.Zero, screenDc);

        int S(int v) => (int)Math.Round(v * _scale);

        CreateFonts();
        _brushBg = CreateSolidBrush(Rgb(10, 32, 76));
        _brushBorder = CreateSolidBrush(Rgb(77, 120, 184));
        _brushLine = CreateSolidBrush(Rgb(53, 86, 146));

        var hInstance = GetModuleHandleW(null);
        _wndProc = WndProc;
        var wc = new WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = hInstance,
            hCursor = LoadCursorW(IntPtr.Zero, IDC_SIZEALL),
            hbrBackground = _brushBg,
            lpszClassName = "ChestCounterOverlay",
        };
        // Already registered when the overlay is turned off and on again in the same run (ERROR_CLASS_ALREADY_EXISTS).
        if (RegisterClassExW(ref wc) == 0 && Marshal.GetLastWin32Error() != 1410)
        {
            throw new InvalidOperationException("RegisterClassEx failed");
        }

        int w = S(MinWidth), h = S(90);
        var posX = x ?? S(24);
        var posY = y ?? S(24);

        _hwnd = CreateWindowExW(
            // Opaque (no WS_EX_LAYERED alpha): nothing for the compositor to blend every frame.
            WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
            wc.lpszClassName, Brand.Name, WS_POPUP,

            posX, posY, w, h, IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
        if (_hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("CreateWindowEx failed");
        }

        ApplyLock(_hwnd, _locked, notify: false);
        _stableW = 0;
        Relayout(_hwnd, firstLayout: true);
        ShowWindow(_hwnd, SW_SHOWNOACTIVATE);
        // No timer at all: the thread sleeps in GetMessage and only wakes up when the counter changes,
        // when you drag/hide the window or press the shortcut.
        // Global shortcut, works even when the game has the focus.
        HotkeyRegistered = RegisterHotKey(_hwnd, 1, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, VK_O);
        LockHotkeyRegistered = RegisterHotKey(_hwnd, 2, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, VK_L);
        ready.Set();

        while (GetMessageW(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessageW(ref msg);
        }
    }

    /// <summary>(Re)creates the fonts for the current display scaling and chosen size.</summary>
    private static void CreateFonts()
    {
        foreach (var old in new[] { _fontBig, _fontSmall, _fontHeader, _fontCaption, _fontLabel, _fontIcon, _fontHudBig, _fontHudSmall })
        {
            if (old != IntPtr.Zero) DeleteObject(old);
        }
        _scale = _dpiScale * _sizePercent / 100f;
        _hudChest?.Dispose();
        _hudChest = null;
        int S(int v) => (int)Math.Round(v * _scale);
        foreach (var old in NumericFonts.Values) DeleteObject(old);
        NumericFonts.Clear();
        var family = Brand.OverlayFont.Value;
        var digits = Brand.OverlayDigits.Value;
        IntPtr MakeFont(int height, int weight, uint quality = 5)
        {
            var handle = CreateFontW(-S(height), 0, 0, 0, weight, 0, 0, 0, 1, 0, 0, quality, 0, family);
            if (handle != IntPtr.Zero)
                NumericFonts[handle] = CreateFontW(-S(height), 0, 0, 0, 800, 0, 0, 0, 1, 0, 0, quality, 0, digits);
            return handle;
        }
        _fontBig = MakeFont(38, 800);
        _fontSmall = MakeFont(14, 700);
        _fontHeader = MakeFont(14, 700);
        _fontCaption = MakeFont(11, 700);
        _fontLabel = MakeFont(13, 700);
        _fontIcon = CreateFontW(-S(14), 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 4, 0, "Segoe MDL2 Assets");
        // Grayscale anti-aliasing preserves the edges on the transparent HUD.
        _fontHudBig = MakeFont(40, 700, 4);
        _fontHudSmall = MakeFont(18, 700, 4);
    }

    private static IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_NCHITTEST:
            {
                if (_locked) return (IntPtr)HTTRANSPARENT;
                // The padlock is a client button. The rest of the panel is a native drag surface.
                GetWindowRect(hwnd, out var wr);
                var px = (short)((long)lParam & 0xFFFF) - wr.Left;
                var py = (short)(((long)lParam >> 16) & 0xFFFF) - wr.Top;
                var onButton = px >= _lockRect.Left && px < _lockRect.Right && py >= _lockRect.Top && py < _lockRect.Bottom;
                return (IntPtr)(onButton ? HTCLIENT : HTCAPTION);
            }

            case WM_SETCURSOR:
                if (((long)lParam & 0xFFFF) == HTCLIENT)
                {
                    SetCursor(LoadCursorW(IntPtr.Zero, IDC_HAND));
                    return (IntPtr)1;
                }
                break;

            case WM_LBUTTONUP:
                ApplyLock(hwnd, true, notify: true);
                return IntPtr.Zero;

            case WM_APP_LOCK:
                ApplyLock(hwnd, !_locked, notify: true);
                return IntPtr.Zero;

            case WM_NCRBUTTONUP:
                SetVisible(hwnd, false);
                return IntPtr.Zero;

            case WM_HOTKEY:
                if ((long)wParam == 2)
                {
                    ApplyLock(hwnd, !_locked, notify: true);
                }
                else
                {
                    SetVisible(hwnd, !IsWindowVisible(hwnd));
                }
                return IntPtr.Zero;

            case WM_EXITSIZEMOVE:
                if (GetWindowRect(hwnd, out var r))
                {
                    _onMoved?.Invoke(r.Left, r.Top);
                }
                return IntPtr.Zero;

            case WM_APP_STOP:
                DestroyWindow(hwnd);
                return IntPtr.Zero;

            case WM_APP_TOGGLE:
                SetVisible(hwnd, !IsWindowVisible(hwnd));
                return IntPtr.Zero;

            case WM_APP_RELAYOUT:
                Relayout(hwnd);
                return IntPtr.Zero;

            case WM_APP_APPEARANCE:
                CreateFonts();
                _stableW = 0; // the width may shrink when the overlay gets smaller
                ApplyLock(hwnd, _locked, notify: false); // also applies the opacity
                Relayout(hwnd);
                return IntPtr.Zero;

            case WM_PAINT:
                Paint(hwnd);
                return IntPtr.Zero;

            case WM_PRINTCLIENT: // snapshot (--overlay-preview): draw into the given DC
                PaintTo(hwnd, wParam);
                return (IntPtr)1;

            case WM_DESTROY:
                UnregisterHotKey(hwnd, 1);
                UnregisterHotKey(hwnd, 2);
                PostQuitMessage(0);
                return IntPtr.Zero;
        }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    /// <summary>
    /// Locked: click-through. Unlocked: a regular interactive panel, with whole-window opacity if requested.
    /// </summary>
    private static void ApplyLock(IntPtr hwnd, bool locked, bool notify)
    {
        _locked = locked;
        var alpha = Alpha;
        var perPixel = IsHud && locked;
        var layered = locked || alpha < 255;
        var ex = (long)GetWindowLongPtrW(hwnd, GWL_EXSTYLE);
        // Reset the layered surface when switching between global and per-pixel alpha.
        if (_perPixelLayer != perPixel && (ex & WS_EX_LAYERED) != 0)
            SetWindowLongPtrW(hwnd, GWL_EXSTYLE, (IntPtr)(ex & ~(long)WS_EX_LAYERED));
        _perPixelLayer = perPixel;
        ex = layered ? ex | WS_EX_LAYERED : ex & ~(long)WS_EX_LAYERED;
        ex = locked ? ex | WS_EX_TRANSPARENT : ex & ~(long)WS_EX_TRANSPARENT;
        SetWindowLongPtrW(hwnd, GWL_EXSTYLE, (IntPtr)ex);
        if (layered && !perPixel) SetLayeredWindowAttributes(hwnd, 0, alpha, LWA_ALPHA);
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        InvalidateRect(hwnd, IntPtr.Zero, true);

        if (notify)
        {
            if (IsHud) RefreshHud(hwnd);
            if (!locked && !IsWindowVisible(hwnd)) SetVisible(hwnd, true);
            _onLockChanged?.Invoke(locked);
            Console.WriteLine(locked
                ? L.T($"Overlay verrouillé : la souris passe au travers. {(LockHotkeyRegistered ? "Ctrl+Maj+L" : "« Déverrouiller l'overlay » dans le compteur")} pour le déverrouiller.",
                      $"Overlay locked: the mouse goes through it. {(LockHotkeyRegistered ? "Ctrl+Shift+L" : "\"Unlock the overlay\" in the counter")} to unlock it.",
                      $"Overlay bloqueado: el ratón lo atraviesa. {(LockHotkeyRegistered ? "Ctrl+Mayús+L" : "«Desbloquear el overlay» en el contador")} para desbloquearlo.")
                : L.T("Overlay déverrouillé : clic gauche maintenu pour le déplacer, cadenas pour le reverrouiller.",
                      "Overlay unlocked: hold the left mouse button to move it, click the padlock to lock it again.",
                      "Overlay desbloqueado: arrástralo con el clic izquierdo; el candado lo vuelve a bloquear."));
        }
    }

    private static void SetVisible(IntPtr hwnd, bool visible)
    {
        if (visible)
        {
            ShowWindow(hwnd, SW_SHOWNOACTIVATE);
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
        else
        {
            ShowWindow(hwnd, SW_HIDE);
            Console.WriteLine(HotkeyRegistered
                ? L.T("Overlay masqué : Ctrl+Maj+O (n'importe où) pour le réafficher.", "Overlay hidden: Ctrl+Shift+O (anywhere) to show it again.", "Overlay oculto: Ctrl+Mayús+O (desde cualquier sitio) para volver a mostrarlo.")
                : L.T("Overlay masqué : « Afficher l'overlay » dans le compteur pour le réafficher.", "Overlay hidden: \"Show the overlay\" in the counter to show it again.", "Overlay oculto: «Mostrar el overlay» en el contador para volver a mostrarlo."));
        }
    }

    private readonly record struct TextRun(string Text, IntPtr Font, int Offset, int Height);

    private static List<TextRun> MeasureRuns(IntPtr hdc, IntPtr font, string text, out int width, out int height)
    {
        if (text.Length == 0) text = " ";
        if (!NumericFonts.TryGetValue(font, out var numeric) || numeric == IntPtr.Zero) numeric = font;
        var runs = new List<TextRun>();
        width = 0;
        height = 0;
        for (var start = 0; start < text.Length;)
        {
            var face = char.IsAsciiDigit(text[start]) ? numeric : font;
            var end = start + 1;
            while (end < text.Length && (char.IsAsciiDigit(text[end]) ? numeric : font) == face) end++;
            var segment = text[start..end];
            SelectObject(hdc, face);
            var bounds = new RECT();
            DrawTextW(hdc, segment, -1, ref bounds, DT_CALCRECT | DT_SINGLELINE | DT_NOPREFIX);
            runs.Add(new TextRun(segment, face, width, bounds.Bottom));
            width += bounds.Right;
            height = Math.Max(height, bounds.Bottom);
            start = end;
        }
        return runs;
    }

    private static int TextWidth(IntPtr hdc, IntPtr font, string text, out int height)
    {
        MeasureRuns(hdc, font, text, out var width, out height);
        return width;
    }

    private static void DrawRuns(IntPtr hdc, IReadOnlyList<TextRun> runs, int width, int height,
        int left, int top, int right, int bottom, uint align)
    {
        if ((align & DT_RIGHT) != 0) left = right - width;
        if ((align & DT_CENTER) != 0) left += (right - left - width) / 2;
        var flags = DT_SINGLELINE | DT_NOPREFIX | (align & ~(DT_RIGHT | DT_CENTER));
        foreach (var run in runs)
        {
            SelectObject(hdc, run.Font);
            var rect = new RECT { Left = left + run.Offset, Top = top + (height - run.Height) / 2, Right = right, Bottom = bottom };
            DrawTextW(hdc, run.Text, -1, ref rect, flags);
        }
    }

    /// <summary>Measures the text and resizes the window (and its rounded region) to fit it.</summary>
    private static void Relayout(IntPtr hwnd, bool firstLayout = false)
    {
        if (IsHud)
        {
            RelayoutStyled(hwnd, firstLayout);
            return;
        }
        int S(int v) => (int)Math.Round(v * _scale);
        var v = _view;
        var hdc = GetDC(hwnd);
        try
        {
            // Header: "COFFRES" ... "● EN DIRECT" [lock]
            var headerW = TextWidth(hdc, _fontHeader, Header, out _headerH) + S(16) + (v.Badge != null ? TextWidth(hdc, _fontHeader, v.Badge, out _) : 0) + S(30);
            _headerH = Math.Max(_headerH, S(18));

            // Two counters side by side.
            _col1W = Math.Max(S(39) + TextWidth(hdc, _fontBig, v.Total, out _bigH), TextWidth(hdc, _fontCaption, CaptionTotal, out _captionH));
            _col2W = Math.Max(TextWidth(hdc, _fontBig, v.Current ?? "—", out _), Math.Max(TextWidth(hdc, _fontCaption, v.CurrentCaption, out _), TextWidth(hdc, _fontCaption, OverlayModel.LastMatchCaption, out _)));
            var countersW = _col1W + S(ColGap) + _col2W;

            // Detail rows: grey label column + value.
            _labelW = 0;
            var valueW = 0;
            _rowH = 0;
            foreach (var row in v.Rows)
            {
                _labelW = Math.Max(_labelW, TextWidth(hdc, _fontLabel, row.Label, out var lh));
                valueW = Math.Max(valueW, TextWidth(hdc, _fontSmall, row.Value, out var vh)
                    + (row.Detail != null ? S(6) + TextWidth(hdc, _fontCaption, row.Detail, out _) : 0));
                _rowH = Math.Max(_rowH, Math.Max(lh, vh));
            }
            var rowsW = v.Rows.Count > 0 ? _labelW + S(12) + valueW : 0;

            // Never shrinks: the overlay keeps the same size between a match in progress and the end of the match.
            var w = Math.Max(_stableW, Math.Max(S(MinWidth), Math.Max(headerW, Math.Max(countersW, rowsW)) + 2 * S(Pad)));
            _stableW = w;
            var h = S(12) + _headerH + S(6) + _bigH + _captionH + S(12);
            if (v.Rows.Count > 0)
            {
                h += 1 + S(10) + v.Rows.Count * (_rowH + S(4)) + S(4);
            }
            _lockRect = new RECT { Left = w - S(Pad) - S(18), Top = S(6), Right = w - S(6), Bottom = S(12) + _headerH + S(6) };
            // Keep it fully on its screen: on the right half of the screen it grows to the left, and it is
            // pushed back inside when it would overflow (a wider overlay used to be cut by the screen edge).
            GetWindowRect(hwnd, out var cur);
            int nx = cur.Left, ny = cur.Top;
            var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
            if (!_offscreen && GetMonitorInfoW(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), ref mi))
            {
                var work = mi.rcWork;
                if (!firstLayout && cur.Right - cur.Left > 0 && (cur.Left + cur.Right) / 2 > (work.Left + work.Right) / 2)
                {
                    nx = cur.Right - w; // anchored on the right edge
                }
                nx = Math.Max(work.Left, Math.Min(nx, work.Right - w));
                ny = Math.Max(work.Top, Math.Min(ny, work.Bottom - h));
            }
            SetWindowPos(hwnd, HWND_TOPMOST, nx, ny, w, h, SWP_NOACTIVATE);
            SetWindowRgn(hwnd, CreateRoundRectRgn(0, 0, w + 1, h + 1, S(18), S(18)), true); // window owns the region
            if (nx != cur.Left || ny != cur.Top)
            {
                _onMoved?.Invoke(nx, ny);
            }
        }
        finally
        {
            ReleaseDC(hwnd, hdc);
        }
        InvalidateRect(hwnd, IntPtr.Zero, true);
        UpdateWindow(hwnd); // paint now (one repaint per update, no timer)
    }

    private static void Text(IntPtr hdc, IntPtr font, uint color, string text, int left, int top, int right, int bottom, uint align = DT_LEFT)
    {
        var runs = MeasureRuns(hdc, font, text, out var width, out var height);
        SetTextColor(hdc, color);
        DrawRuns(hdc, runs, width, height, left, top, right, bottom, align);
    }

    private static void Paint(IntPtr hwnd)
    {
        var hdc = BeginPaint(hwnd, out var ps);
        try
        {
            if (!_perPixelLayer) PaintTo(hwnd, hdc);
        }
        finally
        {
            EndPaint(hwnd, ref ps);
        }
    }

    private static void PaintTo(IntPtr hwnd, IntPtr hdc)
    {
        if (IsHud)
        {
            GetClientRect(hwnd, out var bounds);
            PaintStyled(hdc, bounds, _view, _locked ? Rgb(20, 81, 157) : Rgb(19, 44, 91));
            return;
        }
        var v = _view;
        {
            GetClientRect(hwnd, out var rc);
            FillRect(hdc, ref rc, _brushBg);
            SetBkMode(hdc, TRANSPARENT);

            int S(int n) => (int)Math.Round(n * _scale);
            var x = S(Pad);
            var right = rc.Right - S(Pad);
            var y = S(12);

            // Thin border following the rounded shape.
            var border = CreateRoundRectRgn(0, 0, rc.Right + 1, rc.Bottom + 1, S(18), S(18));
            FrameRgn(hdc, border, _brushBorder, 1, 1);
            DeleteObject(border);

            // Header
            Text(hdc, _fontHeader, Rgb(225, 243, 255), Header, x, y, right, y + _headerH);
            if (v.Badge != null)
            {
                Text(hdc, _fontHeader, Rgb(96, 214, 128), v.Badge, x, y, _lockRect.Left - S(6), y + _headerH, DT_RIGHT);
            }
            // Lock button (Segoe MDL2 Assets: E785 = open padlock, E72E = closed padlock).
            SelectObject(hdc, _fontIcon);
            SetTextColor(hdc, _locked ? Rgb(96, 96, 106) : Rgb(200, 200, 208));
            var lr = _lockRect;
            DrawTextW(hdc, _locked ? "" : "", -1, ref lr, DT_SINGLELINE | DT_CENTER | DT_VCENTER | DT_NOPREFIX);
            y += _headerH + S(6);

            // Counters: total | match in progress
            var x2 = x + _col1W + S(ColGap);
            DrawChestBadge(hdc, x, y + (_bigH - S(34)) / 2, S(34));
            Text(hdc, _fontBig, Rgb(156, 220, 255), v.Total, x + S(39), y, x + _col1W + S(4), y + _bigH);
            Text(hdc, _fontBig, v.Current == null ? Rgb(80, 80, 92) : v.CurrentLive ? Rgb(110, 224, 150) : Rgb(226, 226, 232), v.Current ?? "—", x2 - S(1), y, right + S(4), y + _bigH);
            var sep = new RECT { Left = x + _col1W + S(ColGap) / 2, Top = y + S(8), Right = x + _col1W + S(ColGap) / 2 + 1, Bottom = y + _bigH + _captionH };
            FillRect(hdc, ref sep, _brushLine);
            y += _bigH;
            Text(hdc, _fontCaption, Rgb(174, 201, 235), CaptionTotal, x, y, x + _col1W + S(4), y + _captionH);
            Text(hdc, _fontCaption, Rgb(174, 201, 235), v.CurrentCaption, x2, y, right + S(4), y + _captionH);
            y += _captionH + S(12);

            if (v.Rows.Count > 0)
            {
                var line = new RECT { Left = x, Top = y, Right = right, Bottom = y + 1 };
                FillRect(hdc, ref line, _brushLine);
                y += 1 + S(10);
                foreach (var row in v.Rows)
                {
                    Text(hdc, _fontLabel, Rgb(174, 201, 235), row.Label, x, y, x + _labelW, y + _rowH);
                    var vx = x + _labelW + S(12);
                    Text(hdc, _fontSmall, row.Treasure ? Rgb(196, 160, 255) : Rgb(226, 226, 232), row.Value, vx, y, right + S(4), y + _rowH);
                    if (row.Detail != null)
                    {
                        var dx = vx + TextWidth(hdc, _fontSmall, row.Value, out _) + S(6);
                        Text(hdc, _fontCaption, Rgb(174, 201, 235), row.Detail, dx, y, right + S(4), y + _rowH - S(2), DT_BOTTOM);
                    }
                    y += _rowH + S(4);
                }
            }
        }
    }


    // ---- "hud" style: figures only, outlined like the game's own HUD, right-aligned under the minimap -------------
    //
    //  ● [chest] 3 934       total; the red dot while a match is in progress (gold while it is counted live)
    //   Dernière • 14        last finished match, kept during the next one
    //    Session • 83
    //      [star] 36         treasures

    private sealed record HudRow(string Label, string Value);

    private static HudRow StateRow(OverlayView v) => new(L.T("Dernière", "Last", "Última"), v.Last ?? "—");

    /// <summary>The figures of the match in progress are readable (its chests are already in the total).</summary>
    private static bool LiveFigures(OverlayView v) => v.CurrentLive && v.Current is { } cur && cur != "…";

    private static int Sc(double v) => (int)Math.Round(v * _scale);

    private const string Sep = "  •  ";

    private static void RelayoutStyled(IntPtr hwnd, bool firstLayout)
    {
        var v = _view;
        var hdc = GetDC(hwnd);
        int w, h;
        try
        {
            var pad = Sc(8);
            var lockW = Sc(22);
            var bigW = TextWidth(hdc, _fontHudBig, v.Total, out _hudBigH);
            var state = StateRow(v);
            var sessionLabel = L.T("Session", "Session", "Sesión");
            var rowsW = new[]
            {
                TextWidth(hdc, _fontHudSmall, state.Label + Sep + state.Value, out _hudSmallH),
                TextWidth(hdc, _fontHudSmall, sessionLabel + Sep + v.Session, out _),
                v.Squad != null ? TextWidth(hdc, _fontHudSmall, OverlayModel.SquadLabel + Sep + v.Squad, out _) : 0,
                Sc(22) + TextWidth(hdc, _fontHudSmall, v.Treasures, out _),
                TextWidth(hdc, _fontHudSmall, OverlayModel.TokenLabel + Sep + v.TokenChance, out _),
            }.Max();
            // Room for the in-match dot is always kept: the overlay does not widen when a match starts.
            w = lockW + pad + Math.Max(Sc(18) + Sc(36) + Sc(8) + bigW, rowsW) + pad + Sc(14);
            h = pad + _hudBigH + (v.Squad != null ? 5 : 4) * (_hudSmallH + Sc(2)) + pad;
            w = Math.Max(_stableW, w);
            _stableW = w;
            _lockRect = new RECT { Left = Sc(2), Top = (h - lockW) / 2, Right = lockW, Bottom = (h + lockW) / 2 };

            GetWindowRect(hwnd, out var cur);
            int nx = cur.Left, ny = cur.Top;
            var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
            if (!_offscreen && GetMonitorInfoW(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), ref mi))
            {
                var work = mi.rcWork;
                if (!firstLayout && cur.Right - cur.Left > 0 && (cur.Left + cur.Right) / 2 > (work.Left + work.Right) / 2)
                {
                    nx = cur.Right - w; // anchored on the right edge (under the minimap)
                }
                nx = Math.Max(work.Left, Math.Min(nx, work.Right - w));
                ny = Math.Max(work.Top, Math.Min(ny, work.Bottom - h));
            }
            SetWindowPos(hwnd, HWND_TOPMOST, nx, ny, w, h, SWP_NOACTIVATE);
            if (nx != cur.Left || ny != cur.Top)
            {
                _onMoved?.Invoke(nx, ny);
            }
        }
        finally
        {
            ReleaseDC(hwnd, hdc);
        }
        RefreshHud(hwnd);
    }

    private static void RefreshHud(IntPtr hwnd)
    {
        if (_perPixelLayer)
        {
            SetWindowRgn(hwnd, IntPtr.Zero, false);
            PresentHud(hwnd);
            return;
        }
        GetClientRect(hwnd, out var bounds);
        var corners = Sc(12);
        var region = CreateRoundRectRgn(0, 0, bounds.Right + 1, bounds.Bottom + 1, corners, corners);
        if (SetWindowRgn(hwnd, region, true) == 0) DeleteObject(region);
        InvalidateRect(hwnd, IntPtr.Zero, false);
        UpdateWindow(hwnd);
    }

    private static void PresentHud(IntPtr hwnd)
    {
        if (!GetClientRect(hwnd, out var bounds) || bounds.Right <= 0 || bounds.Bottom <= 0) return;
        var size = new SIZE { Width = bounds.Right, Height = bounds.Bottom };
        var info = new BITMAPINFOHEADER { biSize = 40, biWidth = size.Width, biHeight = -size.Height, biPlanes = 1, biBitCount = 32 };
        var dc = CreateCompatibleDC(IntPtr.Zero);
        if (dc == IntPtr.Zero) return;
        var bitmap = CreateDIBSection(dc, ref info, 0, out var bits, IntPtr.Zero, 0);
        if (bitmap == IntPtr.Zero)
        {
            DeleteDC(dc);
            return;
        }
        var old = SelectObject(dc, bitmap);
        try
        {
            var v = _view;
            var dark = new byte[size.Width * size.Height * 4];
            var light = new byte[dark.Length];
            PaintStyled(dc, bounds, v, 0);
            GdiFlush();
            Marshal.Copy(bits, dark, 0, dark.Length);
            PaintStyled(dc, bounds, v, Rgb(255, 255, 255));
            GdiFlush();
            Marshal.Copy(bits, light, 0, light.Length);
            // The black matte holds the premultiplied color. The difference to the white matte
            // is the fraction of background left at each pixel, including anti-aliased edges.
            for (var i = 0; i < dark.Length; i += 4)
            {
                var uncovered = Math.Min(light[i] - dark[i], Math.Min(light[i + 1] - dark[i + 1], light[i + 2] - dark[i + 2]));
                dark[i + 3] = (byte)(255 - Math.Max(0, uncovered));
            }
            Marshal.Copy(dark, 0, bits, dark.Length);
            var origin = new POINT();
            var blend = new BLENDFUNCTION { SourceConstantAlpha = Alpha, AlphaFormat = AC_SRC_ALPHA };
            if (!UpdateLayeredWindow(hwnd, IntPtr.Zero, IntPtr.Zero, ref size, dc, ref origin, 0, ref blend, ULW_ALPHA))
                System.Diagnostics.Debug.WriteLine($"HUD compositing failed: {Marshal.GetLastWin32Error()}");
        }
        finally
        {
            SelectObject(dc, old);
            DeleteObject(bitmap);
            DeleteDC(dc);
        }
    }

    private static int OutlineRadius(bool thin) => Math.Max(1, Sc(thin ? 1.75 : 3));

    /// <summary>Text outlined in near black, like the game's own HUD figures (readable on any background).</summary>
    private static void Outlined(IntPtr hdc, IntPtr font, uint color, string text, int left, int top, int right, int bottom, uint align, bool thin = false)
    {
        var runs = MeasureRuns(hdc, font, text, out var width, out var height);
        var o = OutlineRadius(thin);
        SetTextColor(hdc, Rgb(11, 15, 24));
        for (var dx = -o; dx <= o; dx++)
        {
            for (var dy = -o; dy <= o; dy++)
            {
                if (dx * dx + dy * dy > o * o || (dx == 0 && dy == 0)) continue;
                DrawRuns(hdc, runs, width, height, left + dx, top + dy, right + dx, bottom + dy, align | DT_NOCLIP);
            }
        }
        SetTextColor(hdc, color);
        DrawRuns(hdc, runs, width, height, left, top, right, bottom, align | DT_NOCLIP);
    }

    private static void DrawChestBadge(IntPtr hdc, int x, int y, int size) =>
        DrawIconEx(hdc, x, y, Brand.Icon.Value.Handle, size, size, 0, IntPtr.Zero, 3);

    private static void DrawHudChest(IntPtr hdc, int x, int y, int size)
    {
        var radius = OutlineRadius(thin: false);
        if (_hudChest == null)
        {
            _hudChest = new Bitmap(size + radius * 2, size + radius * 2, PixelFormat.Format32bppPArgb);
            using var image = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
            using (var resizer = Graphics.FromImage(image))
            {
                resizer.InterpolationMode = InterpolationMode.HighQualityBicubic;
                resizer.DrawImage(Brand.ChestImage.Value, new Rectangle(0, 0, size, size));
            }
            using var mask = new ImageAttributes();
            mask.SetColorMatrix(new ColorMatrix { Matrix00 = 0f, Matrix11 = 0f, Matrix22 = 0f,
                Matrix40 = 11 / 255f, Matrix41 = 15 / 255f, Matrix42 = 24 / 255f });
            using var graphics = Graphics.FromImage(_hudChest);
            for (var dx = -radius; dx <= radius; dx++)
            for (var dy = -radius; dy <= radius; dy++)
            {
                if (dx * dx + dy * dy > radius * radius) continue;
                var rect = new Rectangle(radius + dx, radius + dy, size, size);
                graphics.DrawImage(image, rect, 0, 0, size, size, GraphicsUnit.Pixel, mask);
            }
            graphics.DrawImageUnscaled(image, radius, radius);
        }
        using var target = Graphics.FromHdc(hdc);
        target.DrawImageUnscaled(_hudChest, x - radius, y - radius);
    }

    /// <summary>A five-pointed star (treasures), outlined like the text.</summary>
    private static void Star(IntPtr hdc, int cx, int cy, int r, uint fill, uint outline)
    {
        var pts = new POINT[10];
        for (var i = 0; i < 10; i++)
        {
            var a = -Math.PI / 2 + i * Math.PI / 5;
            var rr = i % 2 == 0 ? r : r * 0.45;
            pts[i] = new POINT { X = cx + (int)Math.Round(rr * Math.Cos(a)), Y = cy + (int)Math.Round(rr * Math.Sin(a)) };
        }
        var pen = CreatePen(0, Math.Max(1, Sc(1.5)), outline);
        var brush = CreateSolidBrush(fill);
        var oldPen = SelectObject(hdc, pen);
        var old = SelectObject(hdc, brush);
        Polygon(hdc, pts, pts.Length);
        SelectObject(hdc, old);
        SelectObject(hdc, oldPen);
        DeleteObject(brush);
        DeleteObject(pen);
    }

    private static void Disc(IntPtr hdc, int x, int y, int d, uint fill, uint outline, int penW)
    {
        var pen = CreatePen(0, penW, outline);
        var brush = CreateSolidBrush(fill);
        var oldPen = SelectObject(hdc, pen);
        var old = SelectObject(hdc, brush);
        Ellipse(hdc, x, y, x + d, y + d);
        SelectObject(hdc, old);
        SelectObject(hdc, oldPen);
        DeleteObject(brush);
        DeleteObject(pen);
    }

    /// <summary>"Label  •  value" right-aligned: pale label, white value.</summary>
    private static int LabelValue(IntPtr hdc, string label, string value, int left, int right, int top, int height, uint valueColor)
    {
        if (value.Length == 0)
        {
            Outlined(hdc, _fontHudSmall, Rgb(199, 230, 255), label, left, top, right, top + height, DT_RIGHT, thin: true);
            return right - TextWidth(hdc, _fontHudSmall, label, out _);
        }
        var valueW = TextWidth(hdc, _fontHudSmall, value, out _);
        Outlined(hdc, _fontHudSmall, valueColor, value, left, top, right, top + height, DT_RIGHT, thin: true);
        var labelRight = right - valueW;
        Outlined(hdc, _fontHudSmall, Rgb(199, 230, 255), label + Sep, left, top, labelRight, top + height, DT_RIGHT, thin: true);
        return labelRight - TextWidth(hdc, _fontHudSmall, label + Sep, out _);
    }

    private static void PaintStyled(IntPtr hdc, RECT rc, OverlayView v, uint matte)
    {
        var background = CreateSolidBrush(matte);
        FillRect(hdc, ref rc, background);
        DeleteObject(background);
        SetBkMode(hdc, TRANSPARENT);
        var pad = Sc(8);
        var right = rc.Right - pad - Sc(12); // italic glyphs and their outline lean past the measured width
        var white = Rgb(255, 255, 255);
        var gold = Rgb(186, 252, 130);
        var ink = Rgb(11, 15, 24);

        // Unlocked: a dark backdrop to grab it, and the padlock (locked, only the figures remain, mouse goes through).
        if (!_locked)
        {
            var pen = CreatePen(0, 1, Rgb(58, 66, 86));
            var brush = CreateSolidBrush(Rgb(19, 44, 91));
            var oldPen = SelectObject(hdc, pen);
            var old = SelectObject(hdc, brush);
            RoundRect(hdc, 0, 0, rc.Right, rc.Bottom, Sc(12), Sc(12));
            SelectObject(hdc, old);
            SelectObject(hdc, oldPen);
            DeleteObject(brush);
            DeleteObject(pen);
            SelectObject(hdc, _fontIcon);
            SetTextColor(hdc, Rgb(200, 200, 208));
            var lr = _lockRect;
            DrawTextW(hdc, "", -1, ref lr, DT_SINGLELINE | DT_CENTER | DT_VCENTER | DT_NOPREFIX);
        }

        var left = _lockRect.Right;
        var y = pad;
        var state = StateRow(v);
        var live = LiveFigures(v);

        // Total, with the chest, and the red dot while a match is in progress.
        var bigW = TextWidth(hdc, _fontHudBig, v.Total, out _);
        Outlined(hdc, _fontHudBig, live ? gold : white, v.Total, left, y, right, y + _hudBigH, DT_RIGHT);
        var iw = Sc(36);
        var ix = right - bigW - Sc(8) - iw;
        DrawHudChest(hdc, ix, y + (_hudBigH - iw) / 2, iw);
        if (v.CurrentLive)
        {
            var d = Sc(11);
            Disc(hdc, ix - Sc(7) - d, y + (_hudBigH - d) / 2, d, Rgb(255, 59, 48), ink, Math.Max(1, Sc(1.5)));
        }
        y += _hudBigH + Sc(2);

        // Last finished match (also during the next one), session, treasures.
        var rowH = _hudSmallH;
        LabelValue(hdc, state.Label, state.Value, left, right, y, rowH, white);
        y += rowH + Sc(2);
        LabelValue(hdc, L.T("Session", "Session", "Sesión"), v.Session, left, right, y, rowH, white);
        y += rowH + Sc(2);
        if (v.Squad != null)
        {
            LabelValue(hdc, OverlayModel.SquadLabel, v.Squad, left, right, y, rowH, white);
            y += rowH + Sc(2);
        }
        var tw = TextWidth(hdc, _fontHudSmall, v.Treasures, out _);
        Outlined(hdc, _fontHudSmall, Rgb(214, 190, 255), v.Treasures, left, y, right, y + rowH, DT_RIGHT, thin: true);
        Star(hdc, right - tw - Sc(12), y + rowH / 2, Sc(8), Rgb(196, 155, 255), ink);
        y += rowH + Sc(2);
        LabelValue(hdc, OverlayModel.TokenLabel, v.TokenChance, left, right, y, rowH, white);
    }

    [DllImport("user32.dll")]
    private static extern bool DrawIconEx(IntPtr hdc, int x, int y, IntPtr icon, int width, int height, uint step, IntPtr brush, uint flags);

    private static uint Rgb(byte r, byte g, byte b) => (uint)(r | (g << 8) | (b << 16));

    // ---- Win32 ------------------------------------------------------------------------------
    private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const uint WS_POPUP = 0x80000000;
    private const uint WS_EX_TOPMOST = 0x00000008, WS_EX_TRANSPARENT = 0x00000020, WS_EX_TOOLWINDOW = 0x00000080, WS_EX_LAYERED = 0x00080000, WS_EX_NOACTIVATE = 0x08000000;
    private const int GWL_EXSTYLE = -20, HTTRANSPARENT = -1, HTCLIENT = 1;
    private const uint WM_SETCURSOR = 0x0020, WM_LBUTTONUP = 0x0202, WM_APP_LOCK = 0x8003, VK_L = 0x4C, SWP_FRAMECHANGED = 0x20;
    private static readonly IntPtr IDC_HAND = new(32649);
    private const uint LWA_ALPHA = 0x2, ULW_ALPHA = 0x2;
    private const byte AC_SRC_ALPHA = 1;
    private const int SW_HIDE = 0, SW_SHOWNOACTIVATE = 4;
    private const uint WM_DESTROY = 0x0002, WM_PAINT = 0x000F, WM_NCHITTEST = 0x0084, WM_NCRBUTTONUP = 0x00A5,
        WM_TIMER = 0x0113, WM_EXITSIZEMOVE = 0x0232, WM_APP_TOGGLE = 0x8001, WM_PRINTCLIENT = 0x0318, WM_APP_RELAYOUT = 0x8002, WM_HOTKEY = 0x0312, WM_APP_APPEARANCE = 0x8004, WM_APP_STOP = 0x8005;
    private const uint MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_NOREPEAT = 0x4000, VK_O = 0x4F;
    private const int HTCAPTION = 2;
    private const int TRANSPARENT = 1, LOGPIXELSY = 90;
    private const uint DT_LEFT = 0x0, DT_CENTER = 0x1, DT_RIGHT = 0x2, DT_VCENTER = 0x4, DT_BOTTOM = 0x8, DT_SINGLELINE = 0x20, DT_CALCRECT = 0x400, DT_NOPREFIX = 0x800, DT_NOCLIP = 0x100;
    private const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10;
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private static readonly IntPtr IDC_SIZEALL = new(32646);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEXW
    {
        public uint cbSize, style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra, cbWndExtra;
        public IntPtr hInstance, hIcon, hCursor, hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE { public int Width, Height; }

    [StructLayout(LayoutKind.Sequential)]
    private struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam, lParam;
        public uint time;
        public POINT pt;
        public uint lPrivate;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PAINTSTRUCT
    {
        public IntPtr hdc;
        public int fErase;
        public RECT rcPaint;
        public int fRestore, fIncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] rgbReserved;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClassExW(ref WNDCLASSEXW wc);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(uint exStyle, string className, string windowName, uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr DefWindowProcW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetMessageW(out MSG msg, IntPtr hwnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr DispatchMessageW(ref MSG msg);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool PostMessageW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern void PostQuitMessage(int code);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int cmd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint key, byte alpha, uint flags);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr dest, IntPtr position, ref SIZE size, IntPtr source, ref POINT origin, uint key, ref BLENDFUNCTION blend, uint flags);
    [DllImport("user32.dll")] private static extern int SetWindowRgn(IntPtr hwnd, IntPtr rgn, bool redraw);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool InvalidateRect(IntPtr hwnd, IntPtr rect, bool erase);
    [DllImport("user32.dll")] private static extern IntPtr BeginPaint(IntPtr hwnd, out PAINTSTRUCT ps);
    [DllImport("user32.dll")] private static extern bool EndPaint(IntPtr hwnd, ref PAINTSTRUCT ps);
    [DllImport("user32.dll")] private static extern int FillRect(IntPtr hdc, ref RECT rect, IntPtr brush);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int DrawTextW(IntPtr hdc, string text, int count, ref RECT rect, uint format);
    [DllImport("user32.dll")] private static extern UIntPtr SetTimer(IntPtr hwnd, UIntPtr id, uint ms, IntPtr proc);
    [DllImport("user32.dll")] private static extern IntPtr LoadCursorW(IntPtr instance, IntPtr name);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
    [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtrW(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtrW(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll")] private static extern IntPtr SetCursor(IntPtr cursor);
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandleW(string? name);
    [DllImport("gdi32.dll")] private static extern int GetDeviceCaps(IntPtr hdc, int index);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFontW(int height, int width, int escapement, int orientation, int weight, uint italic, uint underline, uint strikeOut, uint charSet, uint outPrecision, uint clipPrecision, uint quality, uint pitchAndFamily, string face);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int w, int h);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth, biHeight;
        public ushort biPlanes, biBitCount;
        public uint biCompression, biSizeImage;
        public int biXPelsPerMeter, biYPelsPerMeter;
        public uint biClrUsed, biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO { public uint cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }

    private const uint MONITOR_DEFAULTTONEAREST = 2;
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfoW(IntPtr monitor, ref MONITORINFO info);
    [DllImport("user32.dll")] private static extern bool UpdateWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr SendMessageW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER info, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("gdi32.dll")] private static extern bool GdiFlush();
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern int GetDIBits(IntPtr hdc, IntPtr bmp, uint start, uint lines, byte[] bits, ref BITMAPINFOHEADER bi, uint usage);
    [DllImport("gdi32.dll")] private static extern bool FrameRgn(IntPtr hdc, IntPtr rgn, IntPtr brush, int w, int h);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern int SetBkMode(IntPtr hdc, int mode);
    [DllImport("gdi32.dll")] private static extern uint SetTextColor(IntPtr hdc, uint color);
    [DllImport("gdi32.dll")] private static extern IntPtr CreatePen(int style, int width, uint color);
    [DllImport("gdi32.dll")] private static extern bool Ellipse(IntPtr hdc, int l, int t, int r, int b);
    [DllImport("gdi32.dll")] private static extern bool Polygon(IntPtr hdc, POINT[] points, int count);
    [DllImport("gdi32.dll")] private static extern bool RoundRect(IntPtr hdc, int l, int t, int r, int b, int w, int h);
    [DllImport("user32.dll")] private static extern int FrameRect(IntPtr hdc, ref RECT rect, IntPtr brush);
}
