# Template reference

A template is a JSON object. Names are written in camelCase. Comments and trailing commas are allowed in the JSON.

```json
{
  "name": "shipping-label",
  "widthMm": 100,
  "heightMm": 150,
  "dpi": 203,
  "elements": [ ... ]
}
```

| Setting | Meaning |
|---|---|
| `name` | Required. A name for the template, for example `shipping-label`. |
| `widthMm`, `heightMm` | Required. The label size in millimetres, larger than zero. |
| `dpi` | The printer's resolution: `152`, `203` (default), `300` or `600`. Must match the printer. |
| `elements` | The things on the label, drawn in order (later ones on top). |

Positions and sizes are in **millimetres**, measured from the label's top-left corner. Inside a `repeat` row they are measured
from the top-left of that row. The engine converts millimetres to printer dots using `dpi`.

## Settings every element has

| Setting | Meaning |
|---|---|
| `type` | Required. One of `text`, `barcode`, `qr`, `gs1`, `box`, `image`, `repeat`. |
| `x`, `y` | Position in millimetres from the left and top edge. Not negative. |
| `rotation` | `0` (default), `90`, `180` or `270` degrees. Text, barcodes and QR codes rotate; for a rotated element `x` and `y` still name its top-left corner on the label. |

## Placeholders

Any text value (text, barcode data, QR data, GS1 fields, picture data) can contain `{{placeholders}}`, filled from your
data:

- `{{name}}` reads `name` from the data. `{{customer.city}}` reads `city` inside `customer`.
- A number prints as written (`10482`), and `true` or `false` print as those words.
- A value that is missing, or `null`, prints empty and adds the warning *The field {{x}} has no value in your data. It will
  print empty.* A list or an object cannot be printed as one value and gives a warning too.
