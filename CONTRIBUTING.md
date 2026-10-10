# Contributing

Thank you for helping. Netcodes is small on purpose, so a few rules keep it that way.

## Build and test

You need the .NET 10 SDK.

```
dotnet build -warnaserror
dotnet test --filter "Category!=Labelary"
```

The tests tagged `Labelary` compare the preview with Labelary's output and need internet:
`dotnet test --filter "Category=Labelary"`. They write side-by-side images to `LabelaryDiff/` next to the test binaries.

## How we work

- **Test first.** Write the test, watch it fail for the right reason, then make it pass. A bug fix starts with a test that
  reproduces the bug.
- **Document as you write.** Every public type and member has an XML doc comment (a missing one fails the build). Comments
  say *why*, in English.
- **Plain-language messages.** Anything a person can see says what happened and what to do next. No exception names, no
  jargon.
- **Bad input never crashes.** Anything that can come from outside (ZPL, template JSON, data, pictures, printer names) must
  end in a warning or a `TemplateException`, never a crash or a hang.
- **Every cap needs a total.** A limit on one barcode, picture or graphic is not enough: a label can hold thousands of
  them. Give each kind of work a budget for the whole label, and add a test that fails without it.
- **Safe by default.** Anything that changes the outside world (printing) is a dry run unless the caller asks for more.
- **Examples are tested.** The guides' examples live in `docs/guide/examples` and are run by the test suite. If you change
  behaviour the guides describe, change the guide in the same pull request.
- **Few dependencies.** Please talk to us before adding a package.

## Pull requests

Keep a pull request to one change. Say what it changes and why, and what you ran. Make sure the build and the tests pass.

## Licence of your contribution

Netcodes is licensed under the [Apache License 2.0](LICENSE). By sending a contribution you agree that it is licensed under
the same terms (section 5 of that licence).
