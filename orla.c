#ifndef UNICODE
#define UNICODE
#endif
#define _UNICODE
#define WINVER 0x0A00
#define _WIN32_WINNT 0x0A00
#include <windows.h>
#include <windowsx.h>
#include <commctrl.h>
#include <dwmapi.h>
#include <shellapi.h>
#include <shlobj.h>
#include <wchar.h>

#define CLASS_NAME L"OrlaDock.Window"
#define MAX_TASKS 96
#define MAX_VISIBLE 8
#define TITLE_LENGTH 160

enum { TIMER_CLOCK = 1, TIMER_REFRESH, TIMER_TOOLTIP, HOTKEY_MENU };
enum { SLOT_MENU, SLOT_FILES, SLOT_MORE, SLOT_WINDOW = 100 };
enum {
    CMD_FILES = 1, CMD_TASKBAR, CMD_TOP, CMD_BOTTOM, CMD_EXIT,
    CMD_THEME_FIRST = 20, CMD_WINDOW_FIRST = 100
};

typedef struct {
    const WCHAR *name;
    COLORREF background, border, surface, hover, accent, ink, muted;
} Theme;

static const Theme themes[] = {
    {L"Grafite", RGB(20, 25, 30), RGB(57, 67, 77), RGB(30, 37, 44), RGB(44, 54, 63),
        RGB(142, 232, 201), RGB(242, 247, 249), RGB(164, 181, 190)},
    {L"Azul-marinho", RGB(18, 27, 44), RGB(54, 76, 111), RGB(28, 43, 67), RGB(41, 60, 91),
        RGB(146, 196, 251), RGB(239, 245, 255), RGB(169, 188, 221)},
    {L"Âmbar", RGB(32, 25, 21), RGB(82, 63, 52), RGB(47, 37, 31), RGB(65, 49, 40),
        RGB(247, 187, 123), RGB(254, 245, 236), RGB(199, 175, 155)}
};

typedef struct {
    HWND window;
    DWORD process;
    HICON icon;
    WCHAR title[TITLE_LENGTH];
} Task;

typedef struct {
    int id;
    RECT rect;
} Slot;

static HWND dock, tooltip, foreground;
static HICON default_icon;
static HFONT clock_font, detail_font;
static HWINEVENTHOOK hooks[4];
static Task tasks[MAX_TASKS];
static int task_count, theme_index, hovered = -1, pressed = -1;
static UINT dpi = 96;
static BOOL at_top, refresh_pending, refresh_deferred, menu_open;
static WCHAR settings_path[MAX_PATH], tooltip_text[TITLE_LENGTH];
static SIZE dock_size;
static Slot slots[MAX_VISIBLE + 3];
static int slot_count, first_divider, second_divider;
static RECT task_area, clock_area;

static int scale(int value)
{
    return MulDiv(value, (int)dpi, 96);
}

static void load_preferences(void)
{
    PWSTR local = NULL;
    if (FAILED(SHGetKnownFolderPath(&FOLDERID_LocalAppData, KF_FLAG_DEFAULT, NULL, &local)))
        return;
    int length = swprintf(settings_path, ARRAYSIZE(settings_path), L"%ls\\OrlaDock", local);
    CoTaskMemFree(local);
    if (length < 0 || length + 17 >= MAX_PATH) {
        settings_path[0] = L'\0';
        return;
    }
    CreateDirectoryW(settings_path, NULL);
    wcscat(settings_path, L"\\preferencias.ini");
    at_top = GetPrivateProfileIntW(L"Dock", L"Top", 0, settings_path) == 1;
    UINT stored = GetPrivateProfileIntW(L"Dock", L"Theme", 0, settings_path);
    if (stored < ARRAYSIZE(themes))
        theme_index = (int)stored;
}

static void save_preferences(void)
{
    if (!settings_path[0])
        return;
    WCHAR theme[8];
    swprintf(theme, ARRAYSIZE(theme), L"%d", theme_index);
    WritePrivateProfileStringW(L"Dock", L"Theme", theme, settings_path);
    WritePrivateProfileStringW(L"Dock", L"Top", at_top ? L"1" : L"0", settings_path);
}

static HICON load_window_icon(HWND window)
{
    DWORD_PTR icon = 0;
    SendMessageTimeoutW(window, WM_GETICON, ICON_SMALL2, (LPARAM)dpi,
        SMTO_ABORTIFHUNG | SMTO_BLOCK, 20, &icon);
    if (!icon)
        icon = GetClassLongPtrW(window, GCLP_HICONSM);
    if (!icon)
        icon = GetClassLongPtrW(window, GCLP_HICON);
    return icon ? CopyIcon((HICON)icon) : NULL;
}

