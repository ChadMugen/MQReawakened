﻿using Microsoft.Extensions.DependencyInjection;
using Server.Base.Core.Extensions;
using Server.Reawakened.XMLs.Abstractions;
using System.Xml;

namespace Server.Reawakened.XMLs.Bundles;

public class VendorCatalog : VendorCatalogsXML, IBundledXml
{
    public string BundleName => "vendor_catalogs";
    public bool Priority => false;

    public void InitializeVariables()
    {
        _rootXmlName = BundleName;
        _hasLocalizationDict = true;

        this.SetField<VendorCatalogsXML>("_levelUpCatalogs", new Dictionary<int, List<int>>());
        this.SetField<VendorCatalogsXML>("_vendorCatalogs", new Dictionary<int, List<int>>());
        this.SetField<VendorCatalogsXML>("_vendorCatalogIds", new Dictionary<string, int>());
        this.SetField<VendorCatalogsXML>("_vendorCatalogIdToVendorId", new Dictionary<int, int>());
        this.SetField<VendorCatalogsXML>("_levelUpCatalogs", new Dictionary<int, List<int>>());
        this.SetField<VendorCatalogsXML>("_cashShops", new Dictionary<string, CashShop>());
        this.SetField<VendorCatalogsXML>("_superPacks", new Dictionary<int, Dictionary<int, int>>());
        this.SetField<VendorCatalogsXML>("_loots", new Dictionary<int, List<LootData>>());
    }

    public void EditDescription(XmlDocument xml, IServiceProvider services)
    {
        var items = xml.SelectNodes("/vendor_catalogs/superpacks/superpack/item");
        var vendors = xml.SelectNodes("/vendor_catalogs/vendor");

        if (items != null)
        {
            foreach (XmlNode aNode in items)
            {
                if (aNode.Attributes == null)
                    continue;

                var idAttribute = aNode.Attributes["quantity"];

                if (idAttribute != null)
                    continue;

                var quantity = xml.CreateAttribute("quantity");
                quantity.Value = "1";

                aNode.Attributes.Append(quantity);
            }
        }

        if (vendors != null)
        {
            var internalCatalog = services.GetRequiredService<InternalVendorCatalog>();
            var miscTextDict = services.GetRequiredService<MiscTextDictionary>();
            var preExistingCategories = new List<int>();

            foreach (XmlNode aNode in vendors)
            {
                if (aNode.Attributes == null)
                    continue;

                var categoryAttribute = aNode.Attributes["catalogid"];

                if (categoryAttribute == null)
                    continue;

                preExistingCategories.Add(int.Parse(categoryAttribute.InnerText));
            }

            var lastSmallest = 0;

            foreach (XmlNode childNode in xml.ChildNodes)
            {
                if (!(childNode.Name == "vendor_catalogs"))
                {
                    continue;
                }

                foreach (var vendor in internalCatalog.VendorCatalog.Values)
                {
                    if (vendor.CatalogId != -1)
                        return;

                    var name = miscTextDict.GetLocalizationTextById(vendor.NameId);
                    var catalogId = FindSmallest(preExistingCategories, lastSmallest);
                    var vendorId = catalogId;

                    lastSmallest = catalogId;

                    vendor.CatalogId = catalogId;
                    vendor.VendorId = vendorId;

                    var vendorElement = xml.CreateElement("vendor");

                    vendorElement.SetAttribute("catalogid", catalogId.ToString());
                    vendorElement.SetAttribute("vendorid", catalogId.ToString());
                    vendorElement.SetAttribute("name", name);

                    foreach (var item in vendor.Items)
                    {
                        var itemElement = xml.CreateElement("item");
                        itemElement.SetAttribute("id", item.ToString());
                        vendorElement.AppendChild(itemElement);
                    }

                    childNode.AppendChild(vendorElement);
                }
            }
        }
    }

    private static int FindSmallest(List<int> intArray, int lastSmallest)
    {
        var sortedSet = intArray.Where(x => x > lastSmallest).Distinct().OrderBy(x => x).ToArray();
        if (sortedSet.Length == 0) return lastSmallest + 1;
        var smallestMissing = lastSmallest + 1;
        for (var i = 0; i < sortedSet.Length; i++)
        {
            if (smallestMissing < sortedSet[i]) break;
            smallestMissing = sortedSet[i] + 1;
        }
        return smallestMissing;
    }

    public void ReadDescription(string xml) => ReadDescriptionXml(xml);

    public void FinalizeBundle()
    {
    }
}
