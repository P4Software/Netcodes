# Printing

Netcodes sends ZPL to a printer over the network (raw port 9100) or through a Windows printer queue. Printing is **safe by
default**: nothing reaches a printer until you ask for it.

## Dry run first

```csharp
var engine = new NetcodesEngine();
var printer = new PrinterTarget("Dock 1", Host: "192.168.1.50");

var check = await engine.PrintAsync(printer, zpl, copies: 2);        // dry run
Console.WriteLine(check.Message);
// Dry run: the label was checked and would be sent to Dock 1 (2 copies). Nothing was printed. To really print, set dryRun to false.

var sent = await engine.PrintAsync(printer, zpl, copies: 2, dryRun: false);   // really print
Console.WriteLine(sent.Message);
```

`PrintAsync` returns a `PrintResult`:

| Property | Meaning |
|---|---|
| `Success` | `true` when the request was accepted (for a dry run: when it passed all the checks). |
| `DryRun` | `true` when nothing was sent. |
| `Message` | What happened and what to do next, in plain language. Show it to the person using your program. |

The dry run runs the same checks as a real print, so a request a real print would refuse is refused by the dry run too.

## Choosing the printer: `PrinterTarget`

```csharp
new PrinterTarget("Dock 1", Host: "192.168.1.50")                  // network printer, port 9100
new PrinterTarget("Dock 1", Host: "zebra-dock1", Port: 9100)        // by host name
new PrinterTarget("Office", WindowsQueue: "ZDesigner ZD621-300dpi ZPL")   // a printer installed in Windows
```

| Setting | Meaning |
|---|---|
| `Name` | A friendly name used in messages. |
| `Host`, `Port` | A network printer's IP address or host name, and its TCP port (Zebra printers listen for raw ZPL on `9100`). |
| `WindowsQueue` | The printer's name in Windows, used when there is no `Host`. |

To list the printers installed in Windows: `PrinterDiscovery.WindowsQueues()` (empty on other systems).

### Network printers

The label is sent over TCP and the printer prints it. A Zebra printer does not reply on port 9100, so "sent" is the most
that can be known: *The printer does not confirm, so check the printer if nothing comes out.* If the printer is off or on
another network you get *The printer 192.168.1.50 did not answer on port 9100. Check that it is on and on the same network,
and that the address is right.* If it accepts the connection and then drops it (busy, paper out, error state) you get a
message saying the connection was lost; check the printer for an error.

### Windows printer queues

Use the printer's local name as shown in Windows under Settings > Printers. The label is sent as raw ZPL, so the printer
driver does not reformat it. Names containing `\`, `/`, a comma or a control character are refused, because a network share
name (`\\server\printer`) would make Windows connect to another machine and send the user's credentials to it. Use the
printer's IP address, or its local queue name.

## Copies

`copies` is 1 to 10000. Netcodes writes the quantity into the ZPL (`^PQ`) so the printer repeats the label itself. You can
also set `Quantity` in the generation options (below).

## Printer settings

Nothing here is written to the ZPL unless you ask, so a plain label never changes how the printer is set up.

```csharp
var zpl = engine.GenerateZpl(template, data, new GenerateOptions
{
    PrintSpeed = 4,                          // 1 to 14   (^PR)
    Darkness = 20,                           // 0 to 30   (^MD, relative to the printer's own setting)
    Media = PrinterMedia.Thermal,            // Thermal (ribbon) or Direct (heat-sensitive paper)   (^MT)
    Quantity = 3,                            // 1 to 10000   (^PQ)
    LabelShiftDots = -12,                    // move everything left (-) or right (+), -9999 to 9999   (^LS)
    Mode = PrintMode.PeelOff,                // TearOff, PeelOff, Rewind, Applicator, Cutter, DelayedCut   (^MM)
    Tracking = MediaTracking.Web,            // Continuous, Web (gap), Mark (black mark), Auto   (^MN)
    Inverted = false,                        // true prints the label turned 180 degrees   (^PO)
});
```

Out-of-range values are refused with a message that says the allowed range.

`ConfigUpdate` can save or recall the printer's stored settings (`^JU`):

- `ConfigUpdate.Save` writes the current settings into the printer's permanent memory. That memory wears out after many
  writes, so use it for a setup label, not on every label.
- `ConfigUpdate.Recall` loads the last saved settings. It is written first, because it overrides everything after it.
- There is deliberately no factory reset.

The preview ignores these printer settings (they change how the printer behaves, not what is on the label).

## Match the label size and resolution

The printer prints what the ZPL says. A template is written for one label size and one resolution (`dpi`). If the template
says 203 dpi and the printer is 300 dpi, everything prints smaller than you drew it. You can read a Zebra's resolution from
its configuration label, or from the printer's settings.

## If you build a service on top of this

The library checks that a printer address is well formed (a valid IP address or host name, a port from 1 to 65535, an allowed
Windows queue name). It does **not** decide which printers a caller may reach. If your service lets people name a printer,
keep your own list of printers and let them choose from it, never an address they typed: otherwise anyone who can call your
service can make it open connections to any machine your server can reach.