typedef struct {
    Task items[MAX_TASKS];
    int count;
} TaskList;

static BOOL CALLBACK collect_window(HWND window, LPARAM data)
{
    TaskList *list = (TaskList *)data;
    if (list->count == MAX_TASKS)
        return FALSE;
    if (window == dock || window == tooltip || window == GetShellWindow() || !IsWindowVisible(window))
        return TRUE;

    LONG_PTR styles = GetWindowLongPtrW(window, GWL_EXSTYLE);
    if (!(styles & WS_EX_APPWINDOW) &&
        ((styles & (WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE)) || GetWindow(window, GW_OWNER)))
        return TRUE;
    DWORD cloaked = 0;
    DwmGetWindowAttribute(window, DWMWA_CLOAKED, &cloaked, sizeof(cloaked));
    if (cloaked)
        return TRUE;

    Task *task = &list->items[list->count];
    if (!GetWindowTextW(window, task->title, ARRAYSIZE(task->title)))
        return TRUE;
    task->window = window;
    GetWindowThreadProcessId(window, &task->process);
    list->count++;
    return TRUE;
}

static void refresh_tasks(void)
{
    TaskList list = {0};
    EnumWindows(collect_window, (LPARAM)&list);
    int kept = 0;
    for (int i = 0; i < task_count; i++) {
        int match = 0;
        while (match < list.count &&
            (list.items[match].window != tasks[i].window ||
             list.items[match].process != tasks[i].process))
            match++;
        if (match == list.count) {
            if (tasks[i].icon)
                DestroyIcon(tasks[i].icon);
            continue;
        }
        list.items[match].icon = tasks[i].icon;
        tasks[kept++] = list.items[match];
        list.items[match].window = NULL;
    }
    for (int i = 0; i < list.count; i++) {
        if (!list.items[i].window)
            continue;
        list.items[i].icon = load_window_icon(list.items[i].window);
        tasks[kept++] = list.items[i];
    }
    task_count = kept;
}

static BOOL is_fullscreen(HWND window, HMONITOR monitor, RECT bounds)
{
    if (!window || window == dock || IsIconic(window) ||
        MonitorFromWindow(window, MONITOR_DEFAULTTONULL) != monitor)
        return FALSE;
    LONG_PTR style = GetWindowLongPtrW(window, GWL_STYLE);
    if ((style & WS_CAPTION) == WS_CAPTION || window == GetShellWindow())
        return FALSE;
    RECT rect;
    if (FAILED(DwmGetWindowAttribute(window, DWMWA_EXTENDED_FRAME_BOUNDS, &rect, sizeof(rect))) &&
        !GetWindowRect(window, &rect))
        return FALSE;
    return rect.left <= bounds.left && rect.top <= bounds.top &&
        rect.right >= bounds.right && rect.bottom >= bounds.bottom;
}

static void make_fonts(void)
{
    if (clock_font)
        DeleteObject(clock_font);
    if (detail_font)
        DeleteObject(detail_font);
    clock_font = CreateFontW(-scale(18), 0, 0, 0, FW_SEMIBOLD, 0, 0, 0,
        DEFAULT_CHARSET, 0, 0, CLEARTYPE_QUALITY, 0, L"Segoe UI");
    detail_font = CreateFontW(-scale(11), 0, 0, 0, FW_NORMAL, 0, 0, 0,
        DEFAULT_CHARSET, 0, 0, CLEARTYPE_QUALITY, 0, L"Segoe UI");
}

static void add_slot(int id, int x, int top, int width, int height)
{
    slots[slot_count++] = (Slot){id, {x, top, x + width, top + height}};
}

