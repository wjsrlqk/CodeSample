using CSV;
using System.Collections.Generic;
using UnityEngine;
using Cooking.Ingredient;
namespace Cooking.Ingredient
{
    public struct TagMappingData
    {
        public enum TagType
        {
            Main,
            Sub
        }
        public TagMappingData(CategoryData data)
        {
            m_category = data;
            m_subCategory = null;
        }
        public TagMappingData(SubCategoryData data)
        {
            m_category = null;
            m_subCategory = data;
        }
        CategoryData? m_category;
        SubCategoryData? m_subCategory;

        public TagType GetTagType()
        {
            if(m_category != null)
            {
                return TagType.Main;
            }
            else if(m_subCategory != null)
            {
                return TagType.Sub;
            }
            return TagType.Main;
        }
        public bool TryGetData(out CategoryData data)
        {
            data = default;
            if (m_category != null && GetTagType() == TagType.Main)
            {
                data = m_category.Value;
                return true;
            }
            return false;
        }
        public bool TryGetData(out SubCategoryData data)
        {
            data = default;
            if (m_subCategory != null && GetTagType() == TagType.Sub)
            {
                data = m_subCategory.Value;
                return true;
            }
            return false;
        }
        public bool TryGetValue(out int value)
        {
            value = 0;
            switch(GetTagType()) 
            {
                case TagType.Main:
                    if(m_category != null)
                    {
                        value = m_category.Value.value;
                        return true;
                    }
                    break;
                case TagType.Sub:
                    if(m_subCategory != null)
                    {
                        value = m_subCategory.Value.value;
                        return true;
                    }
                    break;
            }
            return false;
        }
        public int GetValue()
        {
            if(GetTagType() == TagType.Main)
            {
                if(m_category != null)
                {
                    return m_category.Value.value;
                }
            }
            if(GetTagType()==TagType.Sub)
            {
                if(m_subCategory!= null)
                {
                    return m_subCategory.Value.value;
                }
            }
            return 0;
        }
        public TagMappingData SetData(CategoryData data)
        {
            m_subCategory = null;
            m_category = data;
            return this;
        }
        public TagMappingData SetData(SubCategoryData data)
        {
            m_category = null;
            m_subCategory = data;
            return this;
        }
    }

    public struct CategoryData
    {
        public int value;
        public CSV.Category category;
    }
    public struct SubCategoryData
    {
        public int value;
        public CSV.SubCategory category;
    }
    public class IngredientTag
    {
        public int Unique { get; }
        public string Category { get; }
        public string SubCategory { get; }
        public int Grade { get; }
        public string Prefix { get; }
        public IReadOnlyList<TagMappingData> IngredientTagDatas { get => m_ingredientTagDatas; }
        public List<TagMappingData> m_ingredientTagDatas  = new List<TagMappingData>();
        public IngredientTag(int unique, string category, string subCategory, int grade, string prefix,List<TagMappingData> ingredientTags)
        {
            Unique = unique;
            Category = category;
            SubCategory = subCategory;
            Grade = grade;
            Prefix = prefix;
            m_ingredientTagDatas = ingredientTags;
        }
    }
}
public partial class IngredientList
{
    private Dictionary<int,IngredientTag> m_TagCacheDatas = new Dictionary<int, IngredientTag>();

    protected override void LoadCSVAfterProcess()
    {
        m_TagCacheDatas.Clear();
        foreach (var data in m_DataList)
        {
            if(m_TagCacheDatas.ContainsKey(data.Unique))
            {
                Debug.LogError($"중복된 키값!! Key : {data.Unique}");
                continue;
            }
            m_TagCacheDatas.Add(data.Unique,new IngredientTag(
                data.Unique,
                data.Category,
                data.SubCategory,
                data.Grade,
                data.Prefix,
                ParseIngredientTag(data.IngredientTag)
            ));
        }
    }

    public IReadOnlyDictionary<int,IngredientTag> TagCacheDatas => m_TagCacheDatas;

    public IngredientTag SeekTagByKey (int key)
    {
        if(m_TagCacheDatas.ContainsKey(key) == false)
            return null;

        return m_TagCacheDatas[key];
    }

    List<TagMappingData> ParseIngredientTag(string tag)
    {
        var result = new List<TagMappingData>();
        string[] splitTag = tag.Split(";");

        foreach (var iter in splitTag)
        {
            TagMappingData? data = null;
            var parts = iter.Split('=');
            int value = parts.Length > 1 ? int.Parse(parts[1].Trim()) : 0;

            data = CreateTagData(parts[0], value);

            if (data != null)
                result.Add(data.Value);
            else
                Debug.LogError($"Data Create Fail");
        }
        return result;
    }

    public static TagMappingData? CreateTagData (string requireTag, int value)
    {
        requireTag = requireTag.Replace(" ","");
        if (requireTag.StartsWith("CAT_"))
        {
            var catName = requireTag.Substring(4).Trim();
            CSV.Category categoryValue;
            if (System.Enum.TryParse(catName, out categoryValue))
            {
                var cat = new CategoryData {
                    category = categoryValue,
                    value = value
                };
                return new TagMappingData(cat);
            }
            else
            {
                Debug.LogError($"Try Parse Fail, Name : {catName}");
                return null;
            }
        }
        else if (requireTag.StartsWith("SUB_"))
        {
            var subName = requireTag.Substring(4).Trim();
            CSV.SubCategory subCategoryValue;
            if (System.Enum.TryParse(subName, out subCategoryValue))
            {
                var sub = new SubCategoryData {
                    category = subCategoryValue,
                    value = value
                };
                return new TagMappingData(sub);
            }
            else
            {
                Debug.LogError($"Try Parse Fail, Name : {subName}");
                return null;
            }
        }
        return null;
    }
}