- Inside a [`repeat`](#repeat), `{{item.sku}}` reads a field of the current list item and `{{index}}` is its row number,
  starting at 1.

Text from your data is escaped for you: characters that have a meaning in ZPL (`^`, `~` and `_`) are written safely, so
a value cannot break out of its field.

## text

```json
{ "type": "text", "x": 8, "y": 14, "text": "{{name}}", "heightMm": 7 }
```

| Setting | Meaning |
|---|---|
| `text` | The text, with optional placeholders. |
| `heightMm` | Character height in millimetres, larger than zero (default 4). |
| `widthMm` | Character width in millimetres. Leave it out and the width follows the height. |
| `font` | The ZPL font, one letter or digit. Default `"0"`, the scalable font. `A` to `H` are the printer's fixed bitmap fonts. |
| `maxWidthMm` | Wrap the text inside this width. |
| `maxLines` | With `maxWidthMm`, the most lines to print (default 1). |

Text that does not fit in `maxLines` is printed on top of the last line, as a printer does. Give the box more lines or a
shorter text.

## barcode

```json
{ "type": "barcode", "symbology": "code128", "x": 8, "y": 50, "data": "{{tracking}}", "heightMm": 25, "moduleWidth": 3 }
```

| Setting | Meaning |
|---|---|
| `symbology` | The barcode type, see below. Default `code128`. |
| `data` | What to encode, with optional placeholders. A barcode with no data is left out with a warning. |
| `heightMm` | Bar height in millimetres, larger than zero (default 12). For `pdf417`, the height of the whole symbol. |
| `moduleWidth` | Width of the narrowest bar in printer dots, 1 to 10 (default 2). For `dataMatrix` and `aztec`, the size of one square. |
| `showText` | Print the human-readable text under the bars (default `true`). |

| `symbology` | Barcode | What the data may hold |
|---|---|---|
| `code128` | Code 128 | Standard keyboard characters (no accented letters). |
| `code39` | Code 39 | Digits, capital letters, and `- . space $ / + %`. |
| `code93` | Code 93 | Same characters as Code 39. |
| `codabar` | Codabar | Digits and `- $ : / . + `, optionally between a start and stop letter A to D (`A123456B`). Without them, A and A are used. |
| `msi` | MSI | Digits only. Printed without a check digit. |
| `ean13` | EAN-13 | 12 digits (the check digit is added) or 13 digits with a correct check digit. |
| `upcA` | UPC-A | 11 digits (the check digit is added) or 12 digits with a correct check digit. |
| `itf` | Interleaved 2 of 5 | Digits only. An odd number of digits gets a leading zero. |
| `dataMatrix` | Data Matrix | Text. |
| `aztec` | Aztec Code | Text. |
| `pdf417` | PDF417 | Text. The number of columns is chosen for you and written into the ZPL. |

Data that does not suit its barcode type (letters in an EAN-13, a wrong check digit) is not printed: the barcode is left
out and you get a warning that says why. The rest of the label is unaffected.

## qr

```json
{ "type": "qr", "x": 8, "y": 95, "data": "{{url}}", "size": 5, "errorCorrection": "M" }
```

| Setting | Meaning |
|---|---|
| `data` | What to encode. |
| `size` | Printer dots per QR square, 1 to 10 (default 4). |
| `errorCorrection` | `L`, `M`, `Q` (default) or `H`. Anything else uses `Q` and warns. |

## gs1

A GS1-128 barcode, the kind on pallet and logistics labels. You give the application identifiers by name; Netcodes builds
the `(00)...(01)...(10)...` text, adds check digits and prints the readable line.

```json
{
  "type": "gs1", "x": 6, "y": 58, "heightMm": 20, "moduleWidth": 2,
  "fields": { "gtin": "{{gtin}}", "expiry": "{{expiry}}", "lot": "{{lot}}" }
}
```

| Field name | Identifier | Length |
|---|---|---|
| `sscc` | (00) | 17 digits (check digit added) or 18 |
| `gtin` | (01) | 13 digits (check digit added) or 14 |
| `productionDate` | (11) | 6 digits, `YYMMDD` |
| `bestBefore` | (15) | 6 digits, `YYMMDD` |
| `expiry` | (17) | 6 digits, `YYMMDD` |
| `lot` | (10) | variable (the GS1 standard allows up to 20 characters; Netcodes does not check this) |
| `serial` | (21) | variable (the GS1 standard allows up to 20 characters; Netcodes does not check this) |
| `count` | (37) | digits |

`heightMm` (default 15) and `moduleWidth` (default 2) work as for a barcode. A value that has the wrong length leaves just
that barcode out, with a warning that names the field. A GS1 barcode with many fields is wide: it must fit inside the label
(use a smaller `moduleWidth`, a wider label, or split it into two barcodes, as the pallet example does).

## box

A rectangle outline, a filled block, or (when thin) a line.

```json
{ "type": "box", "x": 3, "y": 3, "widthMm": 94, "heightMm": 144, "borderMm": 0.6 }
{ "type": "box", "x": 6, "y": 23, "widthMm": 88, "heightMm": 0.4, "filled": true }
```

| Setting | Meaning |
|---|---|
| `widthMm`, `heightMm` | Size in millimetres, both larger than zero. |
| `borderMm` | Line thickness (default 0.4). |
| `filled` | `true` makes a solid block. A very thin filled box is a line. |

## image

A picture, such as a logo, printed as black and white dots.

```json
{ "type": "image", "x": 6, "y": 6, "data": "{{logo}}", "widthMm": 30, "threshold": 128 }
```

| Setting | Meaning |
|---|---|
| `data` | The picture as **base64 text** or a `data:image/png;base64,...` address, usually through a placeholder. |
| `widthMm` | Printed width in millimetres, larger than zero. Required. |
| `heightMm` | Printed height. Leave it out to keep the picture's shape. |
| `threshold` | Brightness from 0 to 255 below which a dot is black (default 128). Raise it for a darker print. |
| `invert` | `true` swaps black and white. |

PNG, JPEG, GIF, BMP and WebP are accepted. Transparent areas print white. Pictures are **not** read from file paths or web
addresses (a path in untrusted data would read files from your server, and an address would make it call out), so put the
picture in your data. Scale the picture to about the size you print it at; a very large source is slow and may be refused
(see [limits](limits-and-messages.md#limits)).

## repeat

Prints its child elements once for every item of a list in your data: packing-slip lines, order items.

```json
{
  "type": "repeat", "source": "lines", "x": 6, "y": 26, "rowHeightMm": 7, "maxRows": 15,
  "elements": [
    { "type": "text", "x": 0,  "y": 0, "text": "{{index}}.", "heightMm": 3.5 },
    { "type": "text", "x": 8,  "y": 0, "text": "{{item.sku}}", "heightMm": 3.5 },
    { "type": "text", "x": 78, "y": 0, "text": "x{{item.qty}}", "heightMm": 3.5 }
  ]
}
```

| Setting | Meaning |
|---|---|
| `source` | The dotted path of the list in your data, for example `lines`. |
| `rowHeightMm` | The distance between rows, larger than zero. |
| `maxRows` | The most rows to print (default 100). |
| `elements` | The elements of one row, positioned relative to the row's top-left corner. Repeats can be nested. |

If the rows would run off the bottom of the label, printing stops at the last row that fits and you get a warning that says
how many fit. If the list is missing, no rows print and you get a warning.

## Reading and writing templates in code

```csharp
var template = LabelTemplate.FromJson(json);        // throws TemplateException with a plain message if it is broken
var problems = template.Validate();                  // every problem, in plain language (empty when valid)
var json2 = template.ToJson();                       // indented JSON
```

You can also build a template in code with the types in `Netcodes.Template` (`TextElement`, `BarcodeElement`, `QrElement`,
`Gs1Element`, `BoxElement`, `ImageElement`, `RepeatElement`) and call `engine.GenerateZpl(template, dataJson)`.
