# ZPL support

ZPL is Zebra's printer language. Netcodes **writes** ZPL from your templates, and its preview **reads** ZPL (yours or
generated) and draws it as a PNG. This page lists what the preview understands.

## What the preview draws

| Group | Commands |
|---|---|
| Label setup | `^XA` `^XZ` `^PW` `^LL` `^LH` |
| Position | `^FO` (field origin: top-left) `^FT` (field typeset: where the field starts, on its baseline) `^FW` |
| Text | `^FD` `^FS` `^A` `^CF` `^FB` (wrapping blocks) `^FH` (hex escapes) `^FR` (reverse) |
| Shapes | `^GB` (box, line) `^GC` (circle) |
| Graphics | `^GF` with ASCII hex data (including Zebra's run-length shorthand) |
| Barcode defaults | `^BY` (narrow bar width, wide-to-narrow ratio, height) |
| Barcodes | `^BC` Code 128 and GS1-128, `^B3` Code 39, `^BA` Code 93, `^BK` Codabar, `^BM` MSI, `^BE` EAN-13, `^BU` UPC-A, `^B2` Interleaved 2 of 5, `^BQ` QR, `^BX` Data Matrix, `^BO` Aztec, `^B7` PDF417 |

Commands that change how the printer behaves rather than what is drawn are read and **ignored without a warning**:
`^CI` `^PR` `^MD` `^MM` `^MN` `^MT` `^PQ` `^PO` `^JU` `^LS` `^LT` `^PM` `^MU`.

## Not supported yet

Anything else is skipped with a warning, never an error, and the rest of the label is still drawn. For example:
*The command ^DF is not supported yet, so it was skipped.* In particular:

- Stored formats and variable fields (`~DG`, `^DF`, `^XF`, `^FN`).
- RFID commands, and printer set-up commands other than those above.
- Barcodes: Code 11, MaxiCode, GS1 DataBar. (Netcodes cannot draw them; no barcode library it uses supports them.)
- `^GF` data in the binary or compressed-with-Z64 formats. Only ASCII hex (`^GFA`) is read.
- Rounded corners on `^GB` boxes (parsed, drawn square).
- EPL and CPCL (other Zebra languages).

If the ZPL holds several labels (`^XA ... ^XZ ^XA ... ^XZ`), only the first is previewed, and you get a warning.

## How close is the preview to a real printer?

- **Barcodes, boxes, circles and graphics** are drawn dot for dot. Compared with Labelary, QR codes, Data Matrix, Aztec,
  EAN-13, UPC-A, Interleaved 2 of 5, Code 93 and graphics match exactly, and Code 128, Code 39, Codabar, MSI and PDF417
  match in width and height.
- **Text** uses open-licence fonts that come with Netcodes (DejaVu Sans and Sans Mono), because a printer's built-in fonts
  cannot be redistributed. Letters have the right size and position but not the printer's exact shapes, so a line of text
  can be a little wider or narrower than on paper.
- **Interpretation lines** (the readable text under a barcode) differ in size and font from some printers' output. The bars
  are what counts.
- Measured over the examples in this repository, the preview differs from Labelary's in about 0.5 to 4.5 percent of the pixels,
  almost all of it font shapes.

The preview is for checking layout. For anything that must be exact, print a test label.

## Position rules worth knowing

- `^FO` puts a field's top-left corner at the point. `^FT` puts the **start** of the field on its baseline at the point, and
  the field then runs in its own direction: to the right for normal text, down for rotated 90, to the left for rotated 180,
  and up for rotated 270. A barcode's readable line sits on the outer side of the bars, outside the field's box.
- Text that does not fit its `^FB` block is printed on top of the last line, as a printer does, instead of being cut.
- `^FR` reverses a field: whatever is underneath is flipped.

## Barcode shapes

- **Wide-to-narrow ratio.** `^BY` sets it (2.0 to 3.0, default 3). Code 39, Codabar and MSI use it: at the default, a wide bar
  is three times a narrow one.
- **PDF417.** With no column count in `^B7`, the preview uses the fewest columns that keep the symbol within about 20 rows.
  Netcodes writes the column count it chose into the ZPL it generates, so the printed symbol has the same shape. The security
  level in `^B7` is the PDF417 error-correction level (0 to 8).
- **Code 128** is written in automatic mode, so a `>` in your data is printed as a `>` and not read as a printer command.
