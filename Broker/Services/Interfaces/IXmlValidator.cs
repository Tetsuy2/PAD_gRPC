namespace Broker.Services.Interfaces
{
    public interface IXmlValidator
    {
        // Throws on invalid XML/XSD; returns true on valid.
        bool Validate(string xml, string? xsdPath = null);
    }
}
