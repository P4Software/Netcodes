# Limits and messages

Netcodes treats templates, data, pictures and ZPL as untrusted input: they may come from users or from another system.
Every size has a limit, so a bad or hostile label cannot hang a program or use up its memory. This page lists the limits and
explains the messages.

## How problems are reported

| Kind | What happens | Where you see it |
|---|---|---|
| **Warning** | The label is still produced. The affected part (a missing value, a barcode with bad data, a picture) is left out or printed empty. | `Warnings` on the result of `GenerateZpl` and `Preview` |
| **Refusal** | Nothing is produced, because the template or settings are unusable. | A `TemplateException`; its `Message` lists **every** problem at once |
| **Print result** | The request was checked or sent. | `PrintResult.Message` |

All messages say what happened and what to do next. Show them to your users as they are.

## Limits

| What | Limit | When it is exceeded |
|---|---|---|
| Fields on one label (generator and ZPL reader) | 5000 (every repeat row counts as one) | The rest are left out, with a warning |
| ZPL text read by the preview | 1,000,000 characters | Only the first million are used, with a warning |
| Label size the preview draws | 4000 x 4000 dots | The default size is used, with a warning |
| Text in one field (preview) | 5000 characters | The text is cut off, with a warning |
| Data in one barcode (preview) | 8000 characters | That barcode is left out, with a warning |
| Barcode work on one label | 200,000 characters of barcode data (a PDF417 counts ten times) | Further barcodes are left out, with one warning |
| `^GF` graphic | 2,000,000 bytes; at most 500 bytes (4000 dots) per row | That graphic is left out, with a warning |
| `^GF` graphics on one label | 8,000,000 bytes in total | The rest are skipped, with a warning |
| A picture (Image element) | 25 million source pixels; 7,000,000 characters of base64; printed size at most 4000 x 4000 dots and 2,000,000 bytes | That picture is left out, with a warning |
| Pictures on one label | 100 million source pixels, 20 million characters of picture text and 8,000,000 bytes of printed graphics in total | Further pictures are left out, with a warning |
| Repeat rows | `maxRows` (default 100), and as many as fit on the label | The rest are left out, with a warning |
| Copies | 1 to 10000 | Refused |
| Print speed | 1 to 14 | Refused |
| Print darkness | 0 to 30 | Refused |
| Label shift | -9999 to 9999 dots | Refused |
| Module width and QR size | 1 to 10 dots | Clamped into that range |
| Resolution | 152, 203, 300 or 600 dpi | Refused |

These are safety limits, far above what a real label needs. If you hit one, the message names it.

## Common warnings and what to do

| Message (starts with) | Meaning and what to do |
|---|---|
| *The field {{x}} has no value in your data. It will print empty.* | Your data has no value for `x`. Add it, or fix the placeholder's spelling. |
| *The list "x" was not found in your data, or is not a list, so no rows were printed.* | A `repeat` element's `source` does not point at a list in your data. |
| *Only N of M rows fit on the label; the rest were left out.* | More list items than fit. Make the rows closer together, the label longer, or the list shorter. |
| *A barcode has no data, so it was left out of the label.* | The barcode's data (or its placeholder) was empty. |
| *EAN-13 needs 12 or 13 digits...*, *Code 39 can only contain digits, capital letters...* | The data does not suit that barcode type. The barcode is left out; fix the data or choose another type. |
| *The EAN-13 number "x" has a wrong check digit; the last digit should be N.* | An EAN-13 or UPC-A with 13 or 12 digits whose last digit is wrong. Give one digit fewer and the check digit is added for you. |
| *The GS1 barcode was left out of the label. The GS1 field "x" must have N characters...* | A GS1 value has the wrong length. See the field table in the [template reference](template-reference.md#gs1). |
| *An image was left out of the label. The image data is not a picture...* | The picture data is not valid base64, or not a PNG, JPEG, GIF, BMP or WebP. |
| *An image was left out of the label. The label has too many images...* | The pictures on this label together exceed the budget. Use fewer or smaller pictures. |
| *The label has too much barcode data in total, so some barcodes were left out.* | Too many (or too large) barcodes on one label. |
| *The QR error correction "x" is not valid. Use L, M, Q or H. Q was used instead.* | Fix `errorCorrection`. |
| *The command ^XX is not supported yet, so it was skipped.* | The preview does not know this ZPL command. The rest of the label is drawn. See [ZPL support](zpl-support.md). |
| *The ZPL contains more than one label. Only the first one is previewed.* | Preview one label at a time. |
| *A text field is too long ... so it was cut off.* | A single text field passed the preview's 5000-character limit. |

## Refusals (`TemplateException`)

The message lists every problem so you can fix them in one go. For example, an empty template gives:

```
The template has no name. Give it a name such as "shipping-label".
The label size must be larger than zero. Set widthMm and heightMm in millimetres.
```

Typical causes: no `name`, a size of zero, a `dpi` that is not 152, 203, 300 or 600, an element with a negative position, a
rotation that is not 0, 90, 180 or 270, an element height of zero, an unknown `symbology`, an unknown `type`, JSON with a
missing comma or quote (the message gives the line), or a print setting outside its range.

## Print messages

| Message (starts with) | Meaning and what to do |
|---|---|
| *Dry run: the label was checked and would be sent to ...* | Nothing was printed. Pass `dryRun: false` to print. |
| *The label (N copy) was sent to ... The printer does not confirm...* | It was sent. If nothing comes out, check the printer (paper, ribbon, error light). |
| *The printer x did not answer on port 9100...* | The printer is off, on another network, or the address is wrong. |
| *The connection to x was lost while the label was being sent...* | The printer is busy or in an error state. Check it and try again. |
| *"x" is not a valid printer address...* | Use an IP address such as 192.168.1.50 or a host name such as zebra-dock1. |
| *The printer "x" has no address...* | Give the target a `Host`, or a `WindowsQueue`. |
| *That Windows printer name is not allowed...* | Use the printer's local name (not `\\server\printer`), or its IP address. |
| *There is nothing to print: the label is empty.* | Generate the label first. |
| *The number of copies must be between 1 and 10000.* | Fix `copies`. |
