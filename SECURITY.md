# Security

## Reporting a vulnerability

Please **do not open a public issue** for a security problem. Use GitHub's private reporting instead: open the
**Security** tab of this repository and choose **Report a vulnerability**. Say what you found, how to reproduce it, and
what it lets an attacker do. We will answer, and agree a fix and a time to disclose it with you.

## What counts

Netcodes treats the following as untrusted input, because they may come from users or from another system:

- ZPL text given to the preview or to a printer
- label template JSON and the data that fills it
- pictures (base64) given to the Image element
- printer names and addresses

A problem in how any of these is handled is in scope. Examples: input that makes the library hang, use unbounded memory
or throw an unexpected error (rather than a warning or a `TemplateException`), ZPL injection through a data value,
reading a file or contacting an address the caller did not choose, or a printer name that makes Windows connect somewhere
else.

The size limits in [Limits and messages](docs/guide/limits-and-messages.md) exist for this reason. A label that goes past
them and is not stopped with a warning is a bug worth reporting.

## What is up to you

Netcodes only checks that a printer address is well formed. If you build a service on it, decide yourself which printers a
caller may reach (a list you keep), and limit the size of the requests you accept.

## Supported versions

The latest release. The project is pre-1.0, so fixes go into the next release rather than being back-ported.
