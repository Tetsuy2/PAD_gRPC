using System.Xml;
using System.Xml.Schema;
using Broker.Services.Interfaces;

namespace Broker.Services
{
    public class XmlValidator : IXmlValidator
    {
        public bool Validate(string xml, string? xsdPath = null)
        {
            if (string.IsNullOrWhiteSpace(xml)) throw new ArgumentException("Empty XML.");

            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit };
            if (!string.IsNullOrWhiteSpace(xsdPath) && File.Exists(xsdPath))
            {
                settings.ValidationType = ValidationType.Schema;
                settings.Schemas.Add(null, xsdPath);
                settings.ValidationEventHandler += (s, e) =>
                {
                    if (e.Severity == XmlSeverityType.Error) throw new XmlSchemaValidationException(e.Message);
                };
            }

            using var reader = XmlReader.Create(new StringReader(xml), settings);
            while (reader.Read()) { /* forward-only (SAX-like) */ }
            return true;
        }
    }
}
