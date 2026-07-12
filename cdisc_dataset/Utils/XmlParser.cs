using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using cdisc_dataset.Models;

namespace cdisc_dataset.Utils;

public static class XmlParser
{
    public static List<Dataset> GetDatasetFromXml(string path)
    {
        var xmlDoc = new XmlDocument();
        //xmlDoc.Load(@"C:\Users\zhi\Desktop\Temp\SDTM-IG 3.4 (FDA).xml");
        xmlDoc.Load(path);
        var xmlNamespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
        xmlNamespaceManager.AddNamespace("ns","http://www.cdisc.org/ns/odm/v1.3");
        xmlNamespaceManager.AddNamespace("xsi","http://www.w3.org/2001/XMLSchema-instance");
        xmlNamespaceManager.AddNamespace("xlink","http://www.w3.org/1999/xlink");
        xmlNamespaceManager.AddNamespace("def","http://www.cdisc.org/ns/def/v2.0");
        xmlNamespaceManager.AddNamespace("val","http://www.opencdisc.org/schema/validator");

        var study = xmlDoc.SelectSingleNode("//ns:Study", xmlNamespaceManager);
        var version = study.Attributes["OID"].Value;
        XmlNodeList nodeList = xmlDoc.SelectNodes("//ns:ItemGroupDef", xmlNamespaceManager);
        List<Dataset> list = new List<Dataset>();
        if (nodeList != null)
        {
            foreach (XmlNode childNode in nodeList)
            {
                var dataset = new Dataset();
                dataset.Name = childNode.Attributes["Name"].Value;
                dataset.Repeating = childNode.Attributes["Repeating"].Value;
                dataset.ReferenceData = childNode.Attributes["IsReferenceData"].Value;
                dataset.Class =  childNode.Attributes["def:Class"].Value;
                dataset.Standard = version;
                dataset.HasNoData = "No";
                var structure = childNode.Attributes["def:Structure"];
                if (structure != null)
                {
                    dataset.Structure = structure.Value;
                }

                var innerText = childNode.SelectSingleNode("ns:Description/ns:TranslatedText",xmlNamespaceManager).InnerText;
                dataset.Label = innerText;
                var xmlNodeList = childNode.SelectNodes("ns:ItemRef[@KeySequence]/@ItemOID",xmlNamespaceManager);
                List<string> keys = new List<string>();
                if (xmlNodeList != null && xmlNodeList.Count > 0)
                {
                    foreach (XmlAttribute node in xmlNodeList)
                    {
                        keys.Add(node.Value.Split(".").LastOrDefault());
                    }
                }

                var keyVars = String.Join(", ",keys);
                dataset.KeyVariables = keyVars;

                var vars = childNode.SelectNodes("ns:ItemRef",xmlNamespaceManager);
                List<Variable> varList = new List<Variable>();
                foreach (XmlNode var in vars)
                {
                    Variable variable = new Variable();
                    var id = var.Attributes["ItemOID"].Value;
                    var order = var.Attributes["OrderNumber"].Value;
                    var orderNum = int.Parse(order);
                    var mandatory = var.Attributes["Mandatory"].Value;
                    var role = var.Attributes["Role"].Value;
                    var core  = var.Attributes["val:Core"].Value;
                    var itemdef = var.SelectSingleNode("//ns:ItemDef[@OID='"+id+"']",xmlNamespaceManager);
                    var dataType = itemdef.Attributes["DataType"].Value;
                    var label = itemdef.SelectSingleNode("ns:Description/ns:TranslatedText",xmlNamespaceManager).InnerText;
                    var codelist = itemdef.SelectSingleNode("ns:CodeListRef/@CodeListOID",xmlNamespaceManager);
                    variable.DatasetName = id.Split(".")[1];
                    variable.VariableName = id.Split(".").LastOrDefault();
                    variable.Label = label;
                    variable.DataType = dataType;
                    variable.Mandatory = mandatory;
                    variable.Role = role;
                    variable.Order = orderNum;
                    variable.Core = core;
                    if (codelist != null)
                    {
                        var codelistValue = codelist.Value;
                        variable.DefaultCodeList = codelistValue;
                    }
                    varList.Add(variable);
                    //Console.WriteLine(id);
                }
                dataset.Variables = varList;
                list.Add(dataset);
            }
        }
        return list;
    }
}