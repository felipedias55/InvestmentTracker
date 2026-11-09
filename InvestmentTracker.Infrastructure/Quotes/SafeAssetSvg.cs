using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace InvestmentTracker.Infrastructure.Quotes
{
    internal static class SafeAssetSvg
    {
        public static byte[] Normalize(byte[] bytes)
        {
            using var reader = XmlReader.Create(new MemoryStream(bytes), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 262144 });
            var document = XDocument.Load(reader);
            XNamespace ns = "http://www.w3.org/2000/svg";
            var root = document.Root;
            if (root?.Name != ns + "svg") throw new XmlException();
            var elements = new HashSet<string>(["svg", "g", "path", "rect", "circle", "ellipse", "line", "polyline", "polygon", "defs", "linearGradient", "radialGradient", "stop", "clipPath", "title", "desc"]);
            var attributes = new HashSet<string>(["id", "viewBox", "width", "height", "x", "y", "x1", "x2", "y1", "y2", "cx", "cy", "r", "rx", "ry", "d", "points", "fill", "fill-rule", "fill-opacity", "stroke", "stroke-width", "stroke-linecap", "stroke-linejoin", "stroke-opacity", "opacity", "transform", "gradientTransform", "gradientUnits", "offset", "stop-color", "stop-opacity", "clip-path", "clip-rule", "preserveAspectRatio"]);
            if (root.DescendantsAndSelf().Count() > 2000) throw new XmlException();
            foreach (var element in root.DescendantsAndSelf())
            {
                if (element.Name.Namespace != ns || !elements.Contains(element.Name.LocalName)) throw new XmlException();
                foreach (var attribute in element.Attributes().ToArray())
                {
                    if (attribute.IsNamespaceDeclaration) { attribute.Remove(); continue; }
                    if (attribute.Name.Namespace != XNamespace.None || !attributes.Contains(attribute.Name.LocalName)) { attribute.Remove(); continue; }
                    if (Regex.IsMatch(attribute.Value, @"url\(", RegexOptions.IgnoreCase) && !Regex.IsMatch(attribute.Value, @"^url\(#[A-Za-z0-9_\-]+\)$")) throw new XmlException();
                    if (attribute.Value.Contains(':') || attribute.Value.Contains('<') || attribute.Value.Contains('&')) throw new XmlException();
                }
            }
            // Fixed rasterization viewport; original proportions are retained through viewBox.
            if (root.Attribute("viewBox") is null) root.SetAttributeValue("viewBox", $"0 0 {root.Attribute("width")?.Value ?? "128"} {root.Attribute("height")?.Value ?? "128"}");
            root.SetAttributeValue("width", "128"); root.SetAttributeValue("height", "128");
            return Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting));
        }
    }
}
