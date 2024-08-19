using System.Xml;

namespace GameServer
{
    public static class XmlParser
    {
        public static string[] ChildContentFromParent(string xmlPath, string elementName, string parentElement)
        {
            List<string> result = new List<string>();

            // Convert the element and parent names to lowercase for case-insensitive matching
            elementName = elementName.ToLower();
            parentElement = parentElement.ToLower();

            try
            {
                using (XmlReader reader = XmlReader.Create(xmlPath))
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element && reader.Name.ToLower() == parentElement)
                        {
                            string childContent = InnerNodeCaseInsensitive(reader, elementName);
                            if (!string.IsNullOrEmpty(childContent))
                            {
                                result.Add(childContent);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error($"Failed to parse XML at '{xmlPath}'. Exception: {e}");
            }

            return result.ToArray();
        }

        // Iterate over the Inner elements in case-insensitive mode
        public static string InnerNodeCaseInsensitive(XmlReader reader, string elementName)
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name.ToLower() == elementName)
                {
                    return reader.ReadElementContentAsString().ToLower();
                }
            }

            return string.Empty;
        }
    }
}