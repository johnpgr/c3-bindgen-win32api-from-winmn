module generated_win32_app;

import win32;

bool running = true;

fn LResult exampleWindowProc(HWnd hWnd, uint message, WParam wParam, LParam lParam) {
    if (message == win32::WM_DESTROY) {
        running = false;
        win32::postQuitMessage(0);
        return 0;
    }

    return win32::defWindowProcW(hWnd, message, wParam, lParam);
}

fn void main() {
    WString className = @wstring("C3Win32Example");
    HInstance instance = (HInstance)win32::getModuleHandleW(null);

    WndClassExW windowClass;
    windowClass.cbSize = (uint)@sizeof(windowClass);
    windowClass.style = win32::CS_HREDRAW | win32::CS_VREDRAW;
    windowClass.lpfnWndProc = &exampleWindowProc;
    windowClass.cbClsExtra = 0;
    windowClass.cbWndExtra = 0;
    windowClass.hInstance = instance;
    windowClass.hIcon = null;
    windowClass.hCursor = null;
    windowClass.hbrBackground = null;
    windowClass.lpszMenuName = null;
    windowClass.lpszClassName = &className[0];
    windowClass.hIconSm = null;

    win32::registerClassExW(&windowClass);

    HWnd window = win32::createWindowExW(
        0,
        &className[0],
        &className[0],
        win32::WS_OVERLAPPEDWINDOW | win32::WS_VISIBLE,
        win32::CW_USEDEFAULT,
        win32::CW_USEDEFAULT,
        640,
        480,
        null,
        null,
        instance,
        null
    );
    
    Msg message;
    while (running) {
        Bool messageResult = win32::getMessageW(&message, null, 0, 0);
        if (messageResult < 0) {
            continue;
        }
        win32::translateMessage(&message);
        win32::dispatchMessageW(&message);
    }

}