static void place_dock(void)
{
    POINT origin = {0, 0};
    HMONITOR monitor = MonitorFromPoint(origin, MONITOR_DEFAULTTOPRIMARY);
    MONITORINFO info = {0};
    info.cbSize = sizeof(info);
    if (!GetMonitorInfoW(monitor, &info))
        return;
    foreground = GetForegroundWindow();
    if (!menu_open && is_fullscreen(foreground, monitor, info.rcMonitor)) {
        ShowWindow(dock, SW_HIDE);
        return;
    }

    UINT new_dpi = GetDpiForWindow(dock);
    if (new_dpi && (dpi != new_dpi || !clock_font)) {
        dpi = new_dpi;
        make_fonts();
    }
    RECT work = info.rcWork;
    int padding = scale(10);
    int unit = scale(42);
    int fixed = scale(196);
    int available = work.right - work.left - 2 * padding;
    int capacity = (available - fixed) / unit;
    if (capacity < 1)
        capacity = 1;
    if (capacity > MAX_VISIBLE)
        capacity = MAX_VISIBLE;
    int visible = task_count;
    if (visible > capacity)
        visible = capacity - 1;
    int task_width = scale(126);
    int occupied = unit * (visible + (visible < task_count));
    if (occupied > task_width)
        task_width = occupied;
    int width = fixed + task_width;
    int height = scale(58);
    if (width > available)
        width = available;
    if (width < scale(235))
        return;

    slot_count = 0;
    int top = scale(9);
    int button = scale(40);
    add_slot(SLOT_MENU, padding, top, button, button);
    add_slot(SLOT_FILES, padding + unit, top, button, button);
    first_divider = scale(98);
    second_divider = width - scale(90);
    task_area = (RECT){first_divider + scale(4), top, second_divider - scale(4), height - top};
    clock_area = (RECT){second_divider + scale(8), top, width - scale(7), height - top};
    int x = task_area.left + (task_area.right - task_area.left - occupied) / 2;
    for (int i = 0; i < visible; i++, x += unit)
        add_slot(SLOT_WINDOW + i, x, top, button, button);
    if (visible < task_count)
        add_slot(SLOT_MORE, x, top, button, button);

    int left = work.left + (work.right - work.left - width) / 2;
    int y = at_top ? work.top + padding : work.bottom - height - padding;
    if (dock_size.cx != width || dock_size.cy != height) {
        HRGN shape = CreateRoundRectRgn(0, 0, width + 1, height + 1, scale(28), scale(28));
        if (shape && !SetWindowRgn(dock, shape, FALSE))
            DeleteObject(shape);
        dock_size = (SIZE){width, height};
    }
    SetWindowPos(dock, HWND_TOPMOST, left, y, width, height,
        SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOOWNERZORDER);
    InvalidateRect(dock, NULL, FALSE);
}

static void fill(HDC dc, RECT rect, COLORREF color)
{
    SetDCBrushColor(dc, color);
    FillRect(dc, &rect, (HBRUSH)GetStockObject(DC_BRUSH));
}

static void rounded(HDC dc, RECT rect, int corner, COLORREF fill_color, COLORREF line)
{
    SetDCBrushColor(dc, fill_color);
    SetDCPenColor(dc, line);
    RoundRect(dc, rect.left, rect.top, rect.right, rect.bottom, corner, corner);
}

static void draw_glyph(HDC dc, RECT rect, int id, COLORREF color)
{
    int x = (rect.left + rect.right) / 2;
    int y = (rect.top + rect.bottom) / 2;
    SetDCPenColor(dc, color);
    SetDCBrushColor(dc, color);
    if (id == SLOT_MENU) {
        POINT wave[] = {
            {x - scale(9), y + scale(4)}, {x - scale(3), y - scale(5)},
            {x + scale(2), y + scale(2)}, {x + scale(9), y - scale(6)}
        };
        POINT ridge[] = {
            {x - scale(8), y + scale(8)}, {x - scale(3), y + scale(1)},
            {x + scale(2), y + scale(7)}, {x + scale(7), y + scale(1)}
        };
        Polyline(dc, wave, ARRAYSIZE(wave));
        Polyline(dc, ridge, ARRAYSIZE(ridge));
        return;
    }
    if (id == SLOT_MORE) {
        for (int i = -1; i <= 1; i++) {
            int dot = x + i * scale(6);
            Ellipse(dc, dot - scale(1), y - scale(1), dot + scale(2), y + scale(2));
        }
        return;
    }
    HGDIOBJ old_brush = SelectObject(dc, GetStockObject(NULL_BRUSH));
    POINT folder[] = {
        {x - scale(9), y + scale(6)}, {x - scale(9), y - scale(6)},
        {x - scale(3), y - scale(6)}, {x - scale(1), y - scale(3)},
        {x + scale(9), y - scale(3)}, {x + scale(9), y + scale(6)},
        {x - scale(9), y + scale(6)}
    };
    Polyline(dc, folder, ARRAYSIZE(folder));
    SelectObject(dc, old_brush);
}

