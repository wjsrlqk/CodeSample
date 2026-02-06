// ------------------------------------------------------------------------------
// ⚠️ 이 파일은 CSVCodeGenerator 툴에 의해 자동 생성되었습니다.
// ⚠️ 직접 수정하지 마십시오. CSV 원본을 수정하고 툴을 다시 실행하세요.
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

namespace CSV
{
    public enum Category
    {
        Meat,
        Fish,
        Veggie,
        Fruit,
    }
    public enum SubCategory
    {
        Pig,
        Cow,
        Cow_Tail,
        Chicken,
        Sardine,
        Tomato,
        Potato,
        Mushroom,
        Berry,
        Slime,
    }
}
[System.Serializable]
public struct IngredientListData
{
    public int Unique;
    public string ItemName;
    public string Category;
    public string SubCategory;
    public int Grade;
    public string Prefix;
    public string IngredientTag;
    public string Icon;
}

public partial class IngredientList : TableBase
{
    public static IngredientList Table { get; private set; } = new IngredientList();
    private List<IngredientListData> m_DataList = new List<IngredientListData>();
    private Dictionary<int, IngredientListData> m_DataByUnique = new Dictionary<int, IngredientListData>();
    private Dictionary<(string,string,int), IngredientListData> m_DataByCategorySubCategoryGrade = new Dictionary<(string,string,int), IngredientListData>();
    private Dictionary<(string,string), List<IngredientListData>> m_DataByCategorySubCategory = new Dictionary<(string,string), List<IngredientListData>?>();

    public IReadOnlyList<IngredientListData> repo { get => m_DataList;}
    public void LoadFromCSV(string csvText,ref int lineCount)
    {
        var lines = csvText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        bool inTable = false;
        int step = 0;
        for (int i = lineCount; i < lines.Length; i++) {
            lineCount = i;
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("//")) continue;
            if (line.StartsWith("[Table]")) { inTable = true; step = 0; continue; }
            if (line.StartsWith("[END]")) { ++lineCount; if(inTable){break;} else{ continue; } }
            if (!inTable) continue;
            if (line.StartsWith("#Meta:")) continue;
            if (step < 2) { step++; continue; }
            var cells = line.Split(',');
            if (cells[0].StartsWith("//")) continue;
            if (cells.All(string.IsNullOrEmpty)) continue;
            IngredientListData data = new IngredientListData();
            data.Unique = int.TryParse(cells[0], out var v0) ? v0 : 0;
            data.ItemName = string.IsNullOrEmpty(cells[1]) ? string.Empty : cells[1];
            data.Category = string.IsNullOrEmpty(cells[2]) ? string.Empty : cells[2];
            data.SubCategory = string.IsNullOrEmpty(cells[3]) ? string.Empty : cells[3];
            data.Grade = int.TryParse(cells[4], out var v4) ? v4 : 0;
            data.Prefix = string.IsNullOrEmpty(cells[5]) ? string.Empty : cells[5];
            data.IngredientTag = string.IsNullOrEmpty(cells[6]) ? string.Empty : cells[6];
            data.Icon = string.IsNullOrEmpty(cells[7]) ? string.Empty : cells[7];
            m_DataList.Add(data);
            m_DataByUnique[data.Unique] = data;
            m_DataByCategorySubCategoryGrade[(data.Category,data.SubCategory,data.Grade)] = data;
            if (!m_DataByCategorySubCategory.ContainsKey((data.Category,data.SubCategory)))
                m_DataByCategorySubCategory[(data.Category,data.SubCategory)] = new List<IngredientListData>();
            m_DataByCategorySubCategory[(data.Category,data.SubCategory)].Add(data);
        }
         LoadCSVAfterProcess();
    }

    public IngredientListData GetByUnique(int key) => m_DataByUnique[key];
    public bool TryGetByUnique(int key, out IngredientListData data) => m_DataByUnique.TryGetValue(key, out data);

    public IngredientListData GetByCategorySubCategoryGrade((string,string,int) key) => m_DataByCategorySubCategoryGrade[key];
    public bool TryGetByCategorySubCategoryGrade((string,string,int) key, out IngredientListData data) => m_DataByCategorySubCategoryGrade.TryGetValue(key, out data);

    public IReadOnlyList<IngredientListData> GetByCategorySubCategory((string,string) group) =>
        m_DataByCategorySubCategory.TryGetValue(group, out var list) ? list : new List<IngredientListData>();
}
namespace IngredientListLoader
{
    public static class TableLoader
    {
        public static void LoadFromCSV(string csvText)
        {
            int lineCount = 0;
            IngredientList.Table.LoadFromCSV(csvText, ref lineCount);
        }
    }
}
