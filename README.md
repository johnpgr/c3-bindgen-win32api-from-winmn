# c3-bindgen-win32api-from-winmn — C3 Win32 Bindings Generator

`c3-bindgen-win32api-from-winmn` is a .NET utility designed to automatically generate C3 language bindings (`.c3i` interface files) from Microsoft's Win32 Metadata (`Windows.Win32.winmd` ECMA-335 assemblies).

---

## Table of Contents
1. [Why this Generator? (C3 Interop & Casing Rules)](#why-this-generator-c3-interop--casing-rules)
2. [Prerequisites](#prerequisites)
3. [How to Run & Generate Bindings](#how-to-run--generate-bindings)
4. [Using the JSON Subset Configuration](#using-the-json-subset-configuration)
5. [Compiling & Using the Bindings in C3](#compiling--using-the-bindings-in-c3)
6. [Example GUI Application](#example-gui-application)

---

## Why this Generator? (C3 Interop & Casing Rules)

In C3, naming rules are enforced directly by the lexer and parser. Unlike Odin or Zig, which can import C headers and preserve their exact casing (e.g. `HWND` or `RegisterClassExW`), C3 requires:
* **Types** to start with an uppercase letter and contain at least one lowercase character (e.g., `HWND -> HWnd`, `HINSTANCE -> HInstance`, `WNDCLASSEXW -> WndClassExW`).
* **Functions, variables, and parameters** to start with a lowercase letter (e.g., `RegisterClassExW -> registerClassExW`, `GetModuleHandleW -> getModuleHandleW`).

To resolve this naming mismatch without losing the correct ABI alignment, `c3-bindgen-win32api-from-winmn` acts as a name projector. It maps Win32 structures to valid C3 identifiers while preserving the original linker symbols using C3's `@cname` attribute:

```c3
// Win32 original: RegisterClassExW
extern fn Atom registerClassExW(WndClassExW* lpwcx)
    @cname("RegisterClassExW");
```

It also maps Win32 parameter direction metadata (`[In]`, `[Out]`) to C3 pointer contracts:
```c3
// Win32 original: GetWindowRect
<*
 @param [out] lpRect
*>
extern fn Bool getWindowRect(HWnd hWnd, Rect* lpRect)
    @cname("GetWindowRect");
```

---

## Prerequisites

* [.NET SDK 8.0+](https://dotnet.microsoft.com/download)
* [C3 Compiler (`c3c`)](https://c3-lang.org/) (for compiling C3 applications)

---

## How to Run & Generate Bindings

### 1. Run Self-Tests
Verify your environment and dependencies by running the built-in self-tests:
```powershell
dotnet run -- --self-test
```

### 2. Generate C3 Bindings
Generate bindings by specifying the path to the `.winmd` file, your JSON subset specification, and the output path:
```powershell
dotnet run -- --winmd data/Windows.Win32.winmd --subset data/win32.json --out out/win32.c3i --db out/bindgen-runs.sqlite
```

#### CLI Parameters:
* `--winmd <path>`: (Required) Path to Microsoft's `Windows.Win32.winmd` file.
* `--subset <path>`: Path to a JSON subset definition (defaults to `data/window-subset.json`).
* `--out <path>`: Path to the generated C3 interface output file (defaults to `out/win32.c3i`).
* `--db <path>`: Optional path to write a SQLite runs metadata database logging generation runs.
* `--dump-json <path>`: Optional path to write the raw, un-subsetted api metadata as JSON.
* `--self-test`: Runs the project's internal unit tests.
* `-h` / `--help`: Shows command usage help.

---

## Using the JSON Subset Configuration

Dumping all of the Windows API produces a massive file. Instead, `c3-bindgen-win32api-from-winmn` takes a subset configuration file (like `data/win32.json`) to recursively resolve only the types, constants, and functions you actually need for your project.

### Example JSON Subset (`data/win32.json`):
```json
{
  "module": "win32",
  "namespaces": [
    "Windows.Win32.Foundation",
    "Windows.Win32.System.LibraryLoader",
    "Windows.Win32.UI.WindowsAndMessaging",
    "Windows.Win32.Graphics.Gdi"
  ],
  "includeNamespaces": [
    "Windows.Win32.Foundation",
    "Windows.Win32.System.LibraryLoader",
    "Windows.Win32.UI.WindowsAndMessaging",
    "Windows.Win32.Graphics.Gdi",
    "Windows.Win32.Graphics.OpenGL"
  ],
  "includeImportModules": [
    "USER32.dll",
    "GDI32.dll",
    "OPENGL32.dll"
  ],
  "includeConstantsMatching": [
    "WM_*",
    "WS_*"
  ],
  "functions": [
    "GetModuleHandleW",
    "RegisterClassExW",
    "CreateWindowExW",
    "DefWindowProcW",
    "ShowWindow",
    "UpdateWindow",
    "GetMessageW",
    "TranslateMessage",
    "DispatchMessageW",
    "PostQuitMessage"
  ],
  "types": [
    "WNDCLASSEXW",
    "MSG"
  ],
  "typeNameOverrides": {
    "HWND": "HWnd",
    "HDC": "Hdc",
    "WNDCLASSEXW": "WndClassExW",
    "MSG": "Msg"
  }
}
```

### Key Configuration Fields:
1. **`module`**: The name of the C3 module header emitted at the top of the generated file (e.g. `module win32;`).
2. **`namespaces` / `includeNamespaces`**: The Win32 namespaces scanned in the metadata.
3. **`includeImportModules`**: DLL modules whose exported functions are allowed.
4. **`includeConstantsMatching`**: Glob patterns (e.g., `WM_*`) to automatically include matching constants.
5. **`functions` / `types` / `constants`**: Explicit lists of APIs to include. 
6. **`typeNameOverrides`**: Custom dictionary to define the C3-compliant PascalCase name for Win32 types (highly recommended to override default title-casing).

### Dependency Resolution
You only need to list the main functions or structs you use. The generator's `DependencyResolver` automatically traverses parameters, struct fields, and return types, pulling in transitive dependencies recursively (e.g. including `RegisterClassExW` will automatically resolve and pull in `WndClassExW`, `WndProc`, `HINSTANCE`, etc.).

---

## Compiling & Using the Bindings in C3

The generated `.c3i` file acts as a C3 interface file. You import it using C3's module name defined in your subset JSON:

```c3
import win32;
```

Compile your C3 files alongside the generated interface file using the C3 compiler:
```powershell
c3c compile -o my_app out/win32.c3i my_app.c3
```

---

## Example GUI Application

A full OpenGL window-creation example is available in the `examples/` directory.

### Build and Run the Example:
Navigate to the `examples/` directory and execute the build batch script:
```powershell
cd examples
.\build.bat
```

The example demonstrates:
1. Allocating and initializing the `WndClassExW` struct in C3.
2. Registering the window class with `win32::registerClassExW`.
3. Creating a GUI window with OpenGL double-buffering context setup.
4. Processing the standard Win32 message loop using `win32::peekMessageW`.