static void draw_dock(HDC dc, RECT client)
{
    const Theme *theme = &themes[theme_index];
    HGDIOBJ old_brush = SelectObject(dc, GetStockObject(DC_BRUSH));
    HGDIOBJ old_pen = SelectObject(dc, GetStockObject(DC_PEN));
    fill(dc, client, theme->background);
    rounded(dc, client, scale(28), theme->background, theme->border);
    int top = scale(18);
    int bottom = client.bottom - top;
    fill(dc, (RECT){first_divider, top, first_divider + 1, bottom}, theme->border);
    fill(dc, (RECT){second_divider, top, second_divider + 1, bottom}, theme->border);

    HWND root_owner = foreground ? GetAncestor(foreground, GA_ROOTOWNER) : NULL;
    for (int i = 0; i < slot_count; i++) {
        int id = slots[i].id;
        RECT rect = slots[i].rect;
        BOOL active = id >= SLOT_WINDOW &&
            (tasks[id - SLOT_WINDOW].window == foreground || tasks[id - SLOT_WINDOW].window == root_owner);
        COLORREF surface = theme->surface;
        if (hovered == id || pressed == id || active)
            surface = theme->hover;
        if (id == SLOT_MENU)
            surface = theme->accent;
        if (id < SLOT_WINDOW || surface != theme->surface)
            rounded(dc, rect, scale(15), surface, surface);

        if (id >= SLOT_WINDOW) {
            HICON icon = tasks[id - SLOT_WINDOW].icon;
            int size = scale(22);
            DrawIconEx(dc, (rect.left + rect.right - size) / 2,
                rect.top + scale(7), icon ? icon : default_icon, size, size, 0, NULL, DI_NORMAL);
            int center = (rect.left + rect.right) / 2;
            int half = scale(active ? 5 : 2);
            fill(dc, (RECT){center - half, rect.bottom - scale(6),
                center + half, rect.bottom - scale(4)}, active ? theme->accent : theme->muted);
        } else {
            draw_glyph(dc, rect, id, id == SLOT_MENU ? theme->background : theme->ink);
        }
    }

    SetBkMode(dc, TRANSPARENT);
    HGDIOBJ old_font = SelectObject(dc, detail_font ? detail_font : GetStockObject(DEFAULT_GUI_FONT));
    SetTextColor(dc, theme->muted);
    if (!task_count) {
        RECT empty = task_area;
        DrawTextW(dc, L"Nenhuma janela aberta", -1, &empty,
            DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX | DT_END_ELLIPSIS);
    }
    SYSTEMTIME now;
    WCHAR time[16], date[24] = L"";
    GetLocalTime(&now);
    swprintf(time, ARRAYSIZE(time), L"%02u:%02u", now.wHour, now.wMinute);
    GetDateFormatW(LOCALE_USER_DEFAULT, 0, &now, L"ddd, dd/MM", date, ARRAYSIZE(date));
    RECT date_line = clock_area;
    date_line.top += scale(24);
    DrawTextW(dc, date, -1, &date_line, DT_CENTER | DT_SINGLELINE | DT_NOPREFIX);
    SelectObject(dc, clock_font ? clock_font : GetStockObject(DEFAULT_GUI_FONT));
    SetTextColor(dc, theme->ink);
    RECT time_line = clock_area;
    time_line.top += scale(2);
    DrawTextW(dc, time, -1, &time_line, DT_CENTER | DT_SINGLELINE | DT_NOPREFIX);

    SelectObject(dc, old_font);
    SelectObject(dc, old_pen);
    SelectObject(dc, old_brush);
}

static void hide_tooltip(void)
{
    KillTimer(dock, TIMER_TOOLTIP);
    if (tooltip) {
        TOOLINFOW tool = {.cbSize = sizeof(tool), .hwnd = dock, .uId = 1};
        SendMessageW(tooltip, TTM_TRACKACTIVATE, FALSE, (LPARAM)&tool);
    }
}

static void show_tooltip(void)
{
    if (!tooltip || hovered < 0 || menu_open)
        return;
    const WCHAR *text = L"Todas as janelas";
    if (hovered == SLOT_MENU)
        text = L"Menu do Orla Dock";
    else if (hovered == SLOT_FILES)
        text = L"Explorador de Arquivos";
    else if (hovered >= SLOT_WINDOW && hovered - SLOT_WINDOW < task_count)
        text = tasks[hovered - SLOT_WINDOW].title;
    lstrcpynW(tooltip_text, text, ARRAYSIZE(tooltip_text));

    POINT cursor;
    RECT bar;
    if (!GetCursorPos(&cursor) || !GetWindowRect(dock, &bar))
        return;
    TOOLINFOW tool = {.cbSize = sizeof(tool), .hwnd = dock, .uId = 1, .lpszText = tooltip_text};
    SendMessageW(tooltip, TTM_UPDATETIPTEXTW, 0, (LPARAM)&tool);
    int y = at_top ? bar.bottom + scale(4) : bar.top - scale(29);
    SendMessageW(tooltip, TTM_TRACKPOSITION, 0, MAKELPARAM(cursor.x, y));
    SendMessageW(tooltip, TTM_TRACKACTIVATE, TRUE, (LPARAM)&tool);
}

