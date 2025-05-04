using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace ModlinksShaVerifier;

public static class SerializationConstants
{
    public const string Namespace = "https://github.com/HollowKnight-Modding/HollowKnight.ModLinks/HollowKnight.ModManager";
}

[Serializable]
[XmlRoot(Namespace = SerializationConstants.Namespace)]
public record Manifest
{
    // Internally handle the Link/Links either-or divide
    private Links? _links;
    private Link? _link;

    // The name is required on all manifests *except* for ApiLinks, so default to it.
    public string Name { get; set; } = "ApiLinks";

    [XmlElement]
    public Link? Link
    {
        get => throw new NotImplementedException("This is only for XML Serialization!");
        set => _link = value;
    }

    public Links Links
    {
        get =>
            _links ??= new Links
            {
                Windows = _link ?? throw new InvalidDataException(nameof(_link))
            };
        set => _links = value;
    }

    public override string ToString()
    {
        return "{\n"
               + $"\t{nameof(Name)}: {Name},\n"
               + $"\t{nameof(Links)}: {(object?) _link ?? Links},\n"
               + "}";
    }
}

public record Links
{
    public Link Windows = null!;
    public Link? Mac;
    public Link? Linux;

    public override string ToString()
    {
        return "Links {"
               + $"\t{nameof(Windows)} = {Windows},\n"
               + $"\t{nameof(Mac)} = {Mac},\n"
               + $"\t{nameof(Linux)} = {Linux}\n"
               + "}";
    }

    public IEnumerable<Link> AsEnumerable()
    {
        if (Windows is not null)
            yield return Windows;
        if (Mac is not null)
            yield return Mac;
        if (Linux is not null)
            yield return Linux;
    }
}

public record Link : IXmlSerializable
{
    [XmlAttribute]
    public string SHA256 = null!;

    [XmlText]
    public string URL = null!;

    public override string ToString()
    {
        return $"[Link: {nameof(SHA256)} = {SHA256}, {nameof(URL)}: {URL}]";
    }
    
    public XmlSchema? GetSchema() => null;

    public void ReadXml(XmlReader reader)
    {
        var sha256 = reader.GetAttribute(nameof(SHA256));
        SHA256 = sha256 ?? throw new InvalidDataException("SHA256 attribute not found");
        URL = reader.ReadElementContentAsString().Trim();
    }

    public void WriteXml(XmlWriter writer)
    {
    }
}