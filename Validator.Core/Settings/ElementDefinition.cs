using System.Xml;

namespace P21.Validator.Core.Settings;

public sealed class ElementDefinition : Definition
{
    public ElementDefinition(Target target, XmlElement element)
        : this(target, element, element.GetAttribute("Name"), string.Empty)
    {
    }

    public ElementDefinition(Target target, XmlElement element, string name)
        : this(target, element, name, string.Empty)
    {
    }

    public ElementDefinition(Target target, XmlElement element, string name, string prefix)
        : base(target, name, prefix)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        foreach (XmlAttribute attribute in element.Attributes)
        {
            SetProperty(attribute.LocalName, attribute.Value);
        }
    }
}