static int hit_test(POINT point)
{
    for (int i = 0; i < slot_count; i++) {
        if (PtInRect(&slots[i].rect, point))
            return slots[i].id;
    }
    return -1;
}

static void update_hover(int id)
{
    if (hovered == id)
        return;
    hide_tooltip();
    hovered = id;
    if (id >= 0)
        SetTimer(dock, TIMER_TOOLTIP, 550, NULL);
    InvalidateRect(dock, NULL, FALSE);
}

static BOOL open_target(const WCHAR *target)
{
    HINSTANCE result = ShellExecuteW(dock, L"open", target, NULL, NULL, SW_SHOWNORMAL);
    if ((INT_PTR)result > 32)
        return TRUE;
    MessageBoxW(dock, L"O Windows não conseguiu abrir este atalho.",
        L"Orla Dock", MB_OK | MB_ICONWARNING);
    return FALSE;
}

static void activate_task(const Task *task, BOOL toggle)
{
    DWORD process = 0;
    if (!GetWindowThreadProcessId(task->window, &process) || process != task->process)
        return;
    HWND current = GetForegroundWindow();
    if (toggle && (current == task->window || GetAncestor(current, GA_ROOTOWNER) == task->window)
        && !IsIconic(task->window)) {
        ShowWindowAsync(task->window, SW_MINIMIZE);
        return;
    }
    if (IsIconic(task->window))
        ShowWindowAsync(task->window, SW_RESTORE);
    HWND popup = GetLastActivePopup(task->window);
    HWND target = IsWindowVisible(popup) && IsWindowEnabled(popup) ? popup : task->window;
    SetForegroundWindow(target);
}

static void menu_label(const WCHAR *source, WCHAR *label)
{
    unsigned int n = 0;
    while (*source && n + 2 < 2 * TITLE_LENGTH) {
        WCHAR c = *source++;
        if (c == L'&')
            label[n++] = L'&';
        label[n++] = (c == L'\t' || c == L'\r' || c == L'\n') ? L' ' : c;
    }
    label[n] = L'\0';
}

static void append_windows(HMENU menu, const Task *snapshot, int count)
{
    if (!count) {
        AppendMenuW(menu, MF_STRING | MF_GRAYED, 0, L"Nenhuma janela aberta");
        return;
    }
    for (int i = 0; i < count && i < 40; i++) {
        WCHAR label[TITLE_LENGTH * 2];
        menu_label(snapshot[i].title, label);
        AppendMenuW(menu, MF_STRING, CMD_WINDOW_FIRST + i, label);
    }
    if (count > 40) {
        HMENU extra = CreatePopupMenu();
        if (!extra)
            return;
        for (int i = 40; i < count; i++) {
            WCHAR label[TITLE_LENGTH * 2];
            menu_label(snapshot[i].title, label);
            AppendMenuW(extra, MF_STRING, CMD_WINDOW_FIRST + i, label);
        }
        if (!AppendMenuW(menu, MF_POPUP, (UINT_PTR)extra, L"Mais janelas"))
            DestroyMenu(extra);
    }
}

static void schedule_refresh(void);

