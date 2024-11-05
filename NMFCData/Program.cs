using System.Text.RegularExpressions;
using System.Xml.Linq;

public class Program
{
    public static void Main(string[] args)
    {
        string filePath1 = "ArticleDataOld.xml"; // Path to the first XML file
        string filePath2 = "ArticleDataNew.xml"; // Path to the second XML file
        string outputPath = "comparison_output.txt"; // Output file path

        XDocument doc1 = XDocument.Load(filePath1);
        XDocument doc2 = XDocument.Load(filePath2);

        CompareXmlDocuments(doc1, doc2, outputPath);
    }

    static void CompareXmlDocuments(XDocument doc1, XDocument doc2, string outputPath)
    {
        var articles1 = doc1.Root.Elements("Article").ToDictionary(article => article.Element("Item").Value);
        var articles2 = doc2.Root.Elements("Article").ToDictionary(article => article.Element("Item").Value);

        var addedArticles = articles2.Keys.Except(articles1.Keys).ToList();
        var deletedArticles = articles1.Keys.Except(articles2.Keys).ToList();

        using (StreamWriter writer = new StreamWriter(outputPath))
        {
            AddArticles(writer, addedArticles, articles2);

            DeleteArticels(deletedArticles, writer, articles1);

            writer.WriteLine("\nUpdates:");
            foreach (var item in articles1.Keys.Intersect(articles2.Keys))
            {
                var article1 = articles1[item];
                var article2 = articles2[item];

                if (!XNode.DeepEquals(article1, article2))
                {
                    // Compare attributes
                    var itemsEqual = AreItemsEqual(article1, article2);

                    if (!itemsEqual)
                    {
                        var classificationElementValue = article2.Element("classification")?.Value;
                        string description = article2.Element("Description")?.Value?.Replace("'", "''") ?? ""; // Replace single quotes with two single quotes

                        if (classificationElementValue != "")
                        {
                            writer.WriteLine($"UPDATE itemCatalog.NMFCItems SET Description = '{description}', Class = {classificationElementValue}, ArticlePointer = '{article2.Attribute("ArticlePointer")?.Value}' WHERE NMFCItemId = {item}");
                        }
                        else
                        {
                            writer.WriteLine($"UPDATE itemCatalog.NMFCItems SET Description = '{description}', Class = NULL, ArticlePointer = '{article2.Attribute("ArticlePointer")?.Value}' WHERE NMFCItemId = {item}");
                        }
                    }


                    // Compare child elements
                    var subItems1 = article1.Elements("SubItems").Elements("SubItem").ToDictionary(e => e.Element("Item").Value);
                    var subItems2 = article2.Elements("SubItems").Elements("SubItem").ToDictionary(e => e.Element("Item").Value);

                    var addedSubItems = subItems2.Keys.Except(subItems1.Keys).ToList();
                    var deletedSubItems = subItems1.Keys.Except(subItems2.Keys).ToList();

                    foreach (var addItem in addedSubItems)
                    {
                        var subItem = subItems2[addItem];

                        var subItemValue = subItem.Element("Item")?.Value.Trim();
                        var description = subItem.Element("Description")?.Value.Trim();
                        var SubItemClassification = subItem.Element("classification")?.Value.Trim();

                        var replaceItemValue = Regex.Replace(subItemValue, @"^sub\s*|\s*sub$", "", RegexOptions.IgnoreCase).Trim();

                        if (SubItemClassification != "")
                        {
                            writer.WriteLine($"INSERT INTO itemCatalog.NMFCSubItems(NMFCItemId, SubItemId, SubItemDescription, Class) VALUES('{item}', '{replaceItemValue}', '{description}', {SubItemClassification})");
                        }
                        else
                        {
                            writer.WriteLine($"INSERT INTO itemCatalog.NMFCSubItems(NMFCItemId, SubItemId, SubItemDescription, Class) VALUES('{item}', '{replaceItemValue}', '{description}', NULL)");
                        }
                    }

                    foreach (var element in deletedSubItems)
                    {
                        writer.WriteLine($"DELETE FROM itemCatalog.NMFCSubItems where NMFCItemId = '{item}'");
                    }

                    foreach (var subItem in subItems1.Keys.Intersect(subItems2.Keys))
                    {
                        var subItem1 = subItems1[subItem];
                        var subItem2 = subItems2[subItem];

                        if (!XNode.DeepEquals(subItem1, subItem2))
                        {
                            // Compare attributes
                            var subItemsEqual = AreSubItemsEqual(subItem1, subItem2);

                            if (!subItemsEqual)
                            {
                                var subClassItem = subItem2.Element("Item")?.Value;
                                var subClassValue = subItem2.Element("classification")?.Value;
                                string subClassDescription = subItem2.Element("Description")?.Value?.Replace("'", "''") ?? ""; // Replace single quotes with two single quotes

                                var replaceItemValue = Regex.Replace(subClassItem, @"^sub\s*|\s*sub$", "", RegexOptions.IgnoreCase).Trim();

                                if (subClassValue != "")
                                {
                                    writer.WriteLine($"UPDATE itemCatalog.NMFCSubItems SET SubItemDescription = '{subClassDescription}', Class = {subItem2.Element("classification")?.Value} WHERE NMFCItemId = {item} AND SubItemId = '{replaceItemValue}'");
                                }
                                else
                                {
                                    writer.WriteLine($"UPDATE itemCatalog.NMFCSubItems SET SubItemDescription = '{subClassDescription}', Class = NULL WHERE NMFCItemId = {item} AND SubItemId = '{replaceItemValue}'");
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private static void DeleteArticels(List<string> deletedArticles, StreamWriter writer, Dictionary<string, XElement>? articles1)
    {
        writer.WriteLine("\nDeletions:");
        foreach (var item in deletedArticles)
        {
            var subItems = articles1[item].Elements("SubItems").Elements("SubItem");

            if (subItems.Any())
            {
                writer.WriteLine($"DELETE FROM itemCatalog.NMFCSubItems where NMFCItemId = '{item}'");
            }

            writer.WriteLine($"DELETE FROM itemCatalog.NMFCItems where NMFCItemId = '{item}'");
        }
    }

    private static void AddArticles(StreamWriter writer, List<string>? addedArticles, Dictionary<string, XElement>? articles2)
    {
        writer.WriteLine("Additions:");
        foreach (var item in addedArticles)
        {
            // Access otehr properties
            var newArticle = articles2[item];
            var classificationElementValue = newArticle.Element("classification")?.Value;
            if (classificationElementValue != "")
            {
                writer.WriteLine($"INSERT INTO itemCatalog.NMFCItems (NMFCItemId, Description, Class, ArticlePointer) VALUES('{item}', '{newArticle.Element("Description")?.Value}', {classificationElementValue}, '{newArticle.Attribute("ArticlePointer")?.Value}')");
            }
            else
            {
                writer.WriteLine($"INSERT INTO itemCatalog.NMFCItems (NMFCItemId, Description, Class, ArticlePointer) VALUES('{item}', '{newArticle.Element("Description")?.Value}', NULL, '{newArticle.Attribute("ArticlePointer")?.Value}')");
            }

            // Add sub Items script
            var subItems = newArticle.Elements("SubItems").Elements("SubItem");
            foreach (var subItem in subItems)
            {
                var subItemValue = subItem.Element("Item")?.Value.Trim();
                var description = subItem.Element("Description")?.Value.Trim();
                var SubItemClassification = subItem.Element("classification")?.Value.Trim();

                var replaceItemValue = Regex.Replace(subItemValue, @"^sub\s*|\s*sub$", "", RegexOptions.IgnoreCase).Trim();

                if (SubItemClassification != "")
                {
                    writer.WriteLine($"INSERT INTO itemCatalog.NMFCSubItems(NMFCItemId, SubItemId, SubItemDescription, Class) VALUES('{item}', '{replaceItemValue}', '{description}', {SubItemClassification})");
                }
                else
                {
                    writer.WriteLine($"INSERT INTO itemCatalog.NMFCSubItems(NMFCItemId, SubItemId, SubItemDescription, Class) VALUES('{item}', '{replaceItemValue}', '{description}', NULL)");
                }
            }
        }
    }

    private static bool AreItemsEqual(XElement? article1, XElement? article2)
    {
        var oldDescription = article1.Element("Description")?.Value;
        var newDescription = article2.Element("Description")?.Value;

        var oldClass = article1.Element("classification")?.Value;
        var newClass = article2.Element("classification")?.Value;

        var oldArticlePointer = article1.Attribute("ArticlePointer")?.Value;
        var newArticlePointer = article2.Attribute("ArticlePointer")?.Value;

        if (newDescription.Equals(oldDescription) && newClass.Equals(oldClass) && newArticlePointer.Equals(oldArticlePointer))
        {
            return true;
        }
        return false;
    }

    private static bool AreSubItemsEqual(XElement? subItem1, XElement? subItem2)
    {
        var oldDescription = subItem1.Element("Description")?.Value;
        var newDescription = subItem2.Element("Description")?.Value;

        var oldClass = subItem1.Element("classification")?.Value;
        var newClass = subItem2.Element("classification")?.Value;


        if (newDescription.Equals(oldDescription) && newClass.Equals(oldClass))
        {
            return true;
        }
        return false;
    }
}
