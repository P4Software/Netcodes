# Your first label: a step-by-step guide for beginners

No experience needed. You will make a shipping label, see it on screen and (if you have a Zebra printer) print it.
It takes about 10 minutes. Every step says what you should see, so you always know you are on track.

> **What is ZPL?** The language Zebra label printers speak. You never have to write it: Netcodes writes it for you.

## Step 1: Install the tools (once)

You need two free things:

1. **The .NET 10 SDK.** Download it from <https://dotnet.microsoft.com/download> and run the installer.
   (Or, in a Windows terminal: `winget install Microsoft.DotNet.SDK.10`.)
2. **Git**, to download Netcodes: <https://git-scm.com/downloads>.

To check it worked, open a terminal (on Windows press the Windows key, type `terminal`, press Enter) and type:

```
dotnet --version
```

You should see a number starting with `10`. If you see "not recognized", close the terminal, open a new one and try again.

## Step 2: Download Netcodes

In the terminal:

```
cd %USERPROFILE%\Documents
git clone https://github.com/P4Software/Netcodes.git
```

You should now have a folder `Documents\Netcodes`.

## Step 3: Make your own small program

```
mkdir MyFirstLabel
cd MyFirstLabel
dotnet new console
dotnet add reference ..\Netcodes\src\Netcodes.Core\Netcodes.Core.csproj
```

You should see "The project template was created successfully" and then "Reference ... added to the project".

## Step 4: Describe your label

Create a file called `hello.template.json` in the `MyFirstLabel` folder (Notepad is fine) and paste this in:

```json
{
  "name": "hello",
  "widthMm": 100,
  "heightMm": 50,
  "dpi": 203,
  "elements": [
    { "type": "text", "x": 6, "y": 6, "text": "Hello, {{name}}", "heightMm": 8 },
    { "type": "barcode", "symbology": "code128", "x": 6, "y": 22, "data": "{{code}}", "heightMm": 15, "moduleWidth": 3 }
  ]
}
```

This says: a label 100 mm wide and 50 mm tall, with a line of text and a barcode. The `{{name}}` and `{{code}}` are
blanks that your data fills in. Positions (`x`, `y`) are in millimetres from the top-left corner.

Now create `hello.data.json` next to it:

```json
{ "name": "Ana", "code": "HELLO-0001" }
```

## Step 5: Write the program

Open `Program.cs` (it was created for you), delete everything in it and paste this in:

```csharp
using Netcodes;
using Netcodes.Template;

// 1. Read the template and the data you just wrote.
var template = LabelTemplate.FromJson(File.ReadAllText("hello.template.json"));
var data = File.ReadAllText("hello.data.json");

var engine = new NetcodesEngine();

// 2. Turn them into ZPL (the printer's language). Problems come back as warnings, never crashes.
var zpl = engine.GenerateZpl(template, data);
foreach (var warning in zpl.Warnings) Console.WriteLine(warning);

// 3. Draw a picture of the label, so you can see it before wasting a real label.
File.WriteAllBytes("label.png", engine.Preview(zpl.Zpl).Png);

Console.WriteLine("Done! Open label.png in the folder to see your label.");
```

## Step 6: Run it

```
dotnet run
```

You should see `Done! Open label.png in the folder to see your label.` Open `label.png`. You have a label: the words
"Hello, Ana" and a barcode.

Change `Ana` in `hello.data.json`, run again and watch the label change. That is the whole idea: one template, many labels.

## Step 7 (optional): Print it

You need a Zebra printer on your network. Find its IP address (the printer's configuration label shows it; hold the
feed button for a few seconds). Add these lines to the end of `Program.cs`, and put your own address in:

```csharp
using Netcodes.Printing;

var printer = new PrinterTarget("My Zebra", Host: "192.168.1.50");   // <- your printer's IP address

// This is a DRY RUN: it checks everything and tells you what would happen, but prints nothing.
var check = await engine.PrintAsync(printer, zpl.Zpl, copies: 1);
Console.WriteLine(check.Message);
```

(Put the `using` line at the top of the file with the other `using` lines.) Run again. You should see a message saying
what would be printed. Nothing comes out of the printer yet, on purpose: Netcodes never prints unless you say so.

To print for real, add `dryRun: false`:

```csharp
await engine.PrintAsync(printer, zpl.Zpl, copies: 1, dryRun: false);
```

Your label comes out of the printer.

## If something goes wrong

| What you see | What to do |
|---|---|
| `dotnet` is not recognized | Close the terminal, open a new one. If it still fails, run the .NET installer again. |
| `Could not find a part of the path ...Netcodes.Core.csproj` | Step 3's path is wrong. Make sure `Netcodes` and `MyFirstLabel` sit side by side in the same folder. |
| A warning about a missing value | A `{{name}}` in the template has no matching key in the data file. Check the spelling. |
| The label is tiny or huge when printed | `dpi` in the template must match your printer (203 or 300). |
| The printer does not react | Check the IP address, that the printer is on the same network and that port 9100 is not blocked. |

## What next

- [Template reference](template-reference.md): every element (QR codes, pictures, lines, repeated rows) and setting.
- [Getting started](getting-started.md): the same ideas in more depth, plus the example gallery.
- [Printing](printing.md): printers, copies, Windows print queues and safety.