static void show_menu(BOOL windows_only, int x, int y)
{
    if (menu_open)
        return;
    HMENU menu = CreatePopupMenu();
    if (!menu)
        return;
    Task snapshot[MAX_TASKS];
    int count = task_count;
    CopyMemory(snapshot, tasks, sizeof(Task) * count);

    if (windows_only) {
        append_windows(menu, snapshot, count);
    } else {
        AppendMenuW(menu, MF_STRING, CMD_FILES, L"Explorador de Arquivos");
        HMENU windows = CreatePopupMenu();
        if (windows) {
            append_windows(windows, snapshot, count);
            if (!AppendMenuW(menu, MF_POPUP, (UINT_PTR)windows, L"Janelas abertas"))
                DestroyMenu(windows);
        }
        AppendMenuW(menu, MF_SEPARATOR, 0, NULL);
        HMENU colors = CreatePopupMenu();
        if (colors) {
            for (UINT i = 0; i < ARRAYSIZE(themes); i++)
                AppendMenuW(colors, MF_STRING | ((int)i == theme_index ? MF_CHECKED : 0),
                    CMD_THEME_FIRST + i, themes[i].name);
            if (!AppendMenuW(menu, MF_POPUP, (UINT_PTR)colors, L"Tema"))
                DestroyMenu(colors);
        }
        HMENU edges = CreatePopupMenu();
        if (edges) {
            AppendMenuW(edges, MF_STRING | (at_top ? MF_CHECKED : 0), CMD_TOP, L"Topo");
            AppendMenuW(edges, MF_STRING | (!at_top ? MF_CHECKED : 0), CMD_BOTTOM, L"Parte inferior");
            if (!AppendMenuW(menu, MF_POPUP, (UINT_PTR)edges, L"Posição"))
                DestroyMenu(edges);
        }
        AppendMenuW(menu, MF_STRING, CMD_TASKBAR, L"Configurações da barra do Windows");
        AppendMenuW(menu, MF_SEPARATOR, 0, NULL);
        AppendMenuW(menu, MF_STRING, CMD_EXIT, L"Fechar Orla Dock");
    }

    hide_tooltip();
    HWND previous = GetForegroundWindow();
    menu_open = TRUE;
    SetForegroundWindow(dock);
    UINT flags = TPM_RETURNCMD | TPM_NONOTIFY | TPM_RIGHTBUTTON;
    flags |= at_top ? TPM_TOPALIGN : TPM_BOTTOMALIGN;
    UINT command = (UINT)TrackPopupMenu(menu, flags, x, y, 0, dock, NULL);
    menu_open = FALSE;
    schedule_refresh();
    DestroyMenu(menu);
    PostMessageW(dock, WM_NULL, 0, 0);

    if (command >= CMD_WINDOW_FIRST && command - CMD_WINDOW_FIRST < (UINT)count) {
        activate_task(&snapshot[command - CMD_WINDOW_FIRST], FALSE);
        return;
    }
    if (previous && previous != dock && GetForegroundWindow() == dock)
        SetForegroundWindow(previous);
    if (command == CMD_EXIT) {
        DestroyWindow(dock);
    } else if (command == CMD_FILES) {
        open_target(L"explorer.exe");
    } else if (command == CMD_TASKBAR) {
        open_target(L"ms-settings:taskbar");
    } else if (command == CMD_TOP || command == CMD_BOTTOM) {
        at_top = command == CMD_TOP;
        save_preferences();
        place_dock();
    } else if (command >= CMD_THEME_FIRST && command - CMD_THEME_FIRST < ARRAYSIZE(themes)) {
        theme_index = (int)command - CMD_THEME_FIRST;
        save_preferences();
        InvalidateRect(dock, NULL, FALSE);
    }
}

static void open_slot(int id, POINT point)
{
    if (id >= SLOT_WINDOW && id - SLOT_WINDOW < task_count) {
        activate_task(&tasks[id - SLOT_WINDOW], TRUE);
        return;
    }
    if (id == SLOT_FILES) {
        open_target(L"explorer.exe");
        return;
    }
    if (id == SLOT_MENU || id == SLOT_MORE) {
        RECT rect;
        GetWindowRect(dock, &rect);
        ClientToScreen(dock, &point);
        show_menu(id == SLOT_MORE, point.x, at_top ? rect.bottom : rect.top);
    }
}

static void schedule_refresh(void)
{
    if (refresh_pending || !dock)
        return;
    refresh_deferred = FALSE;
    refresh_pending = SetTimer(dock, TIMER_REFRESH, 120, NULL) != 0;
}

static void schedule_clock(void)
{
    SYSTEMTIME now;
    GetLocalTime(&now);
    SetTimer(dock, TIMER_CLOCK, 60010 - now.wSecond * 1000 - now.wMilliseconds, NULL);
}

static void CALLBACK window_event(HWINEVENTHOOK hook, DWORD event, HWND window, LONG object,
    LONG child, DWORD thread, DWORD timestamp)
{
    (void)hook;
    (void)thread;
    (void)timestamp;
    if (!dock || !window || window == dock || window == tooltip ||
        object != OBJID_WINDOW || child != CHILDID_SELF)
        return;

    if (event == EVENT_SYSTEM_FOREGROUND || event == EVENT_SYSTEM_MINIMIZESTART ||
        event == EVENT_SYSTEM_MINIMIZEEND) {
        schedule_refresh();
        return;
    }
    if (event == EVENT_OBJECT_LOCATIONCHANGE) {
        if (window == GetForegroundWindow() && !menu_open)
            schedule_refresh();
        return;
    }
    if (event == EVENT_OBJECT_DESTROY || event == EVENT_OBJECT_HIDE ||
        event == EVENT_OBJECT_SHOW || event == EVENT_OBJECT_NAMECHANGE ||
        event == EVENT_OBJECT_CLOAKED || event == EVENT_OBJECT_UNCLOAKED) {
        if (!IsWindow(window) || GetAncestor(window, GA_ROOT) == window)
            schedule_refresh();
    }
}

