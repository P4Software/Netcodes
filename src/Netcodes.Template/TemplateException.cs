namespace Netcodes.Template;

/// <summary>
/// Raised when a template or its data cannot be read or used. The message is written for the
/// person using the product (what happened and what to do), never for a developer log.
/// </summary>
public sealed class TemplateException : Exception
{
    /// <summary>Creates the exception with a plain-language message.</summary>
    /// <param name="message">What happened and what to do next.</param>
    /// <param name="inner">The underlying error, kept for diagnostics only.</param>
    public TemplateException(string message, Exception? inner = null) : base(message, inner) { }
}