static LRESULT CALLBACK dock_proc(HWND window, UINT message, WPARAM wparam, LPARAM lparam)
{
    switch (message) {
    case WM_PAINT: {
        PAINTSTRUCT paint;
        HDC dc = BeginPaint(window, &paint);
        RECT rect;
        GetClientRect(window, &rect);
        HDC buffer = CreateCompatibleDC(dc);
        HBITMAP bitmap = buffer ? CreateCompatibleBitmap(dc, rect.right, rect.bottom) : NULL;
        if (bitmap) {
            HGDIOBJ old = SelectObject(buffer, bitmap);
            draw_dock(buffer, rect);
            BitBlt(dc, 0, 0, rect.right, rect.bottom, buffer, 0, 0, SRCCOPY);
            SelectObject(buffer, old);
            DeleteObject(bitmap);
        } else {
            draw_dock(dc, rect);
        }
        if (buffer)
            DeleteDC(buffer);
        EndPaint(window, &paint);
        return 0;
    }
    case WM_ERASEBKGND:
        return 1;
    case WM_MOUSEACTIVATE:
        return MA_NOACTIVATE;
    case WM_MOUSEMOVE: {
        POINT point = {GET_X_LPARAM(lparam), GET_Y_LPARAM(lparam)};
        TRACKMOUSEEVENT track = {sizeof(track), TME_LEAVE, window, 0};
        TrackMouseEvent(&track);
        update_hover(hit_test(point));
        return 0;
    }
    case WM_MOUSELEAVE:
        update_hover(-1);
        return 0;
    case WM_LBUTTONDOWN: {
        POINT point = {GET_X_LPARAM(lparam), GET_Y_LPARAM(lparam)};
        pressed = hit_test(point);
        if (pressed >= 0) {
            hide_tooltip();
            SetCapture(window);
            InvalidateRect(window, NULL, FALSE);
        }
        return 0;
    }
    case WM_LBUTTONUP: {
        POINT point = {GET_X_LPARAM(lparam), GET_Y_LPARAM(lparam)};
        int id = hit_test(point);
        int down = pressed;
        pressed = -1;
        if (GetCapture() == window)
            ReleaseCapture();
        InvalidateRect(window, NULL, FALSE);
        if (refresh_deferred)
            schedule_refresh();
        if (id >= 0 && id == down)
            open_slot(id, point);
        return 0;
    }
    case WM_CAPTURECHANGED:
        pressed = -1;
        if (refresh_deferred)
            schedule_refresh();
        InvalidateRect(window, NULL, FALSE);
        return 0;
    case WM_CONTEXTMENU: {
        RECT rect;
        GetWindowRect(window, &rect);
        int x = GET_X_LPARAM(lparam);
        if (x == -1 && GET_Y_LPARAM(lparam) == -1)
            x = rect.left + scale(10);
        show_menu(FALSE, x, at_top ? rect.bottom : rect.top);
        return 0;
    }
    case WM_HOTKEY: {
        if (wparam == HOTKEY_MENU && IsWindowVisible(window)) {
            RECT rect;
            GetWindowRect(window, &rect);
            show_menu(FALSE, rect.left + scale(10), at_top ? rect.bottom : rect.top);
        }
        return 0;
    }
    case WM_TIMER:
        KillTimer(window, wparam);
        if (wparam == TIMER_REFRESH) {
            refresh_pending = FALSE;
            if (pressed < SLOT_WINDOW && !menu_open) {
                if (hovered >= SLOT_WINDOW)
                    update_hover(-1);
                refresh_tasks();
                place_dock();
            } else {
                refresh_deferred = TRUE;
            }
        } else if (wparam == TIMER_CLOCK) {
            if (IsWindowVisible(window))
                InvalidateRect(window, &clock_area, FALSE);
            schedule_clock();
        } else if (wparam == TIMER_TOOLTIP) {
            show_tooltip();
        }
        return 0;
    case WM_TIMECHANGE:
        schedule_clock();
        InvalidateRect(window, &clock_area, FALSE);
        return 0;
    case WM_DISPLAYCHANGE:
    case WM_SETTINGCHANGE:
    case WM_DPICHANGED:
        if (dock)
            schedule_refresh();
        return 0;
    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;
    }
    return DefWindowProcW(window, message, wparam, lparam);
}

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE previous, PWSTR arguments, int show)
{
    (void)previous;
    (void)arguments;
    (void)show;
    HANDLE singleton = CreateMutexW(NULL, FALSE, L"Local\\OrlaDock.Application");
    if (!singleton)
        return 1;
    if (GetLastError() == ERROR_ALREADY_EXISTS) {
        CloseHandle(singleton);
        return 0;
    }
    HRESULT com = CoInitializeEx(NULL, COINIT_APARTMENTTHREADED | COINIT_DISABLE_OLE1DDE);
    load_preferences();

    default_icon = LoadIconW(NULL, IDI_APPLICATION);
    HICON app_icon = LoadIconW(instance, MAKEINTRESOURCEW(1));
    WNDCLASSEXW klass = {
        .cbSize = sizeof(klass),
        .lpfnWndProc = dock_proc,
        .hInstance = instance,
        .hIcon = app_icon ? app_icon : default_icon,
        .hCursor = LoadCursorW(NULL, IDC_ARROW),
        .lpszClassName = CLASS_NAME,
    };
    if (RegisterClassExW(&klass)) {
        dock = CreateWindowExW(WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED,
            CLASS_NAME, L"Orla Dock", WS_POPUP, 0, 0, 1, 1,
            NULL, NULL, instance, NULL);
    }
    if (!dock) {
        MessageBoxW(NULL, L"Não foi possível iniciar o Orla Dock.", L"Orla Dock", MB_ICONERROR);
        if (SUCCEEDED(com))
            CoUninitialize();
        CloseHandle(singleton);
        return 1;
    }

    SetLayeredWindowAttributes(dock, 0, 246, LWA_ALPHA);
    INITCOMMONCONTROLSEX common = {sizeof(common), ICC_WIN95_CLASSES};
    InitCommonControlsEx(&common);
    tooltip = CreateWindowExW(WS_EX_TOPMOST | WS_EX_NOACTIVATE, TOOLTIPS_CLASSW, NULL,
        WS_POPUP | TTS_NOPREFIX, 0, 0, 0, 0, dock, NULL, instance, NULL);
    if (tooltip) {
        TOOLINFOW tool = {.cbSize = sizeof(tool), .uFlags = TTF_TRACK | TTF_ABSOLUTE,
            .hwnd = dock, .uId = 1, .lpszText = tooltip_text};
        SendMessageW(tooltip, TTM_ADDTOOLW, 0, (LPARAM)&tool);
        SendMessageW(tooltip, TTM_SETMAXTIPWIDTH, 0, 300);
    }

    RegisterHotKey(dock, HOTKEY_MENU, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, 'O');
    UINT flags = WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS;
    hooks[0] = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
        NULL, window_event, 0, 0, flags);
    hooks[1] = SetWinEventHook(EVENT_SYSTEM_MINIMIZESTART, EVENT_SYSTEM_MINIMIZEEND,
        NULL, window_event, 0, 0, flags);
    hooks[2] = SetWinEventHook(EVENT_OBJECT_DESTROY, EVENT_OBJECT_NAMECHANGE,
        NULL, window_event, 0, 0, flags);
    hooks[3] = SetWinEventHook(EVENT_OBJECT_CLOAKED, EVENT_OBJECT_UNCLOAKED,
        NULL, window_event, 0, 0, flags);

    refresh_tasks();
    place_dock();
    schedule_clock();

    MSG message;
    BOOL result;
    while ((result = GetMessageW(&message, NULL, 0, 0)) > 0) {
        TranslateMessage(&message);
        DispatchMessageW(&message);
    }
    for (UINT i = 0; i < ARRAYSIZE(hooks); i++) {
        if (hooks[i])
            UnhookWinEvent(hooks[i]);
    }
    UnregisterHotKey(dock, HOTKEY_MENU);
    for (int i = 0; i < task_count; i++) {
        if (tasks[i].icon)
            DestroyIcon(tasks[i].icon);
    }
    if (clock_font)
        DeleteObject(clock_font);
    if (detail_font)
        DeleteObject(detail_font);
    if (SUCCEEDED(com))
        CoUninitialize();
    CloseHandle(singleton);
    return result == -1 ? 1 : 0;
}
